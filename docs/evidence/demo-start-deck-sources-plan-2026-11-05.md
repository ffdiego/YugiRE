# Demonstração: escalonador, início e origem dos decks (2026-11-05)

Fonte enviada pelo operador: `ai-demo-scheduler.txt`
(353 linhas) produzido em Ghidra para SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## O que esta extração prova

`FUN_005BC700` é o **dispatcher de estados do duelo**.
Ele indexa `PTR_LAB_005EFBB0` com a metade
`(DAT_00A577FC & 0xFFFF) >> 8`, executa a
função atual e ajusta o estado quando a função
retorna não zero. Antes de avançar, consulta
`FUN_005BD2D0`, `FUN_0040CA70`,
`FUN_005AA490`, `FUN_0040CF20`,
`FUN_005B9CA0` e `FUN_00569FC0`.
Essa é a máquina **dentro** do duelo:
**não mostra o timeout/menu que ativa a demo**.

O recorte `0x005B72A0` usa o byte de
`DAT_00A579A2` e tabela de estados em
`0x005B7580`. As decisões leem o lado ativo
`DAT_00A577FA & 1`, verificam
`playerFlags & 0x20000` e
`FUN_005AE390`, e chamam
`FUN_005B1360`/ `FUN_005B16C0`.
O executor `FUN_005B6FF0` continua
sendo alcançado por `0x005B73D2` e
`0x005B766D`. O arquivo não traz
o corpo de `FUN_005B726C` completo,
mas o recorte das instruções é
consistente com a captura de IA
nos dois lados já documentada.

## O candidato de deck é, na verdade, shuffle

Em evidência prévia do mesmo EXE
(`player-state-initialization-candidates.txt`),
`FUN_005B9E50` limpa blocos do estado
global, inicializa ambos os lados e chama:

```c
FUN_00568D80((DAT_00A54E5C >> 3) & 1, 8);
FUN_00568D80(1 - ((DAT_00A54E5C >> 3) & 1), 8);
```

**Entre a limpeza de memória e o shuffle,
`FUN_005B9E50` chama
`FUN_00569380(0)` e
`FUN_00569380(1)`.** Essa função
ainda não foi decompilada na evidência
atual e é o candidato prioritário
para identificar a preparação/composição.

O corpo de `FUN_00568D80(side,passes)` em
`native-deck-to-hand-transfer.txt` lê o
`deckCount` em `0x00A55D69 + side*0xD44`
e **embaralha** as entradas de 32 bits
em `0x00A56574 + side*0xD44`, com
`FUN_005BDDB0` como fonte de índices
pseudoaleatórios. Essa rotina **não
seleciona a composição do deck**; a
lista precisa estar presente antes dela.

`FUN_005B9E50` é chamado por
`FUN_005BFEB0`, outro candidato
para investigar a cadeia de início
do duelo. `FUN_00568D80` tem também
um chamador `FUN_00408B90` em
`0x00408C11`, potencialmente
um fluxo separado. Não atribuir
essa distinção à demo sem prova.

## Nova instrumentação (somente observação)

`trace demo on` agora observa:
- `demo_init/duel_entry`:
  `FUN_005BFEB0`, caller e thread;
- `demo_init/state_reset`:
  `FUN_005B9E50`, decks de ambos os
  lados antes/depois;
- `demo_init/deck_prepare`:
  `FUN_00569380`, snapshots antes/depois
  para cada lado;
- `demo_init/deck_shuffle`:
  `FUN_00568D80`, lado, passes e
  arrays `deck.words` (uint32)/
  `deck.indices` (12 bits) antes/depois.

Os dados são registrados em `commands-*.jsonl`,
no buffer já existente, sem execução de
funções nativas pelo adaptador.
**Ainda não há uma captura real com
esses novos hooks.** Comparar hashes
de listas *ordenadas* por índice por
demonstração para distinguir
composição de ordem. Os arrays podem
estar vazios em etapas iniciais, e
o formato de índices deve ser
confirmado pelos próprios valores.

## Próximas extrações estáticas

A primeira consulta investiga cadeia
de inicialização, callers e shuffle:

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-start-path.txt `
  -Addresses @(
    'ref:005bfeb0',
    'ref:005b9e50',
    'ref:005c0000',
    '005bfeb0',
    '00408b90',
    '005bddb0'
  )
```

A segunda investiga quem alimenta
os arrays e quais outros contextos
carregam decks:

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-deck-sources.txt `
  -Addresses @(
    'xref:00a56574',
    'xref:00a55d69',
    'ref:00568d80',
    'ref:00569380',
    '00569380',
    '0044b980'
  )
```

**Limites:** os `ref:` selecionam
o corpo da função que contém uma
referência direta; tabelas indiretas
podem não aparecer. Os `xref:`
listam referências sem selecionar
automaticamente todos os callers;
isso evita um relatório gigantesco.
Essas duas consultas não garantem
encontrar o timeout/menu: se
a cadeia termina em outro módulo
ou callback indireto, investigar a
transição inicial mediante hooks
dinâmicos (callers de `duel_entry`),
analisando somente o EXE do hash
validado.

Este relatório não afirma que os
dois decks foram selecionados
aleatoriamente ou que vieram de
um recurso fixo. A composição
ainda não foi capturada.
