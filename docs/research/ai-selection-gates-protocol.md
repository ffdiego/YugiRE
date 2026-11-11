# Piloto Windows — filtros que descartam cartas na IA normal

**Estado:** implementado em 2026-11-10 e **validado dinamicamente no
Joey Windows em 2026-11-11 UTC**, usando um duelo de 890 eventos e sete
seleções normais. Foram observados 26 candidatos, nove rejeições explícitas
em gates, quinze simulações e sete índices reconstruídos com os scores
nativos. O procedimento continua sendo um piloto curto, não o laboratório
em lote. [Evidência evento a evento](../evidence/ai-gates-full-decision-live-validation-2026-11-11.md).

Objetivo: explicar por que as cartas de uma mão podem não chegar a
`FUN_005B1610` (simulação de summon), mesmo quando parecem
invocáveis. Complementa a validação de ranking feita em
[duas decisões reais](../evidence/ai-detail-ranking-live-validation-2026-11-11.md).

## Modo de captura

`trace ai on gates` é um **terceiro modo opt-in**. Preserva:
- `trace ai on`: 4 hooks existentes;
- `trace ai on detail`: 4 + 5 hooks de score;
- `trace ai on gates`: 4 + 5 + **3 hooks adicionais de filtro**.

Os três hooks adicionais são exclusivamente em **entradas de funções** e
somente em chamadas **diretas** de `FUN_005B1360`, com a
thread dentro de um `select_normal` de CPU/lado0.
Outras chamadas dos mesmos helpers (efeitos, UI, outros scripts e
combate hipotético) não geram `ai_gate`. Nenhuma função é invocada,
argumento/retorno alterado, buffer escrito ou novo executável criado.

| Filter | Endereço da função | CALL nativo → endereço de retorno | Retorno que rejeita |
| --- | --- | --- | --- |
| `eligibility_mask` | `005AAB90` | `005B13E0` → `005B13E5` | `returnRaw == 0` |
| `resource_policy` | `005AE3E0` | `005B13FA` → `005B13FF` | `returnRaw != 0` |
| `special_policy` | `005AE220` | `005B1413` → `005B1418` | `returnRaw != 0` |

As três CALL e as instruções seguintes foram conferidas no assembly
`docs/evidence/ai-decision-control-flow.txt` para o EXE conhecido, SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Não reutilizar em outro build com endereços diferentes.

Eventos `ai_gate` são emitidos na saída natural do helper e contêm:
`selectorCallId`, `threadId`, `function`, `functionVa`,
`callsiteReturn`, `argsRaw`, `returnRaw`, `returnSigned`,
`candidateCardIndex`, `handPositions`, `gatePassed` e
`gateVerdict`. As posições na mão são **todas** as que têm o
mesmo índice interno no snapshot de entrada. Se houver cópias
repetidas, não inferir qual cópia o loop estava examinando somente
por esse campo. Para `resource_policy`, `argsRaw` inclui o ID de
recurso original; a identidade interna da carta vem do filtro anterior
na mesma seleção. Um eventual `readError` não deve ser
interpretado como rejeição: o código não chama nem corrige o helper.

**Semântica limitada:** `gateVerdict=reject` comprova que aquele
helper, naquela chamada, rejeitou **esse candidato segundo a política
da IA**; não significa necessariamente jogada ilegal para um humano.
`gateVerdict=continue` só permite chegar ao próximo teste, sem
provar aprovação de toda a cadeia.

A captura **não cobre** diretamente:
`FUN_004026C0` (classe de nível), branch do `pass`,
`FUN_005AECE0` (tributos), todos os predicados internos dos
três helpers, ou seleção final de postura. O modo `gates` inclui os
dados de `detail` para correlacionar as cartas que chegam à
simulação/score/ranking, sem prometer reprodução integral da IA.

Os hooks de `trace prep` e `trace ai on gates` compartilham
`FUN_005AAB90`: a instalação simultânea é proibida e o segundo
modo tentado falha antes de instalar novos hooks. Apenas `--observe-only`
pode iniciar `gates`.

## Procedimento no Windows

Faça backup do save e sincronize:

```powershell
git pull --ff-only
node .\native_rules\tests\test_loader.cjs
python -m unittest discover -s native_rules/tests -p test_console_commands.py
```

Abra `joey_pc.exe` manualmente, depois anexe:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe .\native_rules\native_rules.py --observe-only
```

No console:

```text
trace on 20000
trace ai on gates
trace mark ai-gates-primeira-decisao
```

Espere **uma decisão natural de colocação da CPU** em Single Duel
normal. Não usar `trace prep`, `trace deck`, `trace demo`,
comandos mutadores ou overrides de velocidade nessa sessão. Ao
observar a ação, execute:

```text
trace ai off
trace off
trace dump
stop
```

Esperado após ativar: `aiGateActive=True`,
`aiDetailActive=True`, `aiHooks=12`. Envie o JSONL gerado.
Se `overwritten > 0` ou houver `readError`, classificar a
decisão como incompleta, não preencher dados ausentes.

Se o jogo travar, **não repetir de imediato**. Preserve EIP/minidump,
informações do Windows Event Viewer, console e qualquer JSONL existente;
os novos hooks foram testados em fixtures, **não em Windows real**.

## O que comparar com a captura anterior

1. Para cada seletor de CPU, agrupar `ai_gate` pelo
   `selectorCallId`, mantendo ordem e endereço do caller.
2. Conferir índice interno e `handPositions` com a mão na entrada.
3. Observar o **primeiro filtro rejeitante** de cada carta que não
   chegou a `simulate_card`.
4. Para aprovadas, associar `simulate_card`, score, AA/AC e
   resultado de `NativeAiRanking`.
5. Se uma carta aparentemente aprovada não chegou à simulação, marcar
   como **pendente** e investigar classe, `pass` e tributos — não
   deduzir aprovação final por ausência de mais eventos.

Resultado desejado: sair de três comparações de candidatos
verificadas para **uma decisão reproduzida a partir de todas as cartas
da mão**, sem utilizar regras aproximadas.
