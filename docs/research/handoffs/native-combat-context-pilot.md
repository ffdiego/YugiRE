# Handoff S2 — piloto combat-context preparado offline

Data: 2026-11-11. Branch `work/native-analysis`.
Contrato S1: `eeb75ac`. Implementação S2: `824b444`
(`feat(native): prepare bounded passive combat-context pilot`).

## Objetivo e arquivos

Produzir contexto novo sem chamadas artificiais nem patch de estado. Entregues:

- `native_hooks/yugire_dual_loader.js`: modo RPC `traceaistart('combat-context')`,
  dez listeners de entrada de função e retornos naturais; janela de seletor,
  stack por thread, IDs próprios, filtros por return VA, limite 64 confrontos e
  24 filhos, null em falhas, proteção contra callbacks após parada/rearme.
- `native_hooks/patch_dual_rules.py`: comando opt-in, ajuda e status.
- Testes em `native_hooks/tests/test_loader.cjs`, `test_console_commands.py` e
  `test_combat_context_preflight.py`.
- [Procedimento Windows](../../../native_hooks/README.md#piloto-s2-contexto-passivo-de-combate-preparado-offline)
  e [contrato](../ai-combat-observation-contract.md).

Não houve alteração da trilha T, do Python legado yugilab, de PLANS.md ou AGENTS.md.
Não houve push. Nenhum EXE/DAT/save/replay foi escrito.

## Evidência e testes executados

Artefato local original validado por SHA256:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

| Comando | Resultado |
| --- | --- |
| `python3 -m unittest discover -s native_hooks/tests` | 86 testes, OK; inclui conferência real dos CALLs no PE local, sem skip |
| `node native_hooks/tests/test_loader.cjs` | PASS; novos mocks cobrem wrappers, seed, nulls, slots inválidos, threads, aninhamento, callers, limites, rollback, parada e exclusão de modos |
| `node native_hooks/tests/test_deck_trace.cjs` | PASS |
| `node native_hooks/tests/test_demo_rng_seed.cjs` | PASS |
| `git diff --check` | PASS |

As mensagens argparse de rejeição são saídas esperadas dos testes negativos.
Os mocks verificam instrumentação e preservação da memória simulada, não
regras do jogo. O teste PE é estático; não executa helpers. Os testes C++ e .NET,
a suíte yugilab e o piloto Windows não foram executados nesta unidade.

## Riscos e limites

Não validado no Windows. Os hooks acrescentam overhead; ausência de chamadas
artificiais não prova ausência de interferência temporal. Snapshots são leituras
síncronas, sem garantia atômica entre threads. A postura é da janela de avaliação,
potencialmente hipotética. `resourceId` dos snapshots permanece null, com os
retornos de conversão em registros filhos separados. Ausência de predicado por
curto circuito não é retorno zero. Não copiamos listas brutas de vínculos.

A captura permite testar a fórmula usando stats e consultas nativas observadas;
a independência do cálculo de efeitos continua uma entrega futura.
O limite 64 deixa listeners ativos, mas para de ler novos contextos; o buffer de
comandos/seletor continua limitado por 20000. Pares interrompidos são incompletos.
Nenhuma divergência 304/319 foi resolvida; **0 confrontos completos novos observados**.

## Dependência exata e próximo experimento

**Parada no gate M3/Windows de PLANS.md.** S3 depende do JSONL original de um
piloto natural de 1–3 duelos, inicialmente um, após revisão da implementação.
S4 depende de observação que discrimine hipóteses de Goblin Attack Force; ainda
não existe essa evidência nesta unidade. Não iniciar investigações especulativas
para contornar esses gates.

Depois da revisão, o operador deve abrir o Joey original no Windows e, na pasta
native_hooks com o ambiente Frida existente, executar:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --observe-only --pid <PID>
```

No console: `trace on 20000`, `trace ai on combat-context`, jogar um duelo
natural curto, `trace off`, `trace status`, `trace dump`, `stop`.
Não usar seed/deck/speed/mutex overrides, trace demo ou prep. Interromper no
primeiro problema. Preservar retorno normal do jogo e relato de anomalias.

Enviar por canal privado: JSONL original, SHA256 do JSONL, fingerprint/atestado do
EXE, contadores de integridade e relato do operador. Auditar sequência, perdas,
readErrors e pares com dois stats antes de rodar a comparação. O trace bruto
não deve ser commitado. Este handoff não promete execução após o fim do turno.
