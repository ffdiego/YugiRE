# Indicadores nativos sobre as cartas — pesquisa assembly-first (22/11/2026)

## Evidências e distinções importantes

Base estática: `joey-knowledge-visual-decoded.sqlite` (6.518 funções) do
`joey_pc.exe` SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Funções consultadas: `004182E0`, `00418EF0`, `004194D0`, `00419B70`,
`0041A070`, `0041A3B0`, `0041A800`, `00413DB0`, `00417F40`,
`004025D0`, `00402650` e `0056DD30`.

**São duas famílias de recursos, com propósitos distintos:**

- `004182E0` carrega **`duel/card/card.txt`** na coleção
  `DAT_005F2FD8`. Todos os Draws estudados aqui consultam essa
  coleção para os índices 4..13. Os índices 0..3 são os quatro
  `card_waku#*.bmp` de foco já mapeados por `NativeCardSelectionFrame`.
- `0041A800` carrega também **`duel/card/icon.txt`**, mas para um
  objeto diferente (vista/lista de cartas). Não associar seus ícones,
  índices ou tamanhos à coleção `DAT_005F2FD8` sem evidência adicional.

`004194D0` executa os desenhos de status **depois** da linha
DEF/separador/ATK. O gate de chamada dos três Draws é:
`(descriptor & 0x8000) != 0` e `(descriptor & 0x3e) < 0x15`.
A condição não garante que todos renderizem um sprite; cada Draw tem
seus próprios filtros.

## Banco de índices do `card.txt`

| Índices | Origem | Papel comprovado / parcial |
| --- | --- | --- |
| 0..3 | `00418EF0` | Quadros de seleção da carta |
| 4 | `00419B70` | Indicador de alteração do atributo (`FUN_00402650`) |
| 5 | `00419B70` | Indicador de alteração do tipo (`FUN_004025D0`) |
| 6 | `0041A070` | Indicador A, flags de campo bits 2..5; animação em estado 3 |
| 7 | `0041A070` | Pass adicional do indicador A durante a saída/transição |
| 8 | `0041A3B0` | Indicador B, flags de campo bits 6..8; animação em estado 4 |
| 9 | `0041A3B0` | Pass adicional do indicador B durante a saída/transição |
| 10..11 | `004194D0` | Pulso alternado de mudança positiva de ATK/DEF |
| 12..13 | `004194D0` | Pulso alternado de mudança negativa de ATK/DEF |

Os índices 4/5 comparam tipo/atributo efetivo com a propriedade base
da carta: tipo modificado no intervalo 1..20, atributo modificado no
intervalo 1..6. O ramo de `00419B70` que trata `card ID 0x48A` e
campos excepcionais **não** está implementado em `NativeCardStatusOverlay`.

Os índices 6/7 e 8/9 têm **mais de um estágio**. O C# reproduz apenas
o seletor de frames, sem inferir o significado das flags de campo ou
construir um objeto gráfico inexistente.

### Detalhes dos contadores extraídos

- `0041A070`: campo `(flags >> 2) & 0xF`. Renderiza se o campo é
  não zero ou se o estado do objeto é 3.
  Estado 3 e contador `t < 256` selecionam no índice 6
  `floor(t/64) + 2` (frames 2, 3, 4, 5).
  Fora desse ramo, seleciona `(globalCounter >> 4) % 3`.
  Se estado 3 e `t >= 256`, acrescenta índice 7 com
  frame `(-t >> 4) & 7`.
- `0041A3B0`: campo `(flags & 0x1C0) >> 6`.
  Renderiza se o campo é não zero ou se o estado é 4.
  Estado 4 e `t < 256` selecionam no índice 8
  `floor(t/64)` (frames 0..3); fora do ramo, frame 0.
  Se estado 4 e `t >= 256`, acrescenta índice 9 com
  frame `(-t >> 4) & 7`.
