# IA do jogador: invocação legal, rejeitada pelo seletor (2026-11-05 UTC)

Fontes: `commands-20261105-035820-056432-pid5596.jsonl` (fornecido
pelo operador), `state`, `ai check 1` e `summon check 1 2`
reportados na mesma sessão do console. EXE: SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Separa-se **observação dinâmica** de **hipóteses estratégicas**.

## Estado do duelo observado

| Propriedade | Lado 0 (CPU) | Lado 1 (jogador) |
| --- | --- | --- |
| LP | 9000 | 8500 |
| Mão | 4 | 6 |
| Deck | 34 | 33 |
| Cemitério | 2 | 1 |
| Campo de monstros | slot 0: índice interno 522 | cinco slots vazios |
| Zonas adicionais 5..10 | vazias | vazias |

A mão reportada durante a seleção continha:
- slot 0: índice interno **369**, não monstro — catálogo: **Crush Card**;
- slot 1: índice interno **272**, não monstro — catálogo: **Torrential Tribute**;
- slot 2: índice interno **221**, monstro classe 4 — **Mystic Tomato**;
- slot 3: índice interno **221**, monstro classe 4 — **Mystic Tomato**;
- slot 4: índice interno **467**, não monstro — **Mirror Force**;
- slot 5: índice interno **273**, monstro classe 4 — **Wall of Illusion**.

Nomes de cartas foram cruzados com `docs/evidence/card-callback-catalog.json`
por `identities[].index`; a identidade do índice **522**
não foi estabelecida nessa fonte e permanece sem nome neste relatório.

O teste de colocação `summon check 1 2` foi aprovado:
`allowed=true`, `eligibility=17` (`0x11`),
`eligibilityMask=1`, `selectedSlot=0`,
`playerFlags=0`, `normalSummonUsed=false`,
`cardIndex=221`, `levelClass=4`.
**Isso comprova uma opção legal de summon conforme o preflight,
não uma obrigação da IA de escolhê-la.**

## JSONL: dois passes realmente executados

O arquivo contém **7 linhas: 1 metadata + 6 eventos**
(`seen=6`, `retained=6`, `overwritten=0`),
todos os eventos com `kind=ai_console`,
`origin=yugire_console_native_call`,
`requestId=2`, `side=1`,
`checkOnly=true`, `threadId=10120`.
A ordem observada:

| Seq | Phase | Pass | Resultado |
| --- | --- | --- | --- |
| 1 | `start` | — | `handCountAtRequest=6` |
| 2 | `select_enter` | 0 | início da avaliação |
| 3 | `select_leave` | 0 | `selectedHandIndex=-1` |
| 4 | `select_enter` | -1 | segundo passe |
| 5 | `select_leave` | -1 | `selectedHandIndex=-1` |
| 6 | `decision` | — | `allowed=false`, `candidateCount=6` |

Todos os registros têm `timestampMs=1793851051663`
(resolução de milissegundos, sem inferir duração exata).
**Não** há `ai_probe` com eventos de combate ou buffers
de atributos nesse trace. Os eventos `ai_console` são
gravados diretamente pelo nosso adaptador, não por um
`Interceptor.attach` aninhado.

O marcador `candidateCount=6` é o número de cartas
classificadas na mão pelo adaptador após uma seleção
sem resultado. **Não é o número de candidatos
avaliados como legais ou simulados pela IA**.

## Comparação com invocação bem-sucedida

Em outro estado do duelo, reportado pelo operador,
`ai check 1` retornou `normalPass0=1`,
`selectedInternalIndex=221`, `allowed=true`,
e `ai step 1` conseguiu invocar **Mystic Tomato**.
Logo, esta versão do adaptador pode selecionar
e executar a invocação no lado 1, mas o seletor
também retorna `-1` em situações em que ao menos
uma invocação normal é permitida.

**Não é possível isolar a causa da diferença**
comparando esses dois relatos: posição/identidade
e atributos do monstro oponente na partida
bem-sucedida não foram capturados no mesmo
formato de estado; podem existir outros globals
distintos além do campo visível.

## Hipóteses, não conclusões

1. **Filtro estratégico:** `FUN_005B1360` compara
   score baseline (`FUN_005B08B0`) com uma
   simulação (`FUN_005B0A50` após
   `FUN_005B1610`). Há ainda um teste de
   valor `>=900` no assembly próximo de
   `0x005B153F`, mas seu significado exato
   ainda não está fechado; não afirmar
   que seja diretamente ATK ou limiar de dano.
   Monstros legalmente invocáveis podem ser
   descartados como opções ruins.
2. **Gate de eligibilidade interno:**
   `FUN_005AAB90`, `FUN_005AE3E0` ou
   `FUN_005AE220` podem rejeitar
   candidatos que passam no preflight externo
   para summon.
3. **Contexto do controlador:** ao chamar a
   função da CPU no lado 1, a preparação de
   globais ou o momento dentro de
   `FUN_005BC700` pode ser diferente do
   caminho original.

Nenhuma dessas hipóteses foi comprovada
pelos seis eventos. **Não concluir que
Mystic Tomato foi rejeitado por ATK
insuficiente ou que o monstro 522 é mais forte.**

## Refinamento após extração de gates

O relatório `ai-selection-rejection-gates.txt`
confirmou a ordem dos filtros
`FUN_005AAB90` (máscara),
`FUN_005AE3E0` (restrição de recurso)
e `FUN_005AE220` (vínculos), seguida
de simulação, teste numérico `>=900`,
bit de slot em `DAT_00A579AC` e
comparação do score com baseline.

Mystic Tomato (`0x463`) e Wall of
Illusion (`0x419`) passam pelo
ramo padrão não bloqueado do filtro
`FUN_005AE3E0(side,2,resource)`
na decompilação. O retorno de
`FUN_005AE220` ainda depende de
`FUN_00567620`.

O valor comparado a `900` é
**provavelmente ATK computado**,
porque `local_c` recebe o struct
de 12 bytes e `local_8` pode
sobrepor `out+4`; falta validar o
endereço no assembly do caller.
Ver [estudo detalhado dos gates](ai-selection-rejection-gates-2026-11-05.md).

**Próxima consulta:**

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-selection-score-offsets.txt `
  -Addresses @(
    'ins:005b1360:125',
    'ins:005b14b0:145',
    '00567620',
    'ins:005b1540:90'
  )
```

Também vale comparar o caminho do seletor
alternativo `FUN_005B16C0`, mas não
adicioná-lo como fallback executável
sem validar seu contrato e contexto.
