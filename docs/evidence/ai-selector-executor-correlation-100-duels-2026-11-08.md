# Correlação offline dos seletores da IA com o executor (100 duelos)

**Fonte:** `duels-20261108-055008-899147-pid22128.jsonl`, captura real de 100 duelos, com `--trace-ai` e substituição dos decks A/B, enviada pelo operador em 2026-11-08. Arquivo com 152.735 eventos sequenciais, sem sobrescritas. Esta análise não fez novas intervenções no processo do Joey.

## Descoberta: podemos recuperar a identidade das cartas sem novo hook

Cada um dos **1.299** eventos `demo_executor/normal_placement/enter` foi precedido **imediatamente**, na linha/evento anterior (`executor.seq = selector.seq + 1`), por um evento `ai_probe/select_normal/leave` ou `ai_probe/select_priority/leave` do **mesmo lado e thread**, cujo `selectedHandIndex` era **idêntico** ao `handIndex` recebido pelo executor.

| Encadeamento | Quantidade |
|---|---:|
| `select_normal.leave → normal_placement.enter` | 610 |
| `select_priority.leave → normal_placement.enter` | 689 |
| **Total** | **1.299** |

Em **1.200** dessas chamadas, `duelRunning=true` e o evento do executor já incluía `handInternalIndex` e `handCount`, lidos da memória original do Joey. Portanto, podemos ligar o resultado do seletor à identidade interna da carta correspondente na mão **na entrada do executor**, sem coletar outro trace. As 1.200 identidades pertencem à lista `main` do deck configurado para o respectivo lado (A para 0, B para 1).

As outras **99** chamadas ocorreram com `duelRunning=false`; não possuem `handInternalIndex` confiável e não devem ser contabilizadas como cartas identificadas. Houve uma por demo, exceto a demo 53.

## Encaminhamentos durante o duelo

| Seletor | `modeSelector=0` | `modeSelector=1` | Total |
|---|---:|---:|---:|
| `select_normal` | 610 | 0 | 610 |
| `select_priority` | 259 | 331 | 590 |
| **Total** | **869** | **331** | **1.200** |

Os 100 duelos tiveram mediana **10,5** chamadas ativas do executor por duelo (intervalo **3–33**). Distribuição por lado:
- Lado 0: 287 `select_normal`, 157 `select_priority/mode=0`, 150 `select_priority/mode=1` — **594** chamadas.
- Lado 1: 323 `select_normal`, 102 `select_priority/mode=0`, 181 `select_priority/mode=1` — **606** chamadas.

Cartas mais frequentemente encaminhadas pelo lado 0: índice 525 (100 vezes), 151 (83), 1052 (79), 834 (60) e 1099 (49). Pelo lado 1: índice 887 (100), 885 (90), 886 (78), 828 (66), 1038 (56). **Frequência bruta não mede preferência**, pois os decks têm quantidades de cópias diferentes e não conhecemos todas as alternativas disponíveis em cada instante.

## Limitações

- **Esta é uma correlação entre retorno de seletor e chamada ao executor**, não a prova de que a carta foi efetivamente invocada ou colocada em campo. A função `FUN_005B6FF0` devolveu `returnRaw=1` nos pares de saída registrados, mas ainda não temos contrato semântico do retorno.
- Não captura todas as decisões da IA, apenas `select_normal`, `select_priority` e seu caminho ao executor de posicionamento. Um seletor pode devolver índice positivo sem invocar esse executor logo depois.
- Não podemos reconstruir as cartas que estavam disponíveis na mão **na entrada de cada avaliação** para estimar escolhas rejeitadas ou rankings de candidatos; para isso, um snapshot controlado da mão na entrada dos seletores ainda será útil.
- O nome `modeSelector` é literal do evento; não equiparar `mode=0/1` a `summon/set` sem decodificar o contrato.
- Este conjunto de fatos é específico do executável verificado e do corpus de 100 demos com os decks A/B; revalidar se mudarmos os hooks ou a versão do jogo.

## Aproveitamento para C#

Um futuro gerador de testes pode utilizar cada par confirmado como evidência:

```text
duel, side, selector, selectedHandIndex, handInternalIndex, modeSelector,
selectorSeq, executorSeq, handCount
```

A partir daí, identificar na engine C# os padrões de posicionamento e comparar os resultados observáveis, sem pressupor ainda equivalência entre a escolha e uma ação executada com sucesso. A próxima captura só precisará adicionar snapshots de mão **se quisermos explicar por que a IA escolheu aquela carta entre as alternativas**.

A análise produziu, como artefatos locais da conversa, um CSV de **1.299 pares** (com 99 linhas não ativas e identidade de carta vazia), outro de **100 duelos**, e um analisador Python executável sem Frida/Windows.
