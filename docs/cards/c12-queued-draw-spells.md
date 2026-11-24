# C12 — Magias de compra na fila

Recorte dos dois IDs já Done (1010 Pot of Greed, P119; 1065 Upstart
Goblin, P143). A regra existente DrawSpellRules.Effect é a única fonte
de quantidades. Não há novos IDs nem alegação de paridade nativa.

TryQueueDrawSpell valida mão/Set, ocorrência, fase, zona livre se
mão, deck suficiente e fila ociosa. ActivateDrawSpellCommand repete
a validação antes de emitir SendToGraveyardCommand. A continuação
só produz DrawCommand/HealCommand se o gasto tiver sido aplicado.
A mesma rotina de envio de magias setadas dos LP fixos é ampliada
estritamente aos dois IDs. Um comando por compra, preservando ordem
do deck, HandTransferred e draw normal do turno. Upstart cura o
oponente depois da compra. Retirar a última carta não é deck-out;
a próxima tentativa em deck vazio perde no sandbox.

ResolveDrawSpell legado conclui as compras e cura mesmo se Exodia
for detectado em uma compra intermediária. A fila preserva apenas
esse lote reservado, não permite ações posteriores arbitrárias.
Essa política é de compatibilidade C#, não uma prova de timing Joey.
Não modela respostas, negação ou alteração de deck por correntes.
UI/CPU ainda legados; integração com A04/A06 continua pendente.

Testes: ambos os lados, mão/Set, gate, insuficiência de deck, gasto
obsoleto, última carta, Exodia e ordem dos comandos. CI pendente.
