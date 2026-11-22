# Janela nativa de seleção e as duas famílias visuais de fusão — 22/11/2026

**Pergunta:** como reimplementar a janela flutuante sem criar mais coordenadas e tempos por aproximação, e quais novas Draws de fato ampliariam o badge?

**Material examinado diretamente:** `joey-knowledge.sqlite` (6.518 funções; decompilações de construtores/chamadores), o ZIP `ghidra-visual-virtual-methods.zip` (assembly e decompilações transitórias dos slots virtuais) e `joey-knowledge-visual-dat.sqlite` (15 manifests, 50 imagens, 85 referências ao conteúdo de listas). Alvo `joey_pc.exe`, SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Não foi executado o jogo nem criado um dump novo do Ghidra nesta rodada; os três materiais são evidências já existentes.

## 1. O componente de seleção tem sua própria máquina de estados

| Papel | Evidência de assembly/decompilação | Confiança |
| --- | --- | --- |
| Criador | `0040C940 → 0043A060`; vtable `005DABD8`, camada de cenário `0x700`, até cinco objetos de carta nas camadas `0x701+` | Alta |
| Update | Vtable `+8 → 0043B0F0`; primeiro chama `0043E280`; lê eventos `005664A0(1,2,3,4)`; depois atualiza destinos dos cinco objetos | Alta para fluxo, não para teclas físicas |
| Draw | Vtable `+C → 0043B280`; quando `obj+0x14 == 0` desenha fundo/slot/atributos; quando não-zero aplica transformação com cosseno | Alta para ramos, pass de composição incompleto |
| Animação | `0043A060` coloca `obj+0x14=0x100`; `0043E280` diminui em `0x20` por Update até zero; nesse ponto chama virtual `+0x24` | Alta: oito updates de entrada; duração em ms depende do scheduler |
| Encerramento | `0043E280` trata contador negativo e desativa após ficar abaixo de `-0xFF` | Alta para condicional; origem do contador negativo não mapeada |

A janela não é apenas um overlay de dez cartões: ela possui **objeto de apresentação, recursos próprios e transição geométrica de entrada/saída**. A renderização usa um ramo normal e outro que interpola **posição e dimensões**; afirmar somente fade ou escala vertical seria incompleto.

### Geometria recuperada

`0043A060` define `obj+0x1C = DAT_005DB570` e `obj+0x20 = 300`. A fórmula em `0043ABA0` é:

```text
slotX = anchorX + 64 * (slot - 2)          // slot 0..4
slotY = anchorY + resourceYOffset[variant] - resourceHeight / 2
```

Portanto, deslocamentos horizontais = `[-128, -64, 0, +64, +128]`. O valor global `DAT_005DB570`, a semântica definitiva dos campos de recurso, o pivô e a transformação completa de Draw **não** foram inferidos de uma captura. `NativeSelectionWindow.SlotAnchor` recebe as medidas reais de seu consumidor.

O Draw de transição em `0043B280` contém:

```text
cosineIndex = ((abs(counter) + 256) / 2 - 128) & 511
scaleFixed8 = DAT_00A52EE8[cosineIndex] >> 8
```

Há cálculos posteriores de `anchorX/Y + ((current-anchor) * scaleFixed8 >> 8)` e de largura/altura com a mesma escala. A decompilação avisa `Type propagation algorithm not settling` e usa registradores `unaff_*`; **não copiar esses registradores como valores finais de composição**. O modelo exige a tabela original para calcular `scaleFixed8`, em vez de substituir por `Math.Cos` sem autorização.

### Navegação: janela deslizante

`0043B0F0` usa `obj+0x1B8` para o slot selecionado e `obj+0x214` para o deslocamento da janela; `0x218/0x21C` são limites de scroll, inicializados no construtor conforme `total > 5`. Evento interno 4 avança; evento 3 retrocede (a tecla física correspondente não foi provada).

Exemplo com 7 cartas, como em `NativeSelectionWindow`:

| Índice absoluto | Slot selecionado | Scroll |
| ---: | ---: | ---: |
| 0 | 0 | 0 |
| 1 | 1 | 0 |
| 2 | 2 | 0 |
| 3 | 3 | 0 |
| 4 | 4 | 0 |
| 5 | 4 | 1 |
| 6 | 4 | 2 |

Não há paginação 2×5 nem um sexto cartão visível. O contexto e o sentido exato da seleção de **múltiplas** cartas, inclusive as flags do efeito, pertencem ao Core ou a outro estado do objeto e ainda precisam de investigação.

### Modos e recursos

