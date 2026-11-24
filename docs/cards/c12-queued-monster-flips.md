# C12 — FLIPs seletivos de monstros

500 Man-Eater Bug (destruir um), 540 Hane-Hane (retornar um),
601 Penguin Soldier (retornar zero a dois). IDs e filtros da
implementação existente FlipEffectRules. Sem nova promoção de status.

O FlipCommand/ResolvePositionCommand já abria PendingFlipTarget.
QueuedMonsterFlipCandidates projeta ocorrências nos dois lados, incluindo
a própria fonte se elegível. Bounce exclui marcados para destruição em
batalha. TryQueueMonsterFlipAnswer recebe fonte e até dois alvos; só
admite resposta manual em Main Phase, sem batalha diferida ou outros
prompts concorrentes. Exceção privada e temporária na admissão do
scheduler para essa resposta, sem abrir entrada genérica enquanto há
um prompt. PendingFlipTarget é consumido no executor, nunca na admissão.
Royal Command já ativo aceita resposta vazia, sem mover cartas.
Validação de ocorrência e quantidade repetida antes do consumo.

ReturnToHandCommand usa ocorrência de monstro, com retorno ao Fusion Deck
para Fusion e preservação de ocorrência na mão para Main Deck. Reutiliza
ClearMonsterSlot e ReturnMonsterToHand, mas separa comandos Destroy dos
equipamentos vinculados. Sem efeitos de ida ao GY do monstro (ex. Immortal
of Thunder). DestroyCommand reutiliza gatilhos e adiamento de buscas.
Lote permite concluir remoções reservadas após Exodia conforme sandbox.
Finisher libera escolhas de cemitério após o último gate. Se uma mutação
externa inválida quebrar vínculo reservado, fila falha explicitamente,
como no piloto Raigeki.

Battle FLIPs ainda usam ResolveFlipTarget/FinishFlipTargets existentes,
sem chamar FinishDeferredBattle a partir da nova fila: Damage Step e
seus gatilhos de batalha precisam migração própria (A06/A07). Consulta
read-only respeita IsMarkedForBattleDestruction inclusive em batalha.
UI/CPU e prompt bloqueante seguem legados. Não há alegação de paridade
com timing nativo. Testes de ambos lados, zero/um/dois, duplicatas,
fonte/alvo, gate, equipamento, Immortal, Sangan e batalha. CI pendente.