- `004194D0`: ao surgir delta de ATK/DEF, o índice
  10/11 (positivo) ou 12/13 (negativo) é escolhido por
  `(globalCounter >> 2) & 1`. Esses efeitos são separados
  do atlas de números `duel_fontm`.

`0041A070` também chama `00480690`; `0041A3B0` chama
`004803C0`. O propósito exato dos algarismos e a ligação dos
campos ao `Joey.Core` permanecem em estudo. Não declarar que os
bits representam equipamento, contadores ou nível sem provar.

## Origem dos valores e da animação — nova evidência

O dispatcher de comandos `005B9470` usa três opcodes distintos para
os campos que os Draws 6/7 e 8/9 exibem:

| Comando | Função | Contrato no campo | Estado visual |
| --- | --- | --- | --- |
| `0x5F` | escrita direta | `(valor & 0xF)` no indicador A, se há carta no alvo | sem transição explícita nesse ramo |
| `0x60` | `0040B270` | `min(15, valorAnteriorA + parâmetro)` | `obj+0x18 = 3`; `obj+0x1C = 0x200` |
| `0x69` | `0040B370` | se B anterior é zero ou pedido é menor, usar pedido; no fim `& 7` | `obj+0x18 = 4`; `obj+0x1C = 0x200` |

**O segundo comportamento não é saturação em 7:** é uma operação de
mínimo/valor inicial **seguida de máscara de três bits**. Isso importa
para testes com parâmetros maiores que 7, ainda que o jogo normalmente
possa restringir os valores na origem do comando.

`0040B270` e `0040B370` também verificam que há carta na zona e que
`0041CEE0(lado,zona,0)` encontrou o objeto visual antes de mudar
estado/contador; esses pré-requisitos **não** são delegados
às funções puras de `NativeCardStatusCounters`.

### Tempo: a sequência corre de 512 até zero, não ao contrário

`00418B60` confirma que, **com a carta estabilizada**
(`obj+0x14 == 0x100`), estados de animação 1..5 subtraem
`0x10` de `obj+0x1C` a cada update. Ao chegar a zero,
o estado também é zerado. Com a inicialização 512 dos
comandos `0x60`/`0x69`, são **32 updates nativos**.

Para os indicadores A/B isso significa:

- **Primeira metade:** contadores 496, 480 ... 256. O Draw
  inclui índice 7 (A) ou 9 (B), cuja sequência de 8 quadros
  é selecionada por `(-contador >> 4) & 7`.
- **Segunda metade:** 240, 224 ... 16. Os índices 6 e 8
  usam os quadros principais de estado 3 ou 4.
- **Fim:** após o 32º update, contador e estado zeram.
  O Draw já não usa o ramo de transição.

`NativeCardStatusTransition` reproduz os passos **sem modificar**
`Joey.Core`. O modelo respeita a condição de carta estabilizada;
ele ainda não é agendado para desenhar no cliente sem recursos reais.

## Geometria parcial com coordenadas sustentadas

Outra distinção: `00418560` inicializa
`DAT_005F3008` com a **altura da miniatura da carta**
da coleção `mini/list_card.txt`. Essa altura, e não a altura
de `duel_fontm`, aparece nas fórmulas de `00419B70`,
`0041A070` e `0041A3B0`.

Definindo `Y = obj+0x34`, `H = altura da miniatura`, e
`S = +1` se o bit de lado do descriptor coincide com
`DAT_00A54E5C & 1` (`-1` caso contrário), as âncoras são:

- **Identidade alterada, `00419B70`:** `Y - (H*S)/2`.
- **Indicadores A e B, `0041A070`/`0041A3B0`:**
  `Y + S*(H/2 - 5)`.

`NativeCardStatusGeometry.FromNative` codifica essas expressões.
`SourceFrame` calcula o recorte horizontal a partir da largura do
atlas e do número de colunas **informado**, sem pressupor o número
de quadros de um bitmap ainda não inspecionado.