| Modo da construção | Prefixo dos manifests nativos | Exemplos de tamanho do fundo |
| ---: | --- | --- |
| 1 | `fusionlist1/2.txt` | `list_fusion0.bmp` 422×223, `list_fusion1.bmp` 422×244 |
| 3 | `gravelist1/2.txt` | `list_grave0/1.bmp` 412×204 |
| 4 | `excludelist1/2.txt` | `list_jyoga_jibu/aite.bmp` 384×180 |
| Outros | `selectlist1/2.txt` | `list_select0/1.bmp` 451×191 |

Os sufixos `1/2` são condicionados pela perspectiva no construtor (com um caso especial em modo `0x29`). Não rotular as variantes de jogador/CPU sem rastrear a expressão completa. O DAT de scroll possui `sele_box.bmp` (41×12), `fusi_box.yga` (47×32), setas específicas e um inteiro empacotado decodificado por `0043BA30`; a função de cada campo do inteiro ainda não está provada.

## 2. As duas Draws de fusão não são simplesmente a janela de lista

**Primeira família:** `00408270` espera o scheduler ficar livre e, no estado de fusão `DAT_00A558AC & 0x7F == 1`, cria objeto de **0xC4 bytes** por `004520E0`; esse construtor instala a vtable `005DB364`, carrega `duel/effect/fusion.txt` e usa recursos globais `card/list_card.txt` + `mini/list_card.txt`. A Draw é `004524F0`, Update `004524E0` (ainda não decompiladas no banco disponível).

**Segunda família:** `00487C70` chama `00487930` e, em determinado caso (retorno zero), cria um objeto de **0xB0 bytes** por `004874C0`. A vtable é `005DBD54`; também usa `duel/effect/fusion.txt`. Seu campo de camada recebe `((descriptor >> 6)&0xFF)*0x10 + 0x201` se `(descriptor & 0x3E) == 0x16`; fora desse caso, camada `0x110`. A Draw `004877F0` e Update `004877B0` também faltam.

**Conclusão sustentada:** são dois tipos de objeto visual com caminhos de criação e camadas diferentes. **Hipótese ainda aberta:** o primeiro é um efeito geral e o segundo é um efeito vinculado à carta; isso precisa do Draw para determinar o conteúdo de cada um. Nenhuma das duas Draws deve ser marcada implementada só por existir `FusionSelectionScreen.cs` — esse arquivo desenha um diálogo procedural, não a animação de fusão do executável.

## 3. Trabalho desta rodada e o que ainda falta

- Introduzido `NativeSelectionWindow` no `Joey.LookAndFeel`, **sem integração visual prematura**. O modelo codifica contagem de cinco slots, indexação com scroll, transição de oito updates, geometria parametrizada, fórmula nativa de lookup trigonométrico e mapeamento de manifests.
- Testes sintéticos cobrem listas de 0, 3 e 7 cartas, retorno do scroll, entrada de oito updates, posições simétricas e cálculo com tabela sintética declaradamente não nativa.
- Os **passos restantes** são obter disassembly e decompilações para as quatro rotinas `004524E0`, `004524F0`, `004877B0`, `004877F0`; mapear a interpolação de `0043B280` sem os `unaff_*` ambíguos; e integrar a janela no lugar dos overlays provisórios mantendo `PendingCardSelection` e `PendingFusionSummon`.

A extração dirigida já é suportada pelo script existente:

```bash
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004524E0 004524F0 004877B0 004877F0 \
  --disassemble --retry-missing \
  --output scratch/ghidra-fusion-remaining.zip
```

Pode ser executada na máquina que contém o Ghidra e o projeto Joey, quando chegarmos a essa etapa. **Não é necessário recriar o SQLite inteiro agora.** A importação das decompilações transitórias deve respeitar proveniência e SHA do EXE, sem reclassificar funções Ghidra ausentes como persistentes.

**Cobertura:** permanece **19/34 Partial (55,88%) e 0 Verified**. Um modelo de layout/estado e uma investigação de construtores não equivalem à implementação das três Draws.

## 4. Encerramento da janela: handler e contador confirmados no SQLite

A inspeção direta das funções `0043E0A0` e `0043E280` no
`research-selection-dat.sqlite` esclareceu o ramo negativo do contador:

- `0043E0A0` escreve **`obj+0x14=-1`**, registra o argumento de fechamento em `obj+0x10`, chama `005BFCC0` e o método virtual `+0x28`. Ainda falta comprovar quais eventos de UI chamam esse handler e o significado de seu argumento.
- `0043E280` subtrai **`0x20` por Update** sempre que `obj+0x14!=0`; ao alcançar zero chama virtual `+0x24` (fim da entrada).
- Durante o fechamento, o contador percorre **-1 → -33 → -65 → -97 → -129 → -161 → -193 → -225 → -257**. Quando fica abaixo de `-0xFF`, o original chama virtual `+0x2C` e desativa o objeto pelo byte `obj+4`. Assim, são **oito Updates do início negativo até a desativação**, sendo -225 o último contador a ser desenhado se a renderização acompanhar um Update por frame.

