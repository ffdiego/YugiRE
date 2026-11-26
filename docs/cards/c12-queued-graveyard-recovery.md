# C12 — Recuperação do cemitério

P018: 427 Magician of Faith (Spell, classe 22) e 101 Mask of Darkness
(Trap, classe 21), já Done. Reutiliza CardSelectionRules.FlipGraveyardFilter
e IsEligible. Não amplia efeitos nem afirma paridade nativa.

DemoCardOccurrences passa a capturar/resolver Graveyard por ocorrência
rastreável, inclusive cópias iguais e deslocamento de índices.
QueuedGraveyardRecoveryCandidates projeta somente cartas da solicitação
original que permanecem no próprio GY e passam no filtro. Resposta usa
a referência exata de PendingCardSelection, não só o ID da carta efeito,
e identidade CardTarget escolhida. Outros prompts continuam bloqueados.
ResolveGraveyardRecoveryCommand revalida e consome a solicitação;
MoveToHandCommand move uma ocorrência de GY para mão, sem compra,
sem shuffle, sem efeitos de destruição. FinishGraveyardRecoveryCommand
libera outras solicitações após o gate de movimentação. Royal Command
suprime antes da resolução, inclusive quando ativado entre admissão e
executor. Nenhuma chamada de FinishDeferredBattle a partir da fila.

Na batalha enfileirada, a revelação abre a mesma escolha após o
cálculo de dano. A ocorrência marcada fica no campo até terminar
o gate de MoveToHandCommand. Sem candidato ou com efeito suprimido,
a remoção segue automaticamente. App e CPU usam o mesmo seletor.
Os testes C# cobrem lados, classes, duplicatas, identidade, ausência
de candidato e gates. A comparação dinâmica e configurações de
batalha fora do piloto seguem pendentes.
