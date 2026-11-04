# Como o Joey escolhe monstros na mão: seletores e modo (2026-11-04)

**Fonte:** `ai-hand-selectors.txt`, 457 linhas fornecidas
pelo operador, extração Ghidra para EXE SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Cruzar com `docs/03-functions.md` (acessores de ATK/DEF)
e `docs/evidence/card-callback-catalog.json` (nomes e IDs de
recurso). Nenhum algoritmo foi executado dinamicamente nesta etapa.

## Visão geral

```text
Caminho 0x005B73D2
  FUN_005B1360(side,0) -> se -1, FUN_005B1360(side,-1)
  -> FUN_005B6FF0(side,handIndex,0)

Caminhos 0x005B7534 / 0x005B766D
  FUN_005B16C0(side) -> handIndex (ou -1)
  -> FUN_005AE270(side,cardInternal,0,0) -> modeSelector
  -> FUN_005B6FF0(side,handIndex,modeSelector)
```

No caminho `0x005B7534`, o retorno de `FUN_005AE270`
**deve ser 0** para invocar o executor, que recebe o modo
0. No caminho `0x005B766D`, o retorno da função
é passado diretamente, permitindo decidir entre
summon face-up (`modeSelector=0`) e set face-down
(`modeSelector!=0`) dentro de `FUN_005B6FF0`.
Ambos os caminhos verificam `playerFlags & 0x20000` antes
da seleção e **ambos verificam se o índice retornado é negativo**
(guarda `TEST ESI; JL` em 0x005B74E5/74E7 e
`CMP ESI,-1; JLE` em 0x005B7628/762B).

Essa observação **corrige** a ressalva anterior no documento
dos callers: o segundo caminho tem sim guarda de índice
inválido e o valor de ESI no caminho intermediário vem
de `FUN_005B16C0` (0x005B74DB). Os dois callsites
compartilham o mesmo seletor, mas possuem critérios de
execução diferentes.

## FUN_005B1360(side,pass): avaliação e seleção com fallback

O caller primeiro pede `pass=0` e, se falhar
(`handIndex=-1`), repete com `pass=-1`.

- Percorre a mão atual, filtrando `FUN_005AAB90(side,card)`
  (elegibilidade), `FUN_005AE3E0` e `FUN_005AE220`.
- Para classes de nível 1..4, o ramo aceita qualquer
  `pass != 1`; portanto tanto 0 quanto -1.
- Para classes 5..6, **só continua quando `pass != 0`**
  e `FUN_005AECE0` encontra um candidato a tributo.
- Para classes maiores, também exige `pass != 0`,
  e consulta `FUN_005AECE0` duas vezes.
- Para cada candidato elegível nesse ramo, executa
  `FUN_005B08E0`, `FUN_005B09C0`,
  `FUN_005B1610`, `FUN_0056DD30`,
  `FUN_005B0A50`, `FUN_00569D50` e
  `FUN_005B0950`. O fluxo compara valores de estado
  (`local_1c`, `local_18` e máscaras em
  `DAT_00A579AA/AC`) para atualizar o melhor
  índice encontrado.

**Interpretação:** o primeiro passe evita candidatos
que exigem tributos; o fallback permite considerá-los.
A série de helpers pode ser uma **avaliação de cenário
simulado**, mas não se demonstrou se ela copia/restaura
o campo ou qual métrica cada helper produz. Em especial,
o pseudocódigo lê `local_8 > 899` sem mostrar
inicialização de `local_8` no próprio corpo; isso pode
ser efeito de *out-buffer* tratado incorretamente pelo
decompilador. **Não** reproduzir literalmente a
comparação sem conferir a escrita desse local em assembly
e o contrato de `FUN_0056DD30`.

Não há chamada explícita a RNG observada neste corpo.
Isso **não prova determinismo global**: os helpers
ainda podem usar aleatoriedade ou estado mutável.

## FUN_005B16C0(side): prioridades específicas e força

### 1. Grupo com prioridade condicional por identificador

Sob `FUN_005AE9C0(side,-1,1) == -1` e
`FUN_0056A000(side) > 0`, a função testa
regras específicas e retorna imediatamente
o índice obtido de `FUN_0056D340(side,resourceId)`
quando ele não é negativo.

O catálogo do projeto identifica os IDs de recurso
observados com estes nomes (a semântica exata de
`FUN_0056D340` ainda depende de decompilação):

| Recurso (hex) | Decimal | Carta | Pré-condição visível |
| --- | ---: | --- | --- |
| `0x1F4` | 500 | Man-Eater Bug | `FUN_00569D50(opponent)>0` |
| `0x21C` | 540 | Hane-Hane | mesma condição |
| `0x1AB` | 427 | Magician of Faith | `FUN_0056A8E0(side,0x16)>0` |
| `0x65` | 101 | Mask of Darkness | `FUN_0056A8E0(side,0x15)>0` |
| `0x21B` | 539 | Skelengel | mão própria pequena ou muito menor que a do rival |
| `0x24E` | 590 | Morphing Jar | mesma condição |
| `0x246` | 582 | Weather Report | `FUN_005699A0(opponent,0x15B)>0` |
| `0x27` | 39 | Dragon Piper | `FUN_005699A0(opponent,0x148)>0` |
| `0x109` | 265 | Princess of Tsurugi | `FUN_0056A320(opponent,0,0,1)>1` |
| `0x249` | 585 | Greenkappa | `FUN_0056AA50(opponent,0)>1` |
| `0xDF` | 223 | Trap Master | `FUN_0056AA50(opponent,0)>0` |
| `0x48B` | 1163 | Morphing Jar #2 | `FUN_005AFE90(side)<0` |
| `0x452` | 1106 | Cyber Jar | mesma condição |