O `NativeSelectionWindow` passou a modelar `BeginClosing()`,
`AdvanceClosing()`, `IsClosing`, `IsClosed` e `TransitionCounter`
(que expõe o contador atual para a função de interpolação já extraída).
Não coloca automaticamente a janela em fechamento por um botão provisório
nem considera provada a tecla de confirmação; essa associação depende
dos chamadores.

Os testes novos cobrem os oito passos, impedimento de navegação durante
a saída, encerramento único e ausência de Updates depois de desativado.
Essa fatia continua sendo **modelo visual, não Draw integrada**;
cobertura visual segue 19/34.

## 5. Estado da investigação de monstros em sessão paralela

O mantenedor relatou conclusão da **investigação** dos monstros
em uma sessão Codex paralela. Não foi fornecido ainda um relatório
ou commit dessa sessão nesta rodada; não inferir que todos os efeitos
tenham sido implementados ou comparados com o original.
Na versão pública consultada em 22/11, `docs/cards/PLAYABILITY.md`
registra 337/499 `Done` e 3 `Partial` na trilogia, 98/194
`Done` e 2 `Partial` em Joey, e zero `Verified`.
Não alterar cobertura por esse relato sem reconciliar as evidências
quando forem publicadas. A frente visual da janela de seleção
permanece independente da investigação das regras.

## 6. Correção por disassembly da interpolação em `0043B280`

Ao confrontar o modelo C# de 22/11 com os bytes de assembly já exportados
no bundle `ghidra-visual-virtual-methods.zip`, arquivo
`retry-008-0043B280.txt`, foi encontrada uma divergência real.

O ramo não-zero de `0043B280` executa `0043B2E6..0043B31B`:

```text
counter > 0  ? (256-counter) : (256+counter)
CDQ; SUB EAX, EDX; SAR EAX,1      ; signed divide by 2 toward zero
SUB EAX,128; AND EAX,511          ; índice do atlas trigonométrico
MOV EBX,[0x00A52EE8 + 4*index]; SAR EBX,8
```

Logo, **índice = `(((256-|counter|)/2)-128)&511`** (divisão inteira
truncada para zero). Exemplos independentes da tabela de cosseno:
`0→0`, `128→448`, `-128→448`, `256→384`, `-1→511`,
`-33→495`, `-225→399`.

O modelo anterior usava `((abs(counter)+256)/2-128)&511`, que
consulta o sentido **positivo** da tabela; poderia passar em testes
com tabela simétrica, mas não preservava a instrução real.
`NativeSelectionWindow.TransitionCosineIndex` e o teste com
índices assimétricos corrigem o problema.

Também fica agora documentada a sequência exata de `0043B31B..0043B380`:
`0045C140` devolve a origem do sprite `(baseX,baseY)`,
`obj+1C/20` fornece o centro final, e `obj+2C/30` são as
dimensões do bitmap. Com `scale = cosine[index] >> 8`, o Draw
calcula **ambos os centros e dimensões**:

```text
centerX = baseX + trunc_to_zero((targetX-baseX)*scale/256)
centerY = baseY + trunc_to_zero((targetY-baseY)*scale/256)
width   = trunc_to_zero(bitmapWidth*scale/256)
height  = trunc_to_zero(bitmapHeight*scale/256)
```

A técnica `CDQ; AND EDX,0xFF; ADD EAX,EDX; SAR EAX,8` implementa
a divisão com truncamento para zero, inclusive em deltas negativos.
O C# agora expõe `NativeSelectionTransitionFrame` e
`TransitionFrame` com **centro e tamanho**, sem inferir alinhamento
top-left, sprite final, ordem de composição ou valores da tabela.

Os testes usam propositalmente uma tabela artificial, para validar
a fórmula de *lookup* e a aritmética; ela **não** prova os valores
reais de `DAT_00A52EE8`. Ainda restam a recuperação da própria
tabela, a semântica das variantes, os demais passes Draw e a
integração no cliente. **Badge visual permanece 19/34.**

## 7. A transição possui uma origem distinta para cada família de lista

O bloco de assembly `0043B2A9..0043B2DF` de `0043B280`
seleciona um inteiro por `obj+0x38` (modo de janela) antes de chamar
`0045C140`:

| Modo | Manifest pelo construtor | Descritor de origem do Draw |
| ---: | --- | ---: |
| 1 | fusionlist | `0x0C` |
| 2 | selectlist | `0x0D` |
| 3 | gravelist | `0x0E` |
| 4 | excludelist | `0x0F` |
| Demais | fallback (não assumir mesma âncora) | `-1` |

