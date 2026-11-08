# Laboratório YugiRE — 100 duelos com probes da IA a velocidade 24000

**Fonte:** `duels-20261108-055008-899147-pid22128.jsonl`, captura enviada pelo operador em 2026-11-08. O arquivo original foi analisado integralmente (43 MB, 152.735 eventos com `seq` contínuo); os valores abaixo são observações do trace, não contratos nativos comprovados no Ghidra.

## Integridade

- `requestedDuels=100`, `capturedDuels=100`, `complete=true`, `overwritten=0`.
- Exatamente 100 cenas modo 7 completas; 200 substituições `applied`, 200 `verified`, 0 `failed`/`mismatch`.
- Origem do lado 0: slot 0 em 47 duelos, slot 1 em 53. 200 shuffles iniciais, mais 39 embaralhamentos adicionais no lado 1.
- Primeira/última marca temporal dos eventos: **1.054,741 s** (~17 min 35 s). Duração da cena ao comando terminal: média **9,885 s**, mediana **8,157 s**, mínimo **3,643 s**, máximo **28,945 s**.
- A captura anterior de 100 duelos em velocidade 4800, **sem probes da IA**, durou ~1.037 s e continha 53.538 eventos. Esta tem muito mais hooks e eventos, logo comparar desempenho não isola o efeito da velocidade.

## Cobertura da IA

| Tipo de evento | Quantidade |
|---|---:|
| `ai_probe.computed_stats.leave` | 87.647 |
| `ai_probe.combat_event.leave` | 5.860 |
| `select_normal` (pares enter/leave) | 1.913 |
| `select_priority` (pares enter/leave) | 1.676 |
| `demo_executor.normal_placement` (pares enter/leave) | 1.299 |
| `command` | 46.768 |

`select_normal` devolveu índice não negativo em 610 chamadas e negativo em 1.303; `select_priority`, respectivamente 1.007 e 669. **Índice retornado não é sinônimo de jogada executada**. Ainda precisamos correlacionar o retorno com um snapshot da mão na entrada do seletor.

## Estrutura candidata de atributos: `FUN_0056DD30`

- 87.647 saídas `outputHex` de 12 bytes, interpretadas provisoriamente como `<HHII`: índice interno, campo desconhecido, ATK, DEF.
- 12.579 saídas inteiramente zero; **44 tuplas distintas**, 26 índices não nulos.
- Houve índices repetidos com valores diferentes de ATK/DEF. Por exemplo, índice 100 apresentou (1000,500) em 1.566 observações e (1200,700) em 393; índice 886 apresentou (1500,1800), (1800,1500) e (900,1200). A causa não foi estabelecida — mudanças de atributos são hipótese plausível.
- Os índices observados vieram dos decks configurados. **Duas cartas apenas da terceira lista do deck A** também tiveram atributos consultados pelo seletor: índice 327 (306 vezes, 2400/2000) e índice 651 (212 vezes, 1700/1500), ambas com `side=0`. Isso confirma consulta de cartas auxiliares, **não prova invocação ou presença em campo**.

## Nova relação nos terminais `0x0004`

Todos os 100 terminais foram emitidos em `caller=0x005BC545`; todos têm `terminalNativeState` legível:

| `args[0]` | Contagem |
|---|---:|
| `0x00030004` | 26 |
| `0x00038004` | 26 |
| `0x00070004` | 21 |
| `0x00078004` | 27 |

Relações empíricas **100/100**:

```text
args[0] == (terminalNativeState.duelModeWord << 15) | 0x0004
args[1] == terminalNativeState.duelPhaseByte >> 7
```

`args[1]` foi 0 em 48 duelos e 1 em 52. `sceneMode=7`,
`stateWordAtA577FE=3` e `queueCount=0` em todos os terminais.
A identificação de **vencedor e motivo do fim permanece em aberto**;
nenhum desses campos deve ser rotulado vencedor sem inspecionar o
chamador nativo e produzir um duelo com resultado conhecido.

## Próxima investigação

1. No Ghidra, inspecionar o produtor do comando em torno do callsite `0x005BC545`, distinguindo flags de modo e resultado.
2. **Atualização após análise offline:** não precisamos de novo hook apenas para obter a **carta encaminhada ao executor**: 1.299 retornos de seletores foram imediatamente seguidos pela entrada do executor, e 1.200 já traziam `handInternalIndex`. [Correlação e limitações](ai-selector-executor-correlation-100-duels-2026-11-08.md). Um snapshot da mão **na entrada do seletor** continua útil para descobrir quais alternativas foram rejeitadas e por que determinada carta foi escolhida.
3. Verificar os offsets de `FUN_0056DD30` no decompilador e reproduzir alterações de ATK/DEF controladamente.
4. Preservar o JSONL bruto. Os resultados compactos por duelo foram exportados em `yugire_100_duels_ai_summary.json` (artefato da conversa), que não substitui os dados originais.
