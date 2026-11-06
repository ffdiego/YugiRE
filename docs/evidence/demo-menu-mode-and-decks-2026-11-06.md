# Demo do Joey: modo 7, dois decks de origem e fábrica de cenas

**Data da captura:** 2026-11-06 UTC.
**Fontes:** `demo-menu-trigger.txt` e
`commands-20261106-042344-838041-pid11344.jsonl`
enviados pelo operador. EXE: SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Observação passiva, sem DLL e sem invocar ações nativas.

## Dados dinâmicos da demo

Arquivo JSONL: **1561 linhas** (1 metadata + 1560 eventos);
`seen=1560`, `retained=1560`, `overwritten=0`,
`capacity=20000`; duração **197,534 s**.

| kind | eventos |
| --- | ---: |
| `ai_probe` | 874 |
| `command` | 639 |
| `demo_executor` | 34 (17 entradas, 17 saídas) |
| `demo_init` | 12 |
| `marker` | 1 |

**21 turnos** detectados pelo comando mascarado
`0x0001`, alternando os lados
`1,0,1,0,...,1`. Exatamente
20 comandos `0x0002` e 20
`0x0003`; último comando
`0x0004` em seq 1560.
Isso comprova avanço automático
até um término, sem atribuir vencedor.

A captura mostra `sceneMode=7`
(`DAT_005F3294`) já na entrada de
`FUN_005BFEB0` (seq 2),
com `duelModeWord=0`.
Na entrada de `FUN_005B9E50`
(seq 3), `duelModeWord=14`
(`0x0000000E`, `deckSideBit=0`).
Portanto o bootstrap atribui flags
antes de preparar o estado; ainda
não sabemos o significado de
cada bit. O modo de cena 7
aparece **numa demo efetivamente observada**.

## De onde vieram os dois decks

Logo no `duel_entry/enter` (seq 2)
as duas estruturas preexistentes
já têm conteúdo válido:

| source slot | Main | Seção +0xA0 | Seção +0xBE | Assinatura main (16 hex SHA256) |
| --- | ---: | ---: | ---: | --- |
| 0 | **43** | 6 | 3 | `cded4cf368272b89` |
| 1 | **41** | 6 | 3 | `65d77013fdeb9210` |

A assinatura é SHA256 dos índices
internos da lista principal,
ordenados e separados por vírgula.

Com `deckSideBit=0`, o algoritmo
`sourceSlot = side ^ deckSideBit`
mapeou source 0 → side 0 e
source 1 → side 1. Na
`FUN_00569380`, ambas as listas
principais foram copiadas **na
mesma ordem e integralmente**:
- side 0: 43 cartões, mesmo índice por posição
  que source slot 0;
- side 1: 41 cartões, mesmo índice por posição
  que source slot 1.

Depois, `FUN_00568D80` modificou a
ordem, mas **preservou exatamente o
multiconjunto** de índices por side.
Isso valida os novos hooks de fonte
de deck no processo real.

O deck de **43** tem a mesma
assinatura das duas demonstrações
anteriores e a mesma sequência
pré-shuffle. É evidência de
composição persistente, **não** de
que necessariamente seja um deck
salvo pelo usuário. O deck de 41
também apareceu na segunda demo
anterior. Ainda faltam os
**escritores das estruturas de
origem**.

A seção +0xBE é copiada por
`FUN_00569380`; os nomes
"extra" e "side" não estão
atribuídos sem evidência do jogo.

## O que `demo-menu-trigger.txt` confirmou

O código de cena **`DAT_005F3294`**
tem escritas diretamente referenciadas
no corpo da função
`FUN_0044BEE0`:
`0x0044BF0C` e `0x0044C13D`.
A extração atual listou referências,
**não decompilou seu corpo**.
Também registra outros writers
em `DAT_005F3288/8C/90`,
inclusive nessa rotina.

`FUN_00483500` é uma **fábrica de
objetos de cena**: seu switch
recebe um identificador usado
na criação de diferentes classes.
O **case 3** cria o objeto via
`FUN_0044C3E0`, que por sua vez
chama `FUN_005BFEB0`.
**Não confundir case 3 da fábrica
com sceneMode 7.**

O arquivo não mostra quem chama
`FUN_0044BEE0`, nem onde
está o temporizador de menu.
`ref:00483500` resultou sem
referências estáticas diretas,
o que é consistente com um
caminho indireto/factory; não
é prova de que não tenha
chamadores.

## Nova instrumentação upstream

O `trace demo on` passou a instalar
também probes passivos em:
- `FUN_0044BEE0`:
  `demo_init/scene_config`,
  configuração pré/pós com
  `sceneModeChanged`, caller e thread;
- `FUN_00483500`:
  `demo_init/scene_factory`,
  caller, raw stack arg0 (ABI
  de `__thiscall` ainda pendente),
  configuração pré/pós.

`demoSourceConfig` passou a registrar
`launchGlobals` (DWORDs de
`DAT_005F3288/8C/90`).
A instalação passou em teste de
**simulação** com sete hooks,
inclusive detach em `trace off`,
mas **ainda não foi validada no jogo**.

## Próximas extrações dirigidas

**Quem muda o modo e quando?**
```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-menu-mode-writer.txt `
  -Addresses @(
    '0044bee0',
    'xref:0044bee0',
    '0044c320',
    '0044ad50',
    'ins:0044bef0:100'
  )
```

**Quem fornece a composição dos dois decks?**
```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-source-deck-writers.txt `
  -Addresses @(
    'xref:00a54e60',
    'xref:00a54f42',
    'xref:00a54f3c',
    'xref:00a5501e',
    '0044ad50'
  )
```

Uma captura **desde o menu ocioso** com
`--observe-only`, `trace on 20000` e
`trace demo on` deve confirmar
se `FUN_0044BEE0` é chamada
na transição real para sceneMode 7
e revelar seus callers. Nenhum
patch do valor 7 ou dos buffers
de deck foi proposto até
obtermos esse contexto.


## Refinamento: como o case 7 monta o par de decks

A nova análise [`demo-mode7-deck-resource-steps`](demo-mode7-deck-resource-steps-2026-11-06.md) mostra duas chamadas a `FUN_0044A920(1)` em `0x0044BF75` e `0x0044BFD8`, separadas por uma cópia de `0xE2` bytes do buffer 1 para o 0. Depois ocorrem dois sorteios para `DAT_005F3288/8C`. Isso sugere, mas ainda não prova, um primeiro carregamento persistente seguido por um segundo carregamento de adversário. Instrumentamos `deck_resource_step` com snapshots pré/pós para distinguir a origem de cada lista. Os callers de `FUN_0044BEE0` foram identificados estaticamente, mas falta observar qual passa o modo 7 a partir da inatividade do menu.