A chamada real passa `0045C140(obj+0x34, descritor, 0)`.
No x86, ela devolve o par de coordenadas em **EAX/EDX**,
que é usado como origem da interpolação da etapa 6. A assinatura
automática do Ghidra mostra somente um retorno `int`; esse é
um caso em que ler o assembly evita perder a segunda coordenada.

`NativeSelectionWindow.TransitionOriginDescriptor` preserva essa
decisão sem adivinhar o resultado de `0045C140`, pois ele depende de
estado/perspectiva e de dados globais ainda não exportados. Não
presumir que todos os tipos de lista saem da posição (525,300),
nem que os descritores `0x0C..0x0F` sejam índices diretos
de imagens no arquivo DAT.

O método de Draw que compõe as cartas quando `obj+0x14=0` ainda
contém chamadas indiretas e variáveis transitórias sem tipagem
resolvida. Não transportar sua geometria restante por imitação
do overlay atual.

## 8. Extração privada e reprodutível dos dados que faltam ao Draw

O `QueryArchive.java` já aceitava `bytes:VA:N` de até 512 bytes, mas
o invólucro `export_ghidra_visual_bundle.py` não conseguia solicitar
faixas extensas. Ele agora aceita `--memory VA:BYTES`, dividindo
automaticamente em blocos de 512 bytes, validando a quantidade e os
hexadecimais retornados, e incluindo os dumps **somente no ZIP privado**
sob `scratch/`. Também aceita `--instruction-count` (até 1024)
para evitar truncar a análise de uma Draw longa aos 100 primeiros
mnemônicos. A consulta Ghidra segue `-readOnly`, com rollback.

Com o projeto Ghidra já instalado, a extração enfocada é:

```bash
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0043B280 0055DCF0 004524E0 004524F0 004877B0 004877F0 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005DB570:4 --memory 005DCCF8:8 --memory 005DCCB0:8 \
  --output scratch/ghidra-selection-fusion-contract.zip
```

`005DB570:4` é o inteiro global utilizado como X no construtor
da seleção `0043A060`; `005DCCF8:8` e `005DCCB0:8`
capturam as duas constantes referenciadas pelo inicializador x87
em `0055DCF0`. A quantidade/tipo exato dessas constantes deve ser
confirmada pelas instruções FLD na exportação, não apenas pelo
tamanho solicitado.

**Correção essencial, após investigar quem escreve a tabela:**
`00A52EE8:2048` **não é uma maneira confiável de recuperar a
tabela nativa com Ghidra estático**. A rotina `0055DCF0`,
chamada por `0055BFA0`, produz 512 inteiros em `00A52EE8`
até `00A536E8` com `FCOS` e `__ftol`, e marca
`00A536E8=1` ao finalizar. Uma leitura do EXE em repouso pode
mostrar zeros ou memória não inicializada, não os números finais.

Para conferir valores exatos usados pela aplicação, além da
decompilação de `0055DCF0`, será necessária uma **leitura
runtime após sua execução** (Frida/depurador) ou uma reprodução
comprovada do cálculo e arredondamento x87. A exportação estática
agora se restringe à âncora, às constantes e às funções.
A integração só será considerada fiel após confirmar também a
origem calculada por `0045C140`, as variantes dos manifests
e os passes de desenho correspondentes.

Este commit **não executa Ghidra nem contém esses valores**.
Registra o método para obtê-los sem recriar o SQLite completo,
com testes Python offline para validação dos intervalos e do dump
de memória. Continua valendo **19/34 Partial e zero Verified**.

## 9. Proveniência da tabela trigonométrica: produzida pelo executável

Consulta direta ao SQLite de 6.518 decompilações revelou a rotina
`0055DCF0`, que contém o único escritor identificado da região
`00A52EE8..00A536E8`. Sua sequência é:

```text
if (!tableInitialized) {
  for (i = 0; i < 512; i++) {
    table[i] = __ftol(fcos(i * constant_005DCCF8 * constant_005DCCB0));
  }
  tableInitialized = 1;
}
```

A decompilação mostra referências `005DCCF8` e `005DCCB0`
mas não seus valores. O endereço `0055BFA0` chama `0055DCF0`.
A tabela não deve ser tratada como um asset armazenado no DAT
nem como uma constante já inicializada no arquivo PE.

**Evidências necessárias para fechar este caso:** assembly do
inicializador, os operandos x87 e o conteúdo da tabela **após**
o processo inicializá-la, para decidir se um gerador C# produz
os mesmos 512 inteiros. Não confundir `Math.Cos` duplo padrão
com `FCOS` de precisão estendida e regras de `__ftol` sem teste
diferencial. A geometria de `NativeSelectionWindow` permanece
parametrizada pelo `ReadOnlySpan<int>` recebido, sem heurística.
