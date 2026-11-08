# YugiLab: reprodutibilidade observada em duas execuções independentes

**Data:** 2026-11-08.

**Capturas analisadas:** `parallel-mjj1z5ia.zip` (20:07 UTC) e
`parallel-a1xf_v2y.zip` (20:17 UTC), recebidas do operador.

**Configuração comum:** `--duels 2 --workers 2 --seed 12345
--speed-value 600 --trace-ai`, mesmos `demo-A.json` e `demo-B.json`.
Todos os JSONL possuem `capture_summary.complete=true`,
`overwritten=0`, `rngSeedOverridesApplied=1` por worker.
Ambos os manifests são `complete=true` e todos os PIDs têm
`processTerminationVerified=true`.

## Correspondência por índice global, não PID

| Item | Execução A | Execução B | Correspondência |
|---|---|---|---|
| Índice global 0: PID | 13632 | 27000 | PIDs diferentes |
| Índice global 0: seed efetiva | 2435775735 | 2435775735 | Igual |
| Índice global 0: eventos | 1471 | 1471 | Igual quantidade |
| Índice global 0: hash dupla de shuffles | `92b3623a5774...` | `92b3623a5774...` | Igual |
| Índice global 0: eventos de IA | 1060 | 1060 | Todos iguais ignorando timestamp/threadId |
| Índice global 0: comandos | 354 | 354 | Sequência de opcodes e palavras baixas idênticas |
| Índice global 1: PID | 8484 | 13404 | PIDs diferentes |
| Índice global 1: seed efetiva | 4228375765 | 4228375765 | Igual |
| Índice global 1: eventos | 1729 | 1729 | Igual quantidade |
| Índice global 1: hash dupla de shuffles | `adf2856605aa...` | `adf2856605aa...` | Igual |
| Índice global 1: eventos de IA | 1097 | 1097 | Todos iguais ignorando timestamp/threadId |
| Índice global 1: comandos | 557 | 557 | Sequência de opcodes e palavras baixas idênticas |

### Verificações precisas

Para cada índice global, as listas de eventos JSONL (sem metadata e footer)
têm **exatamente a mesma sequência de `kind/function/phase` e `seq`**.
Todos os eventos de `ai_probe` são idênticos em todos os seus campos
exceto `timestampMs` e `threadId`:
**1060/1060** no índice 0 e **1097/1097** no índice 1.

Os eventos `deck_shuffle/leave` são idênticos, incluindo os arrays
`deck.indices` **e** `deck.words`; no índice 1 também é igual o shuffle
adicional provocado pelo setter em `0x00408C0E`, com seed 21001.
O RNG foi reinicializado nessa chamada interna sem override.

Todos os comandos têm a mesma ordem e `opcodeRaw/opcodeMasked`.
Os **16 bits inferiores de cada palavra em `args` coincidem** em todos
os 911 comandos (354 + 557).

### Diferenças: NÃO são capturas byte a byte iguais

Apesar da correspondência de gameplay observável, campos brutos diferem:
- Índice 0: **51/354** comandos têm argumentos diferentes; em 50,
  `args[1]`, e em 1, `args[0]`.
- Índice 1: **68/557** comandos têm argumentos diferentes; em 67,
  `args[1]`, e em 1, `args[0]`.
- Todas as diferenças de argumentos são nos **16 bits superiores**.
  Exemplo: índice 0, opcode `0x00CE`:
  `args[1] 0x00EA02E1` vs `0x00F402E1`.
- Em `demo_init`, `config.launchGlobals[2]` reflete a seed Unix original:
  no índice 0, `1794168466` vs `1794169022`;
  no índice 1, `1794168466` vs `1794169023`.
- O campo `replacementName` do mutex muda com o PID.
- No índice 0 há diferenças em snapshots de `sourceDeck(s)`
  anteriores à aplicação dos planos de deck. Porém os decks efetivos
  após o override e seus shuffles correspondem.

Esses bits altos podem representar metadados/identificadores de instância
de carta, mas **a semântica ainda não foi decodificada**.
Portanto não se deve descartar ou normalizar esses campos para afirmar
equivalência total sem investigar o formato dos comandos.

**Não foi decodificado o vencedor**: o opcode terminal `0x0004` foi
observado em ambos, mas não basta para identificar resultado/causa.
A igualdade de comportamento dos probes é evidência forte, não
prova de equivalência completa de todos os estados internos do jogo.

## Conclusão e próximas prioridades

1. O mesmo `--seed` reproduz embaralhamentos e sequências de decisões
   observadas da IA em duas execuções iniciadas cerca de 9 minutos
   separadas. Isso valida empiricamente o mecanismo de
   `FUN_005BFEB0 -> FUN_005C3BF5`.
2. A instrumentação produz uma base prática para regressão, mas precisa
   explicitar quais campos são semânticos versus voláteis.
3. Próximo passo recomendado: inspecionar bits 16–31 dos argumentos
   brutos dos comandos `0x00CE`, `0x00CF`, `0x0072`, `0x00E6`,
   `0x006F` e os poucos casos em `args[0]`, antes de criar
   um comparador de estado nativo ou portá-lo para a engine C#.
4. Repetir com mais seeds/decks para avaliar a robustez, sem confundir
   reproduzir a mesma sequência de eventos com demonstrar
   determinismo universal da engine.

*Origem:* comparação programática dos dois ZIPs originais, alinhando
pelos `metadata.rngSeedOverride.globalDuelStartIndex`.
