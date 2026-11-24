# C12 — Buscas Deck → mão

P006: Sangan (47), Witch of the Black Forest (573), ambos Done.
Reusar CardSelectionRules.OnFieldToGraveyardFilter/IsEligible, ATK/DEF
até 1500 respectivamente. Sem inferir outros efeitos a partir do perfil.

DemoCardOccurrences inclui Deck. IDs são per-duel; capturar índices
atuais, preservar a identidade ao mover ou embaralhar. A projeção
QueuedDeckSearchCandidates filtra somente ocorrências da solicitação
original no próprio deck. Consumidor autorizado deve ocultar lista
e ordem do deck do oponente. TryQueueDeckSearch admite resposta da
solicitação exata, mesmo quando seu dono não é jogador ativo.
A admissão de outras ações segue bloqueada por pending choice.

ResolveDeckSearchCommand revalida, consome a escolha e agenda
MoveToHandCommand. O comando remove só a ocorrência escolhida,
sem consumo de RNG, mantendo ordem relativa do restante. A continuação
agenda ShuffleDeckCommand apenas se movimento aplicado, seguido
de FinishDeckSearchCommand. Shuffle usa o mesmo effectRandom.Shuffle
do sandbox; último card retirado não é deck-out e não emite evento
de compra. O lote reservado permite shuffle mesmo se a transferência
completar Exodia. Finalização abre próxima escolha, se houver.

Diferença explícita de apresentação: ResolveCardSelection legado
embaralhava o restante antes de adicionar a carta à mão. A fila
movimenta antes de embaralhar. Ordem final do deck e RNG preservados
com mesma seed; timing original não demonstrado. Sem claim de
paridade de evento/animacao nativa. Battle/turn pipeline, buscas
opcionais, recrutamento Deck → Field e UI/CPU ficam pendentes.

Testes: ambos os donos, filtros, duplicatas, pedido obsoleto,
gates, equivalência do deck final com seed, buscas consecutivas
após Dark Hole, último card, movimento primitivo. CI pendente.
Sem promoção de cobertura.
