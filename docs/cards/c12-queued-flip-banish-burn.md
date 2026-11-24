# C12 — P051/P052 banimento e dano

Lady Assailant of Flames (1435/P051) e Fire Sorcerer (1436/P052)
já Done no sandbox. Ambos compartilham o callback de dano 00584E60;
rotinas de custo nativas continuam pendentes de roteiro 07. Esta
fatia não reivindica equivalência do RNG, timing ou novos status.

BanishCommand recebe uma ocorrência de Hand ou topo exato de Deck
e move para Banished, sem compra, descarte, GY ou deck-out. Captura
de ocorrência passa a suportar a zona Banished. Outros tipos de
banimento (campo/GY) ainda não migram suas consequências nesta fatia.

Na resolução manual, conferir quantidade antes de consumir RNG.
Lady seleciona três do topo, Fire Sorcerer sorteia duas ocorrências
sem reposição. Ambos usam a mesma rotina SampleHandIndices do
sandbox para o sorteio, com seed idêntica. Capturar todas as
ocorrências relevantes antes de revelar e novamente antes do sorteio.
Uma movimentação por comando. A continuação só admite DamageCommand
800 quando todas as movimentações foram aplicadas e seus gates
concluídos. Se custo inicialmente insuficiente não há movimento,
RNG ou dano. Alteração externa indevida durante lote pode interromper
pagamento, sem rollback de movimentos já efetuados e sem dano.
Finalização registra LastFlipBanishBurn, inclusive dano letal.
Royal Command segue guarda comum; FLIP em batalha e reveal externo
continuam legados. UI/CPU ainda não migrados.

Testes: ambos donos, quantidade 0..5, seed/ordem de banimento e mão,
duplicatas, identidade na zona Banished, gates, insuficiência, dano
letal, Royal Command, primitiva não-topo/obsoleta e RNG subsequente.
CI pendente. Sem promoção do manifesto.
