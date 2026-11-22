# YugiRE — lote de métodos virtuais recuperados (22/11/2026)

## Origem e verificação

- Anexo do usuário: `ghidra-visual-virtual-methods.zip`.
- SHA-256 do executável alvo: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- `manifest.json`: `selected=15`, `decompiled=15`, `missing=[]`.
- Todos os 15 vieram de tentativas `tempfunc:`. Os três lotes iniciais (sem criação transitória) encontraram zero funções por esses endereços.
- **18 slots distintos** (`+0/+4/+8/+C`) das sete vtables apontam para esses **15 entrypoints distintos**, incluindo compartilhamentos entre objetos.
- O Ghidra foi executado em `-readOnly` e `QueryArchive.java` encerrou as transações com rollback; portanto, os métodos não passaram a ser funções persistentes no projeto Ghidra.
- O pseudocódigo decompilado não equivale a comportamento verificado em execução. Algumas rotinas contêm registradores `unaff_*`, ponteiros não tipados e propagação de tipos incompleta.

## Correção estrutural crítica: a forma da vtable varia

**Não chamar indiscriminadamente slots `+0x08` de Update e `+0x0C` de Draw.** A implementação concreta demonstra que algumas vtables são de objetos auxiliares sem o mesmo contrato da lista de apresentações.

| Classe/vtable | Slot | VA | Papel evidenciado |
| --- | --- | --- | --- |
| Seleção `005DABD8` | `+8` | `0043B0F0` | navegação e Update de cinco cartões |
| Seleção `005DABD8` | `+C` | `0043B280` | desenho da lista e transformação/saída |
| Botão `005DACC4` | `+4` | `0041C030` | grava `obj+0x14`/`obj+0x1c` com dois valores recebidos |
| Botão `005DACC4` | `+8` | `0041C050` | copia `obj+0x14`/`obj+0x1c` para dois parâmetros de saída |
| Botão `005DACC4` | `+C` | `0043E5B0` | lê recurso em `DAT_005F302C` e ajusta posição em `obj+0x18`/`obj+0x20` |
| Corrente `005DABA8` | `+8` | `0041F830` | incrementa contador (+4) até gate |
| Corrente `005DABA8` | `+C` | `0041F870` | renderização, inclusive condições e números |
| Corrente desativada `005DB374` | `+8` | `004531A0` | soma 1 ao contador e desativa quando alcança `0x18` |
| Corrente desativada `005DB374` | `+C` | `004531C0` | quadro `(contador / 2) % 6`; variante por bit 0x40 do descritor |
| Efeito persistente `005DA44C` | `+8` | `0040E010` | contador `(+8)&0xFF`; reseta se muda o identificador global |
| Efeito persistente `005DA44C` | `+C` | `0040E260` | enumera estados/cartas e compõe indicadores contextuais |
| Layerfont A `005DB3C4` | `+8` | `004481F0` | função compartilhada **vazia**, não Update |
| Layerfont A `005DB3C4` | `+C` | `00455FF0` | incrementa contador (+4)&255 **e desenha no mesmo método** |
| Layerfont B `005DB3D4` | `+8` | `004481F0` | função compartilhada vazia |
| Layerfont B `005DB3D4` | `+C` | `00456360` | atualiza flag/intensidade (+4/-4) **e desenha condicionalmente** |

Os slots `+4` também incluem uma rotina comum `00403080` (`return *(obj+4)`) e `0048D9A0` (`return 0`), sem evidências suficientes para nomeá-los genericamente como destrutores/Update.

## Seleção de cartas: prioridade de implementação

