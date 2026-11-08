# Primeira captura real de seletores da IA e estado de término

**Origem:** `duels-20261108-054534-733240-pid20740.jsonl`, arquivo enviado pelo operador.  
**Objetivo:** validar `--trace-ai --duels 3` e os snapshots passivos de `terminalNativeState`.

## Integridade e configuração

- `requestedDuels=3`, `capturedDuels=3`, `complete=true`.
- `eventsWritten=2505`, `overwritten=0`.
- `seq=1..2505`, sem lacunas ou duplicações; 2.507 linhas incluindo metadata/footer.
- Velocidade solicitada 2400, com `speedy_override` de 60 para 2400.
- Deck A no side 0 (40 main), deck B no side 1 (43 main).
- `deck_override/applied=6` e `deck_override/verified=6`,
  ou seja, 3 preparações por lado, `failed=0`.

## Métricas por duelo

| Duelo | Cena seq | Terminal seq | Duração do início da cena ao terminal | `0x0001` | `ai_probe` | Executor enter |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 9 | 838 | 6,736 s | 9 | 471 | 8 |
| 2 | 841 | 1685 | 7,279 s | 12 | 448 | 10 |
| 3 | 1688 | 2505 | 4,979 s | 10 | 509 | 8 |

Essas durações incluem preparação e transições da cena, não apenas tempo
de raciocínio da IA.

### Instrumentação da IA

**1.428 eventos `ai_probe`**:

| Tipo | Quantidade |
|---|---:|
| `select_normal.enter` | 29 |
| `select_normal.leave` | 29 |
| `select_priority.enter` | 24 |
| `select_priority.leave` | 24 |
| `computed_stats.leave` | 1.261 |
| `combat_event.leave` | 61 |

Os seletores tiveram entradas e saídas balanceadas (53 chamadas).
Nenhum `readError` foi observado.

### Estrutura candidata de `computed_stats`

As 1.261 saídas de `FUN_0056DD30` têm `outputHex`
com 12 bytes (incluindo 184 totalmente zeradas).
Quando interpretadas como `<HHII` (*little-endian*),
os campos produzem uma estrutura candidata:

| Offset | Campo candidato | Tipo |
|---|---|---|
| `+0x0` | índice interno da carta | `uint16` |
| `+0x2` | flags/atributos ainda desconhecidos | `uint16` |
| `+0x4` | ATK observado | `uint32` |
| `+0x8` | DEF observado | `uint32` |

Exemplo real: `77030605A406000040060000` →
índice 887, campo `0x0506`, ATK 1700, DEF 1600.
Essa tupla ocorreu 275 vezes. A tupla de índice 1082,
campo `0x0201`, ATK 2400, DEF 2000, ocorreu 37 vezes.

Nos **1.077** resultados não zerados, o índice interno extraído
**sempre pertenceu a um dos decks A/B configurados**.
Foram observados 15 índices não nulos distintos em 16 tuplas
de 12 bytes (contando o registro de zeros).

**Interpretação forte, mas ainda não confirmada estaticamente.**
As palavras em +0x4/+0x8 são candidatas a ATK/DEF; é necessário
verificar a implementação de `FUN_0056DD30` no Ghidra e testar
efeitos que modifiquem atributos para distinguir base de valores
efetivos e validar o campo `+0x2`. Não generalizar a todos os
cards, efeitos ou cenas sem testes.

## Estado nativo no término

Os três comandos têm `caller=0x5bc545`,
`opcodeMasked=0x0004` e `terminalNativeState`
sem erro de leitura:

| Duelo | `args[0]` | `args[1]` | `duelRunningWord` | `duelPhaseByte` | `duelModeWord` |
|---:|---|---:|---|---|---|
| 1 | `0x00070004` | 1 | `0x0008` | `0x87` | `0x0000000E` |
| 2 | `0x00038004` | 0 | `0x000B` | `0x47` | `0x00000007` |
| 3 | `0x00030004` | 1 | `0x0009` | `0x87` | `0x00000006` |

Nas três amostras, `sceneMode=7`,
`stateWordAtA577FE=3`, `queueCount=0`.
`playerFlags` foi [0,720896] nos duelos 1 e 3,
e [0,0] no duelo 2. Os nomes das variáveis de instrumentação
não demonstram semântica de vitória, derrota ou causa de término.

O bit 15 do comando coincide com `duelModeWord & 1`,
mas nem ele nem `args[1]` são um contrato de vencedor conhecido.

## Próximos passos

1. **Pode-se habilitar 100 duelos com `--trace-ai`** sem mudar a
   instrumentação; a captura de três duelos foi íntegra.
   A maior taxa de eventos exige monitorar `overwritten`.
2. No Ghidra, inspecionar `FUN_0056DD30` para confirmar os offsets,
   e a função que contém `0x005BC545` para decodificar o término.
3. Para reconstruir decisões carta a carta no futuro, considerar
   anexar *snapshot* validado da mão e campo na entrada do seletor,
   pois `selectedHandIndex` isolado não identifica a carta.
