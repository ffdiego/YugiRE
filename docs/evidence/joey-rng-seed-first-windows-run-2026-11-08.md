# YugiLab: primeira validação real de `--seed` no Windows

**Execução:** `parallel-mjj1z5ia.zip` enviada em 2026-11-08.
**Ambiente:** Joey original PE32, demo com decks A/B, `--duels 2 --workers 2 --seed 12345 --speed-value 600 --trace-ai`.
**Status:** o controle de seed e o encerramento dos processos **funcionaram nesta rodada**. Ainda NÃO testamos duas execuções independentes com a mesma seed para comprovar reprodução integral.

## Evidência (manifest, logs e JSONL)

| Métrica | Worker 1 | Worker 2 |
|---|---:|---:|
| PID Joey | 13632 | 8484 |
| Duelos concluídos | 1/1 | 1/1 |
| Global duel index | 0 | 1 |
| Base seed | 12345 | 12345 |
| Seed original recebida em `FUN_005C3BF5` | 1794168466 | 1794168466 |
| Seed efetiva aplicada pelo hook | 2435775735 (`0x912EFCF7`) | 4228375765 (`0xFC07E4D5`) |
| Eventos | 1471 | 1729 |
| Captura `complete` | true | true |
| Eventos perdidos | 0 | 0 |
| `rngSeedOverridesApplied` | 1 | 1 |
| `processTerminationVerified` | true | true |
| Exit code do worker | 0 | 0 |

Manifest: `complete=true`, `capturedDuels=2`, `eventsWritten=3200`,
`elapsedSeconds=27.156`.
Auditoria: `fingerprintedScenes=2`, `uniqueFingerprints=2`,
`duplicateObservations=0`. As ordens iniciais dos dois decks diferiram
entre os workers.

Os dois eventos do setter na entrada do duelo:
- caller `0x005BFEBD`, `duelEntrySeedCall=true`,
  `seedOverrideApplied=true`, `globalDuelIndex=0/1`;
- `candidateSeedRaw=1794168466` nos dois workers, correspondendo exatamente
  a **2026-11-08 20:07:46 UTC**, isto é, o segundo Unix da execução.
  Esse dado apoia fortemente a hipótese de seed inicial dependente de
  `time(NULL)`; para comprovar a origem exata é preciso seguir o
  argumento até o caller `FUN_0044C3E0` (em especial `0x0044C62E`).

**Outro reset observado:** no worker 2, o evento
`demo_init/rng_seed_candidate` (seq 1129) ocorreu no caller
`0x00408C0E`, `candidateSeedRaw=21001`,
`seedOverrideApplied=false`. Em seguida, houve novo
`deck_shuffle/leave` do lado 1 (seq 1132). Isso confirma
que o hook NÃO alterou outros caminhos de reseed.
O worker 1 não registrou evento equivalente nessa rodada.

Os comandos também tiveram contagens diferentes:
worker 1: 354 comandos; worker 2: 557 comandos.
Isso não prova uma diferença de toda a lógica, mas confirma que
as capturas não foram duplicatas integrais.

## Interpretação

Os resultados confirmam empiricamente:
1. A interceptação do setter na entrada do duelo aconteceu.
2. Uma seed derivada distinta foi aplicada em cada índice global.
3. Os embaralhamentos iniciais diferiram, mesmo com a mesma seed-base.
4. O exportador conferiu as aplicações de seed e finalizou.
5. Os dois PIDs do Joey foram encerrados, com confirmação pelo Windows.

**Ainda NÃO confirmam reprodutibilidade entre rodadas.**
Precisamos rodar o mesmo comando novamente, com o mesmo deck e seed, em outro
horário, e comparar:
- `effectiveSeedRaw` por `globalDuelIndex`;
- hashes SHA256 dos dois decks embaralhados na primeira vez;
- sequências de comandos normalizados (sem timestamps, seq e campos voláteis);
- decisões de IA, reseeds posteriores e resultados do duelo.

Também repetir com uma base distinta (`--seed 12346`) para verificar
divergência intencional.

Observação adicional: os logs mostram
`Unable to start idund: idund did not start listening`,
mas nessa rodada a captura, o Frida e o encerramento funcionaram;
essa mensagem não indica falha efetiva do laboratório aqui.