- `0043B0F0`: chama `0043E280`, verifica `obj+0x14==0` e `obj+0x19==0`, incrementa `obj+0x3c`, consulta os estados de entrada por `005664A0` com códigos 1, 2, 3, 4, modifica o índice `obj+0x1b8`, usa `obj+0x1fc` para habilitar rolagem estendida e atualiza cinco ponteiros iniciando em `obj+0x15c`. Reposiciona os objetos com `0043ABA0`, usando pares coordenados de X/Y.
- `0043B280`: quando `obj+0x14==0`, compõe os objetos de lista e textos/atributos, iterando por cinco slots; existem caminhos específicos para flags em `obj+0x38`. Quando a condição não é satisfeita, há uma interpolação baseada na tabela trigonométrica `DAT_00A52EE8`, com deslocamento e escala. **A decompilação reporta `WARNING: Type propagation algorithm not settling`, então não devemos copiar seus registradores `unaff_*` como coordenadas exatas.**
- A UI atual em `src/Joey.App/CardSelectionScreen.cs` ainda dispõe dez cartas em 2×5; o nativo gera até cinco objetos. Projetar a UI reutilizável no `LookAndFeel` e preservar seleção/elegibilidade no `Joey.Core`.
- O Draw recuperado `0043B280` é uma rotina **nova** para o inventário de cobertura e deve entrar como `Pending`, não como `Partial` só porque existe um overlay provisório.

## Outras famílias

- Corrente: `0041F830` incrementa `obj+0x10` por 4; se `obj+0x18==0` e contador já passou de `0x7F`, o passo fica zero. Ativo enquanto `obj+0x10 < 0xC0`. `0041F810` implementa um predicado adicional sobre `obj+4`, `obj+0x18` e contador igual a `0x80`.
- Corrente desativada: `004531A0` incrementa um contador por Update e desativa em `t>=24`; `004531C0` escolhe `(t/2)%6` e consulta um bit do descritor para decidir um pass visual diferente. Ainda não conhecemos exatamente a escolha dos YGAs/BMPs sem ler `chain/disable.txt`.
- Efeito persistente: `0040E010` move contador de 8 em 8 módulo 256 e reinicia quando muda `DAT_005F2FDC`. O Draw `0040E260` examina o descritor retornado por `00418B50`, estado de campo dos dois lados e efeitos, chamando `0040E1F0` com identificadores; cuidado: não equivale a um único overlay estático.
- Layerfont A: `00455FF0` soma 4 módulo 256 e usa a tabela `DAT_00A52EE8` para intensidades fora da janela central `[0x40,0xC0]`; âncora literal visível `0x314` e `0x24c` (788,588), subtraindo dimensões da textura. O tamanho só poderá ser confirmado com o DAT.
- Layerfont B: `00456360` condiciona a exibição por `005BDAD0` e `0044BA10`, atualiza valor entre 0 e 256 em incrementos de 4 e desenha apenas quando `valor>0xE0`. Usa uma interpolação na janela final. O contexto exato (fala, estado ou tela) ainda não está estabelecido.

## Incremento do inventário visual

São três **Draw entrypoints** novos comprovados em relação aos 31 previamente catalogados: seleção `0043B280` e layerfonts `00455FF0`/`00456360`. `0043E5B0` **não é Draw**. Atualizando o inventário: **19/34 (55,88%) parcialmente implementados, 0/34 Verified**. A porcentagem cai pelo aumento correto do denominador, não por regressão do cliente.

## Integração segura com SQLite

O script `scripts/import_visual_virtual_bundle.py` cria **outro SQLite**, preservando o original e mantendo as 15 decompilações transientes numa tabela separada (`visual_transient_decompilations`) para não fingir que passaram a existir no índice persistente `functions`.

```bash
python3 scripts/import_visual_virtual_bundle.py \
  --source scratch/joey-knowledge-visual-ghidra.sqlite \
  --bundle scratch/ghidra-visual-virtual-methods.zip \
  --output scratch/joey-knowledge-visual-decoded.sqlite \
  --summary scratch/visual-transient-decomp-audit.json
```

Consultas:

```sql
SELECT v.subsystem, m.slot_index, m.role, m.target_address,
       d.code, m.resolution
FROM visual_virtual_methods m
JOIN visual_vtables v ON v.address = m.vtable_address
LEFT JOIN visual_transient_decompilations d ON d.address = m.target_address
WHERE v.subsystem = 'duel.selection'
ORDER BY m.slot_index;

SELECT * FROM visual_virtual_decompilation_coverage
ORDER BY subsystem, slot_index;
```

