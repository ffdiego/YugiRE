# C12 — P165 busca Ritual → mão

Sonic Bird (1122) e Senju of the Thousand Hands (1109), já Done.
Reusar RitualSearchRules.OnNormalOrFlipSummon e o filtro fechado de
16 pares Ritual Spell/Monster. Não implementar Ritual Summon.
O produtor existente só abre pedido na Normal/Flip Summon, não Set,
reveal por efeito ou batalha. Resposta usa o mesmo TryQueueDeckSearch
de P006, com CardTarget?; null aceita somente MinSelections=0, P006
continua obrigatório. Request exato, candidato por ocorrência e
filtro novamente conferidos. ReserveDeckTransfer vazio não consome
RNG; seleção aceita emite MoveToHandCommand e ShuffleDeckCommand
uma vez, finaliza após gate. Testes dos dois lados, ambos IDs,
Normal/Flip Summon, recusa, cópias, filtros e paridade de estado
final com seed. UI/CPU ainda legados. CI pendente; sem promoção.
