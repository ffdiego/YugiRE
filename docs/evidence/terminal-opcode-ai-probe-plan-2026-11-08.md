# 0x0004: análise offline dos 100 duelos e próximo probe da IA

**Fonte:** `duels-20261108-051720-709901-pid23392.jsonl`, arquivo enviado pelo operador, captura contínua validada de 100 duelos.

## Observações comprovadas no corpus

Há 100 comandos `kind=command` com `opcodeMasked=0x0004`.
Todos vieram do mesmo `caller=0x5bc545`, na thread de execução
observada. Seus valores brutos de `args[0]` são:

| `args[0]` | Frequência |
|---|---:|
| `0x00030004` | 22 |
| `0x00038004` | 28 |
| `0x00070004` | 24 |
| `0x00078004` | 26 |
| **Total** | **100** |

`args[1]` vale **0 em 44 casos** e **1 em 56**.
`args[2]` e `args[3]` são sempre 0 na amostra,
mas o contrato da função não foi confirmado, então não
classificar os argumentos 2 e 3 como parâmetros válidos.
O bit 15 do primeiro argumento coincidiu, em todos os duelos,
com o slot de origem do lado 0; isso é **correlação**, não
prova de que o bit identifique o vencedor.

Não usar `args[1]` como winner nem deduzir winner dos bits
`0x30000`/`0x70000` sem evidência estática/dinâmica.

## Instrumentação adicionada

No `yugire_dual_loader.js`, quando o produtor
`FUN_005B9170` recebe opcode mascarado `0x0004` e seu
`returnAddress` é exatamente `0x005BC545`, o evento
`command` ganha `terminalNativeState`, uma leitura
somente passiva dos campos conhecidos:

- `duelRunningWord` — `0x00A577F8`, uint16
- `duelPhaseByte` — `0x00A577FA`, uint8
- `stateWordAtA577FE` — `0x00A577FE`, uint16
- `duelModeWord` — `0x00A54E5C`, uint32
- `sceneMode` — `0x005F3294`, uint32
- `playerFlags[0..1]` — `0x00A56430 + side*0xD44`, uint32
- `queueCount` — `0x00A558A8`, uint32

Os nomes são **identificadores de observação**, não
definições comprovadas de vencedor, perdedor ou motivo do
resultado. Falhas de leitura geram `terminalNativeReadError`,
sem interromper a partida.

A opção do launcher `--trace-ai` habilita, **antes do resume**,
os quatro hooks passivos de `trace ai on`, já existentes:
`select_normal` `0x005B1360`,
`select_priority` `0x005B16C0`,
`combat_event` `0x005B0F40` e
`computed_stats` `0x0056DD30`.
A instrumentação de subchamadas só registra as chamadas
que acontecem durante um seletor observado. Não altera
retornos, memória ou decisões.

## Primeiro teste recomendado: 3 duelos

Atualize `experiment/hybrid-native-probe`, abra PowerShell em
`native_hooks` e execute:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 2400 --deck-0 ".\decks\demo-A.json" --deck-1 ".\decks\demo-B.json" --trace-ai --duels 3
```

Esperado: `complete=true`, `overwritten=0`, 3 terminais,
eventos `ai_probe` com `enter/leave`, e
`terminalNativeState` em cada `0x0004` cujo caller
seja `0x005BC545`. A existência e cobertura dos probes
de IA devem ser **medidas** na captura real; não assumir
que sua presença já foi validada só porque instalaram.

Se os 3 forem concluídos e a instrumentação mantiver
uma sequência de eventos íntegra, executar mais 100
trocando apenas `--duels 3` por `--duels 100`.

## Investigação estática da rotina de resultado

Na raiz do repositório (Windows com Ghidra configurado):

```powershell
.\scripts\Run-TerminalResultRecon.ps1
```

Gera dois relatórios de leitura em `docs/evidence/` com
a função que contém `0x005BC545`, o dispatch nativo e
as instruções adjacentes. Combinados com os
`terminalNativeState` da nova captura, devem ajudar
a mapear a causa do encerramento antes de atribuir
resultado ou taxa de vitória.

## Limites

`--trace-ai` tem custo adicional de hooks/JSONL.
Rodar um piloto para verificar se o volume pode ser
absorvido sem `overwritten>0` e sem alterar a taxa de
execução de forma relevante. As capturas anteriores
continuam válidas como referência, mas não continham
`terminalNativeState` nem `ai_probe` porque esses
eventos não estavam habilitados.