O script valida a identidade do executável, verifica conflitos entre os ponteiros da base e as referências às vtables, não modifica a base original e tem testes unitários com dados sintéticos.

## Próximas questões

1. Ler `selectlist*.txt`, `gravelist*.txt` e variantes reais do DAT para geometria e ordem de recursos. Medir a UI no Joey em 800×600 e comparar estado/fase idênticos.
2. Investigar `0043ABA0`, `0043ABF0`, `0043AB10`, `0043AB40`, `0043AB70` para dados dos cinco cartões e scroll.
3. Rastrear `00455FF0` e `00456360` até suas chamadas/estados que os habilitam para nomear corretamente os dois modos de layerfont.
4. Não promover `Verified` sem capturas/frames pareados. Não versionar o ZIP com decompilações, o SQLite, o EXE nem o DAT.


## Janela deslizante e geometria da seleção (investigação 22/11/2026)

Fonte: SQLite das 6.518 funções e decompilações transitórias do ZIP Ghidra
do usuário. Sem DAT local/captura, esta seção comprova fórmulas e fluxos,
**não** fidelidade de pixels nem os nomes definitivos de cada controle.

| Função | Papel sustentado pelo pseudocódigo |
| --- | --- |
| `0043AB10` | Conta cartas: `0040CAE0` nos modos comuns; tamanho do vetor DWORD nos modos `0x2A..0x2E` |
| `0043AB40` | Lê carta por índice: `0040CAF0(index)` ou vetor filtrado |
| `0043AB70` | Retorna flags por `0040CB50` ou zero nos modos filtrados |
| `0043ABA0` | Calcula X/Y do slot visível |
| `0043ABF0` | Usa a geometria e chama `0047FCB0`; argumentos parcialmente perdidos na decompilação |

A fórmula X de `0043ABA0` é `anchorX + 64 * (slot - 2)`, para
slots 0..4. Os deltas são -128, -64, 0, +64, +128. Em Y, a função
calcula `anchorY + resourceY(variant) - height/2`, onde
resourceY(variant) = *(int*)(*(int*)(obj+0x48)+4+8*variant)
e `variant` é o byte em `obj+0x1FC`. **Ainda não fixamos**
âncoras e alturas absolutas sem os manifests.

O Update `0043B0F0` mantém o slot selecionado em `obj+0x1B8`
e o offset de scroll em `obj+0x214`. Se a lista passa de cinco
itens (`obj+0x1FC` não zero), avançar a partir do slot 4 aumenta
o offset em uma unidade, até `total-5`. Retroceder a partir do
slot 0 reduz o offset até zero. Para 7 itens, a sequência dos estados
(slot, scroll) é `(0,0) → (1,0) → (2,0) → (3,0) → (4,0) → (4,1) → (4,2)`.
**É janela deslizante, não paginação.**

Os eventos `005664A0(3)` e `005664A0(4)` controlam
movimento para trás/frente na rotina; não foram mapeados para teclas físicas.
Os flags de `0043AB70` incluem o bit `0x800` no modo comum,
que também é testado em `0043B280`. Não inferir ícones ou regras
sem confirmar a aparência e as variantes no executável.

### Aquisição dos recursos nativos

O índice do DAT no repositório contém **51 recursos** sob
`j\duel\list\` (14 manifests .txt) e **14** sob
`j\duel\list_icon\` (um manifest .txt). Para gerar um relatório
reduzido sem copiar texturas:

~~~bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_selection_dat.py
~~~

Compartilhar somente `scratch/visual-review/selection-manifests.json`
após inspecionar seu conteúdo. Contém texto dos manifests e dimensões de
imagens, não as imagens. O passo seguinte é confrontar os recortes e a
posição real em 800x600 com a tela nativa. Badge permanece 19/34.
