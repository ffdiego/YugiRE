# C12 — FLIP deck flow P082/P088

Needle Worm 561 e Hiro's Shadow Scout 610, já Done. A fila compartilha
o lote entre Flip Summon manual e revelação em batalha. Royal Command
suprime a resolução. RevealMonsterByEffect e batalhas fora do recorte
admitido continuam legados.

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
movimentos aplicados. FinishFlipDeckFlowCommand publica a auditoria
e encerra o efeito antes de FinishFlipBatchCommand (Flip Summon)
ou FinishBattleFlipCommand (batalha). No combate, o dano e a marcação
precedem o lote, e a remoção do defensor marcado ocorre após a
finalização, inclusive após a derrota por compra insuficiente.
Testes ambos lados, decks 0..6,
gate, Royal Command, identidade, eventos, final state legado,
ordem de compras e descartes, Exodia versus shortfall e primitiva top-only.
Os casos de batalha cobrem ambos os lados, decks 0/2/6, manutenção
da carta marcada durante o gate e a preservação de uma cópia antiga
da magia na mão. A validação é de cenários C#.
O catálogo lista `00585E40` e `00586260`; as VAs não apareceram
nas quatro tabelas de decompilação e instruções do SQLite v7.
A comparação dinâmica com Joey segue pendente.
