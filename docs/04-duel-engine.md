# Motor de duelos — primeiros dados de cartas

Esta rodada não identifica estruturas de jogador, campo, fases ou resolução. Não foi reconstruído nem implementado motor algum.

Pistas observadas: caminhos de recursos como `duel/card/deck.txt` no executável; possível sequência de IDs de 16 bits em deck.ydc. Confiança alta na presença dos bytes; baixa quanto à relação com representação em memória. Recursos de interface não demonstram implementação das regras.

Após confirmar leitores e IDs, seguir a transformação entre deck carregado e estado em memória, documentando acessos e limites antes de propor estruturas C.

## Quarta rodada: propriedades estáticas

CARD_Prop.bin contém 1115 palavras de 32 bits, com ATK/DEF compactados e outros campos ainda parcialmente interpretados. Acessor 0x004025F0 usa índice mascarado em 11 bits; ATK em 0x004027B0 e DEF em 0x00402800, identificados pela apresentação na interface 0x0041B3E0. [Layout, exceções e evidências](02-file-formats.md).

Isso confirma dados estáticos de propriedades, não o estado mutável de uma carta em duelo. 0x00418B10 recebe um índice e armazena ATK em objeto+0x4C e DEF em objeto+0x50, após guardar o índice em +0x10. O objeto ainda não foi identificado como carta do campo; pode pertencer à apresentação. [Pseudocódigo](evidence/card-property-ui.txt).

CARD_ID e CARD_IntID possuem acessores distintos, sugerindo conversões entre domínios. A relação desses índices com os números encontrados em deck.ydc ainda não foi confirmada. Nenhuma estrutura global de duelo foi definida ou renomeada.

## Quinta rodada: índices e IDs

As duas tabelas confirmam conversão entre índice de propriedades e um ID de recurso: 1114 associações passam no roundtrip, além do índice sentinela zero. CARD_ID tem 1115 palavras; CARD_IntID tem 2048 palavras, das quais os acessores usam as primeiras 2000. Nove IDs acrescidos de 2000 selecionam o índice seguinte ao do ID base. [Detalhes e limites](02-file-formats.md).

Isso estabelece os índices estáticos usados pelos getters, sem ainda provar o formato do deck ou uma representação completa da carta em duelo. ID de recurso zero é válido e mapeia ao índice 816, enquanto índice zero contém ID 0xFFFF.

Dois consumidores (0x00570BE0/0x00570C70) convertem IDs constantes e comparam índices com palavras em uma região global. Observa-se seleção de um dos dois blocos com stride 0xD44; não foi determinado o significado dos blocos, e não foram declarados como jogadores/campos nesta rodada.

## Sexta rodada: deck em arquivo e em memória

O leitor/escritor 0x00449EF0 confirma IDs de recurso no arquivo e índices de propriedades em sua estrutura de destino. O objeto contém três arrays com capacidades 80/15/15 e contadores em +0xDC/+0xE0/+0xDE. A ordem das listas no arquivo não é a mesma da disposição em memória. Ainda não é a estrutura do estado do duelo; é um destino de serialização de deck.

Na amostra: 42 cartas na lista principal, uma carta na segunda lista e nenhuma na terceira. Nomes confirmados via tabela inglesa; todos os 43 IDs convertem e voltam ao valor original. [Layout e evidências](02-file-formats.md). Oito bytes do cabeçalho permanecem parcialmente desconhecidos; não foi implementada gravação nem alteração do deck.
