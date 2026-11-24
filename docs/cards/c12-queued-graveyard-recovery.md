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

Apenas Main Phase sem batalha diferida (Flip Summon manual); resposta
em batalha, UI/CPU, seleção de Deck/embaralhamento e prompts unificados
continuam pendentes. MoveToHandCommand é primitiva de GY, não de Deck,
não valida efeitos de carta por si. Testes de lados, classes, duplicatas,
request obsoleta, gate, supressão, GY sem candidatos e reuso da ocorrência.
CI pendente, sem promoção de cobertura.
