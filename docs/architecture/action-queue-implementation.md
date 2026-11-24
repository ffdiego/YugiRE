# A02.1 — Fila tipada no Joey.Core (parcial)

Implementa infraestrutura descrita em [A01](action-queue.md), sem substituir ainda os caminhos legados. Não há mudança de comportamento nas entradas atuais de DemoDuel, App ou CPU.

- ActionQueue: FIFO, registro ativo separado, gate global e de conclusão, no máximo um novo comando por Tick. Mutação do executor uma vez. EnqueueBatch valida argumentos estruturalmente antes de admitir. Trace de admissão/início/execução/fim, inclusive resultados Skipped/Rejected. Exceção no executor/gate/continuação deixa fila faulted; nunca repetir uma mutação possivelmente parcial.
- DuelCommand: tipos Draw(1), Summon, Set, SendToGraveyard, Discard, Destroy, Damage, Heal, Flip, ChangePosition. Identidade CardOccurrenceId independente de resource ID, posição e referência de catálogo; a resolução e a alocação no estado ainda precisam de adaptação. Side de movimento identifica o lado da ocorrência. Slots são destinos explícitos.
- IPresentationGate: contrato headless imediato ou gate manual. Sem relógios. Origem só no envelope/trace; não chega ao executor. A autorização de jogador/CPU/lab **não** está implementada aqui; Enqueue significa admitir trabalho já autorizado.
- Continuação: executor pode anexar comandos ou agendar uma continuação; ela retoma em Tick separado, após esvaziar FIFO e liberar gate. Reutiliza PendingEffectDecision / EffectDecisionAnswer como dados, sem migrar queuedEffectDecisions do DemoDuel ainda. Um passo de decisão não pode emitir comandos ao mesmo tempo. SubmitDecision valida ID e resposta e apenas registra a escolha. As continuações devem emitir comandos, nunca mutar estado diretamente.
- Piloto explícito DemoDuel.CreatePrimitiveActionQueue: Draw(1) de efeito, Damage, Heal por meio de DrawToHand/ChangeLife, conservando verificações Exodia/LP e eventos existentes. Não é a compra normal do turno, não dispara Standby. Draw sem deck perde no sandbox. Rejeita comandos não migrados e ações com duelo encerrado/decisão legada pendente. Não usar simultaneamente com chamadas legadas; não é API pública de admissão legal nem console. Outros produtores não usam esta fila ainda.

## Validação

ActionQueueChecks está registrado no runner Joey.Tests. Inclui admissão atômica, gates, FIFO, três destruições (executor de teste), ocorrência obsoleta, continuação/decisão, falha sem retry, e igualdade de trace lógico/estado/eventos Draw/LP no DemoDuel real com gate imediato e retardado. O executor de teste não demonstra paridade de regras de destruição. Executar:

```sh
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests
python scripts/publish_actions_progress.py --check
```

Não executado neste ambiente (SDK .NET indisponível, sem checkout local completo). A02 continua Parcial, badge permanece 1/8; A03–A06/A08 continuam bloqueados.

## Próxima fatia

Adaptar estado físico das ocorrências. O DemoDuel hoje reutiliza objetos Card do catálogo para cópias, e arrays/listas legados permitem mutação por fora. Não simular IDs via ReferenceEquals/slot; implementar IDs estáveis por ocorrência e validar destino/estado no executor. Depois migrar uma família de remoção real (ex. Raigeki), separar destruição de descarte/mero envio, equipamento e gatilhos posteriores em comandos/continuações, com testes de três alvos e de substituição do slot. Preservar regras legadas para caminhos ainda não migrados; não usar um segundo modelo de duelo em produção. Só liberar A02 quando os critérios de sequência forem executados e a integração necessária estiver explícita. A07 e a coleta do script 11 são independentes.