A tabela segue **ordem de prioridade estática** da
decompilação: se um par é candidato, verifica o
primeiro ID antes do segundo. Os nomes reforçam
a hipótese de uma **política especial para FLIP monsters**,
mas ainda não identificamos todos os efeitos dos filtros
e não provamos que `FUN_0056D340` simplesmente procura
a carta na mão sem efeitos colaterais.

### 2. Passagem geral por atributos

Depois das prioridades especiais, a função:

1. Percorre as cinco posições adversárias e consulta
   `FUN_0056F690(opponent,slot)`, documentada como
   **ATK calculado**, mantendo o maior retorno.
2. Obtém `local_c = FUN_005AFFD0(side,side,-1,1,0)`.
   O significado do valor precisa de estudo próprio.
3. Percorre cada carta da mão, consulta
   `FUN_00402800(card)` (**DEF base**) e
   `FUN_004027B0(card)` (**ATK base**), tomando
   `uVar10 = max(DEF,ATK)`.
4. Filtra elegibilidade `FUN_005AAB90`, a busca
   `FUN_005AE3E0` e a disponibilidade de zona.
   Para cartas acima da classe 4, a própria função
   consulta `FUN_005AEE70(side,card,0)`, que
   verifica a possibilidade de tributos.
5. Um candidato satisfaz a condição principal
   quando `uVar10 > local_c` e `uVar10 >=
   maxOpponentATK`, **ou** há um caso especial de
   nível alto autorizado por `FUN_005AE9C0`.
   Ao selecionar, atualiza `local_c=uVar10` e
   `bestHandIndex`, que no pseudocódigo são
   `local_c` e `local_8` respectivamente.

O critério de comparação por força é, portanto,
**confirmado no código**, embora `FUN_005AFFD0`
e algumas guardas ainda não estejam compreendidas.
A direção das comparações é significativa: exige
superar o limiar anterior e ao menos empatar com
o ATK calculado máximo do rival.

### 3. Fallback de monstro comum

Se não houver vencedor no passe anterior e existir
zona elegível, `FUN_005B3D30(side)` é consultada.
Então uma segunda varredura da mão retorna
o **primeiro** monstro que passar nas condições
de classe `<5`, `FUN_005AAB90 != 0` e
`FUN_005AE3E0(...)=0`, com parâmetro dependente
do retorno de `FUN_005B3D30`.

Se nenhum ramo encontrar candidato, retorna
`0xFFFFFFFF` (`-1`).

Esse fallback pode explicar comportamento aparentemente
simples, mas é apenas uma rota de decisão em um sistema
com múltiplos filtros; não concluir que a IA sempre
invoca o primeiro monstro da mão.

## FUN_005AE270: seletor contextual de face-up vs set

Os callsites fornecem `FUN_005AE270(side,card,0,0)`.
A função consulta `FUN_005699C0(0x47F)`, recurso
identificado como **Light of Intervention** no catálogo.
Se houver a condição indicada pelo retorno,
`iVar4` começa como 1 em vez de receber `param_3`;
em seguida, o fluxo distingue vários IDs de
cartas especiais e chega a
`FUN_005AE0A0(side, ...)`, que pode devolver
um modo ou `-1` (nesse caso usa `param_4`).

Isso demonstra que a postura face-up/face-down
depende de **regras e efeitos ativos**, não só
de um dado aleatório ou de simples ATK/DEF.
A lista observada na função mistura cartas com
efeitos especiais; sem mapear `FUN_005AE0A0`,
não atribuir um significado único a todos os IDs.
E, em `FUN_005B6FF0`, a própria
`FUN_00570A70` ainda pode forçar set.

## Estado do entendimento

| Questão | Status |
| --- | --- |
| Seleção de candidato da mão | Confirmada em dois helpers |
| Prioridades por ID específico | Confirmadas estaticamente |
| Comparação de ATK/DEF e ATK do adversário | Confirmada |
| Elegibilidade, tributos e zonas | Confirmadas como filtros |
| Modos summon vs set vindos de efeito/carta | Fortemente sustentados |
| Heurística completa e significado dos limiares | Parcial |
| Uso de RNG na IA | Não verificado |
| Escolha aleatória do deck da CPU | Não localizada |
| Modo demo do menu (replay vs IA) | Não localizado |

## Próxima extração sugerida (PowerShell)

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-decision-helpers.txt `
  -Addresses @(
    '0056d340',
    '005ae0a0',
    '005affd0',
    '005b1610',
    'ins:005b14c4:70'
  )
```

Priorizar `FUN_0056D340` para confirmar o domínio dos
IDs das prioridades; `FUN_005AE0A0` para a decisão
sobre set/summon; `FUN_005B1610` para entender
o valor comparado no primeiro seletor. A janela
`ins:005b14c4:70` deve esclarecer o aparente
`local_8 > 899` e possíveis argumentos de saída.

O estudo é apenas estático: **não habilitar autoplay
ou hooks ativos sem mais testes de estado/filas**.
