# C12 — FLIP deck flow P082/P088

Needle Worm 561 e Hiro's Shadow Scout 610, já Done. Migração
opt-in da resolução após Flip Summon manual. Não tocar no produtor
de batalha nem em RevealMonsterByEffect. Royal Command permanece
na guarda compartilhada. Sem nova evidência ou promoção de status.

P082 reserva até cinco ocorrências do topo do deck adversário,
emite um SendToGraveyardCommand por ocorrência. Implementação da
primitiva Deck limitada ao topo exato, sem consumo de mão, evento
de compra ou deck-out. Não introduz gatilhos C07 ainda pendentes.

P088 reserva até três compras disponíveis. DrawCommand executa
a compra sob contexto do lote, registra a ocorrência transferida
e só as Spells classe 22 recém-compradas entram na continuação
de DiscardCommand. Todas as compras precedem os descartes.
A finalização após o último gate atualiza LastDeckFlow, verifica
Exodia e aplica a derrota por shortfall se não houver vencedor,
preservando política do sandbox. Os novos comandos emitem eventos
HandTransferred para apresentação, ao contrário do helper legado
P088, que era silencioso. Não afirmar paridade de animação nativa.

O lote compara ocorrências esperadas de topo; mudança externa
indevida não autoriza consumir substitutos. Contagens auditam
movimentos aplicados. Finalização reaproveita FinishFlipBatchCommand
sem chamar TriggerFlip novamente. Testes ambos lados, decks 0..6,
gate, Royal Command, identidade, eventos, final state legado,
ordem de compras e descartes, Exodia versus shortfall e primitiva top-only.

Bistro Butcher e Masked Sorcerer estão em DealMonsterBattleDamage
e dependem de migrar o dano de batalha (ACTIONS). Não criar segundo
produtor paralelo. Batalha FLIP e UI/CPU ainda legados. CI pendente.
