# Primeira captura dinâmica do trace direto da IA (2026-11-05 UTC)

**Fonte:** `commands-20261105-035417-642766-pid5596.jsonl` fornecido
pelo operador (2 eventos + 1 linha de metadados).
Teste em duelo normal do Joey, com EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`
validado pelo loader.

## Evidência capturada

O console executou:
```text
trace on 12000
trace ai on
ai check 1
[Enter para inspecionar console.last]
trace status
trace off
trace dump
```

O status na captura mostra `aiActive=True`,
`seen=2`, `retained=2`, `overwritten=0`,
`capacity=12000`. A exportação JSONL tem:

| seq | kind | phase | threadId | resultado |
| --- | --- | --- | --- | --- |
| 1 | `ai_console` | `start` | 10120 | solicitação `requestId=1`, `side=1`, `checkOnly=true`, `handCountAtRequest=5` |
| 2 | `ai_console` | `decision` | 10120 | `allowed=false`, `state=checked`, `detail="Normal summon/set already used; selection not attempted"` |

Ambos os eventos têm `timestampMs=1793850837445`.
Não houve `select_enter` nem `select_leave`.
A origem `yugire_console_native_call` indica
instrumentação **explícita do adaptador**,
não um evento de `Interceptor.attach` passivo.

## Interpretação fundamentada no código

A função `servicePendingConsoleAction()` bloqueia
`ai_normal` quando `playerFlags[side] & 0x20000`
é diferente de zero. A mensagem observada
é emitida por essa guarda **antes** de
`FUN_005B1360`.

Assim, esta captura **valida a gravação de eventos
`ai_console`** e confirma que o teste foi rejeitado
na checagem de invocação normal já usada.
Não demonstra que `FUN_005B1360` retornou
`-1`, nem informa o motivo de qualquer
seleção estratégica. Também **não valida**
a instrumentação passiva `ai_probe`, pois
não houve invocação do seletor nessa tentativa.

O aviso `Unable to start idund` apareceu no
console, mas a sessão Frida conectou, validou
o EXE, instalou os callbacks e exportou eventos.
Não há evidência nesse log de que o aviso tenha
impedido o trabalho do adaptador.

## Próxima coleta útil

Começar novo turno do jogador **sem consumir sua
invocação normal**. Ativar `trace on` e
`trace ai on`, depois chamar `ai check 1`,
consultar `console.last` e `trace status`
antes de `trace off`/`trace dump`.

Resultados esperados quando a chamada realmente
alcançar o seletor:
`ai_console:start`, pelo menos um par
`ai_console:select_enter/leave`, e
`ai_console:decision`. A presença ou
ausência dos hooks passivos `ai_probe`
será um diagnóstico separado.

Uma validação anterior pelo operador mostrou
`FUN_005B1360(1,0)` retornando índice
`1`, carta interna `221`, `allowed=true`,
e execução de `ai step 1` invocando
**Mystic Tomato**. Esse fato é reportado
pelo operador, não é derivado deste JSONL.
