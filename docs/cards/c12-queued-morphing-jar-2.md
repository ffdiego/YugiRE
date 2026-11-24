# C12 — Morphing Jar #2 (1163)

Migra o Flip Summon manual opt-in da regra já Done no sandbox.
ReturnToDeckCommand reservado para ocorrência de Monster, exclui
marcados para destruição em batalha, limpa slot sem gatilho de GY
do monstro. Equipamentos vinculados recebem DestroyCommand separado.
Fusion retorna ao Fusion Deck e não entra na cota do Main Deck.

Retorno em ordem de slots lado 0, shuffle se houve retorno ao Main
Deck, lado 1 e seu shuffle. Após ambos os gates, continuação pura
planeja prefixos dos decks embaralhados até encontrar tantos monstros
quanto retornaram. Level 1–4 sem procedimento especial próprio e
com vaga recebe SpecialSummonCommand face-down; demais ocorrências
recebem SendToGraveyardCommand. Monstros inelegíveis contam para
a cota. A execução checa o topo de cada ocorrência reservada; falha
externa indevida interrompe a fila explicitamente. Sem compra, sem
shuffle de lado com cota zero, sem deck-out. Cada movimento e
shuffle aguardam seus gates. LastMorphingJar2 registra movimentos
aplicados; finalização usa o evento original de Flip Summon (a fonte
pode ter mudado de slot ou voltado face-down), sem reexecutar FLIP.

A ordem de movimentação é equivalente ao sandbox em estado final
com mesma seed. Legado removia o prefixo todo antes de colocar os
monstros; fila intercalará remoção do topo e destino final. Não
afirmar paridade de apresentação nativa. Batalha e revelação externa
permanecem legadas, propriedade/controle pendentes.

Testes ambos lados, sementes, deck vazio ou misto, Fusion, cota,
monstros especiais e de nível alto, gate, equipamentos, identidade
do próprio Jar, Royal Command, comparação com legado e eventos.
CI pendente, nenhuma promoção do manifesto.