Os **X absolutos**, ajustes posteriores de Y, dimensões finais,
rects de origem, blending e o sentido visual próprio/oponente
precisam da inspeção assembly/DAT. O Ghidra mantém
`unaff_EBX`/`unaff_EDI` em expressões com largura de sprite e
não dá para transformá-las em constantes confiáveis. Portanto
**não criar um Draw raylib de placeholder**.

O modelo de estados, geometry e os seletores de frame têm testes
sintéticos de limites, recortes e contagem regressiva. A cobertura
visual continua **19/34 Partial, 0 Verified**.

## Numeração amarela — contrato adicional

Os dois status usam `00417F40(8)` → `j/duel/font/duel_fontm_y.bmp` (fonte de **70×10**, **10 glifos 7×10**, comprovada pelos metadados originais do DAT). `0041A070` centraliza o número via `00480690`; `0041A3B0` desenha diretamente pelo lado direito via `004803C0`. O recorte e a ordem da direita para a esquerda foram codificados em `NativeCardStatusDigits`, com testes. Os offsets horizontais externos mantêm `unaff_EBX` e `iStack_6c` explícitos e **não** autorizam uma posição final. Ver [contrato dos dígitos](native-card-status-digits-2026-11-22.md).

## Incremento no projeto (sem fingir uma Draw implementada)

`NativeCardStatusOverlay.cs`, em `Joey.LookAndFeel`, contém índices
e seleção dos quadros; `NativeCardStatusCounters` modela os comandos
0x5F/0x60/0x69, `NativeCardStatusTransition` a contagem regressiva
512→0, e `NativeCardStatusGeometry` as âncoras verticais e os recortes
com colunas explícitas. Testes cobrem os gates de tipo/atributo, as
máscaras 4/3 bits, as duas fases do movimento e as fontes numéricas.

`scripts/summarize_native_card_status_dat.py` usa o resultado já
existente de `--inspect-visual-dat`, verifica as âncoras do `card.txt`
nos índices 0..3 e gera **apenas metadados** dos índices 4..13
(nomes, largura, altura, presença). Não adivinha o nome de um sprite.
Se as âncoras divergem, falha com erro; se a imagem correspondente
não aparece nos metadados, registra a lacuna.

No Windows com `data.dat` original:

```powershell
dotnet run --project src/Joey.App -- .\passion\data.dat --inspect-visual-dat
python scripts/summarize_native_card_status_dat.py
```

Resultado privado em
`scratch/visual-review/card-status-manifests.json`.
Não commitar conteúdo proprietário, manifest ou imagens.

O disassembly preciso de `00419B70`, `0041A070`,
`0041A3B0`, `004194D0`, `0041A800` e o dispatcher é
outra coleta independente: `scripts/pending/04-card-status-ghidra.sh`.
Ela é necessária para resolver x/y (Ghidra tem registradores
`unaff_*`), retângulos-fonte, blend, clipping e ordem de composição,
antes de desenhar qualquer um desses novos indicadores.

**Resultado:** família identificada e parâmetros de timeline isolados.
Os Draws novos continuam **Pending**; inventário de cobertura
permanece `19/34 Partial, 0 Verified`. Os outros indicadores de
`duel/card/icon.txt` continuam fora desse inventário.


## 23/11 — bundle de assembly recebido

As 17 funções e 12 janelas exatas foram recuperadas no bundle privado `ghidra-card-status-contract.zip`. As fórmulas de X não dependem mais dos registradores indefinidos do decompilador: largura de miniatura em `DAT_005F3004`, largura de quadro pelo divisor de colunas do manifesto, deslocamentos específicos A/B e espelhamento dos ícones de tipo/atributo. A segunda rotina tem gate adicional `(descriptor & 0x3e) < 0x0a`. Ver [contrato geométrico e novos testes](native-card-status-digits-2026-11-22.md). Esta evidência substitui as hipóteses/lacunas de X acima. Falta correlacionar os metadados reais do DAT e os eventos semânticos do Core; não há novo Draw integrado.
