# Validação Windows — LP reais e escolhas da IA em duelo normal

Captura UTC 2026-11-11; recebida/analisada em 2026-11-10 (horário local
America/Campo_Grande). Instrumentação executada no Joey original em modo
`--observe-only`, com `trace on 20000` e `trace ai on`.

Arquivo local (não versionado):
`commands-20261111-022940-355228-pid36348.jsonl`.
SHA256: `dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.

## Integridade da captura

- 1.121 linhas: um `metadata` e 1.120 eventos sequenciais, `seq=1..1120`.
- `metadata.seen=1120`, `retained=1120`, `overwritten=0`, capacidade 20000.
- 649 eventos `command`, 470 `ai_probe`, um `marker` (`lp-ia-duelo-normal`).
- Janela da captura: 295,284 segundos, ~4 min 55 s.
- 29 eventos com `lifePointsObserved`; zero `lifePointsReadError`.
- Último comando: `seq=1120`, `opcodeMasked=0x0004`,
  `opcodeRaw=0x8004` e `caller=0x005BC545`.
- No terminal, `lifePointsObserved.lifePoints=[0,300]`, idêntico a
  `terminalNativeState.lifePoints`; `duelPhaseByte=71`,
  `stateWordAtA577FE=3`, `duelModeWord=11`, `sceneMode=0`.
  Isso evidencia CPU/lado0 com zero LP e jogador/lado1 com 300 LP.
  **A semântica de todos os bits do terminal ainda não foi decodificada.**

A versão do probe lê LP no `onEnter` do produtor
`FUN_005B9170`, imediatamente **antes** de enfileirar o comando atual.
Isso não é leitura posterior à execução do próprio `0x0043`.

## Comparação evento a evento: dano enfileirado vs. LP reais

Todas as **14 chamadas `0x0043`** foram seguidas por outro evento
`command` com snapshot de LP, e em **14/14** a diferença observada
foi exatamente o dano enfileirado, com saturação em zero no golpe final.
Não foi detectada alteração adicional do outro lado nessas transições.

| `seq` de `0x0043` | Alvo | Dano | LP antes | `seq` próxima leitura | LP depois |
| ---: | --- | ---: | ---: | ---: | ---: |
| 43 | jogador/lado1 | 200 | 8000 | 44 | 7800 |
| 90 | jogador/lado1 | 2300 | 7800 | 91 | 5500 |
| 273 | CPU/lado0 | 400 | 8000 | 274 | 7600 |
| 336 | jogador/lado1 | 500 | 5500 | 337 | 5000 |
| 406 | jogador/lado1 | 1500 | 5000 | 407 | 3500 |
| 415 | jogador/lado1 | 2000 | 3500 | 416 | 1500 |
| 547 | jogador/lado1 | 1200 | 1500 | 548 | 300 |
| 556 | CPU/lado0 | 2000 | 7600 | 557 | 5600 |
| 767 | CPU/lado0 | 400 | 5600 | 768 | 5200 |
| 798 | CPU/lado0 | 800 | 5200 | 799 | 4400 |
| 890 | CPU/lado0 | 2100 | 4400 | 891 | 2300 |
| 978 | CPU/lado0 | 100 | 2300 | 979 | 2200 |
| 987 | CPU/lado0 | 1700 | 2200 | 988 | 500 |
| 1119 | CPU/lado0 | 1000 | 500 | 1120 | 0 |

O campo `sideBit15` de `0x0043` vale 0 para CPU e 1
para jogador em todas as 14 amostras. Somatório de danos:
CPU/lado0 8.500, jogador/lado1 7.700. Partindo dos LP
observados [8000,8000], o histórico reproduz [0,300].
O último comando pediu 1000 com 500 LP restantes: o valor
observado no terminal foi 0, não um LP negativo/underflow.
Não há evidência de cura nesta captura; **isso não prova que
todas as mudanças de LP em todos os duelos sempre usem `0x0043`**.

## Seletores e correspondência com preparação da CPU

- `ai_probe` teve 470 eventos: 402 retornos de `computed_stats`,
  22 de `combat_event` e 46 eventos de entrada/saída de seletores.
- 23 chamadas completas de seleção: 13 `select_normal`
  (6 índices válidos, 7 retornos -1) e 10 `select_priority`
  (todos com índice válido; alguns são chamadas repetidas).
- Nenhuma entrada de seletor registrou `handReadError`.
- Foram 21 comandos `0x00CE`, dos quais 12 são do lado CPU
  (bit15=0) e contêm `ceNativeState.source`.
- Em **12/12** comandos CE da CPU, `ceNativeState.source`
  coincide com `selectedCardIndexAtEntry` do **último retorno
  válido de seletor** do lado 0 anterior ao comando:

| `seq` CE | `seq` seleção | Seletor | Carta interna |
| ---: | ---: | --- | ---: |
| 30 | 26 | normal | 550 |
| 215 | 212 | priority | 364 |
| 324 | 320 | normal | 689 |
| 395 | 391 | normal | 1099 |
| 471 | 469 | priority | 839 |
| 523 | 519 | normal | 834 |
| 614 | 612 | priority | 839 |
| 681 | 677 | normal | 525 |
| 754 | 750 | normal | 525 |
| 855 | 853 | priority | 793 |
| 954 | 950 | priority | 364 |
| 1077 | 1075 | priority | 839 |

O padrão observado frequentemente é `select_normal` retornar
-1, `select_priority` produzir uma posição válida e `0x00CE`
usar a carta selecionada. Em alguns instantes há dois
`select_priority` consecutivos selecionando a mesma carta.
A correspondência de `source` **não** explica os critérios de
pontuação, a legalidade de alternativas nem garante que a ação
se concluiu; `computed_stats` e `combat_event` podem conter
avaliações hipotéticas. Não equiparar 402 avaliações a 402 opções.

## Alcance e próximos passos

Essa captura **valida no Windows** o novo contrato de LP por entrada
do produtor para um duelo inteiro, incluindo o terminal por dano
letal, e fornece um fixture de 12 escolhas da CPU correlacionadas a
comandos de preparação. Os testes Node/Frida simulados anteriores
não substituíam esta validação dinâmica.

Próxima investigação: mapear `select_normal` (`0x005B1360`),
`select_priority` (`0x005B16C0`) e seus filtros/pontuações,
começando por um estado de mão conhecido e um único comando
`0x00CE`, sem interpretar buffers de avaliação como ações
efetivamente realizadas. Para decodificar bits de terminal,
comparar com um duelo em que CPU tenha LP positivos e jogador
zero, observado da mesma maneira.

Relacionado: [contrato e contexto de LP](../research/duel-life-points-capture.md).
