# Efeitos das cartas — questão em aberto

## Descarte como etapa de preparação

[Tribute e callback compartilhado](cards/tribute-discard-cost.md) separam
disponibilidade (+12), preparação do descarte (+16), alvo (+20) e resolução
(+4). O descarte usa descritor tipo 2 e comando 0x8E, com compactação da mão.
Quantidades 1/2/5 são selecionadas por ID no mesmo callback. A condição
0x00583510 da resolução trata de Big Shield Gardna; não cobra o descarte.

## Destruição por efeito até a escrita de estado

A [continuação de Man-Eater Bug](cards/monster-destruction.md) liga
0x00576960/0x00576B30 ao comando 0x79 e seu consumidor 0x00404B40.
A origem de campo é zerada e a palavra da carta é inserida na região 14;
o desvio de Banisher seleciona a 15. As quatro fichas de ID 0x780–0x783
não são inseridas nessas listas. Dependências têm preparação e emissão
recursivas separadas; regras completas de proteção e ordem de eventos pendentes.

## Seleção e revalidação de alvo

[Man-Eater Bug](cards/man-eater-bug-targeting.md) usa o slot +8 para filtrar
candidatos e compartilha resolução/+20 com Tribute to The Doomed. O alvo em
pedido+6 contém lado, posição e chave de oito bits. 0x00583470 confere contador,
ocupação e chave na posição antes de a resolução emitir a operação por máscara.
O custo de Tribute foi seguido no estudo acima; a matriz completa de
interações continua aberta.

## Comparação estática: Sparks e Ookazi

[Duas cartas de dano simples](cards/sparks-ookazi.md) compartilham os callbacks
0x00584E60 e 0x00593CC0. A seleção por ID dentro do callback escolhe 200/800,
depois usa o mesmo emissor 0x005784E0 identificado para Parasite.
Isso confirma uma combinação de tabela de callbacks, código compartilhado e
condições por ID. Não há evidência de uma função exclusiva por carta nesses casos.

## Caso funcional concluído: Parasite Paracide

Ver [análise consolidada](cards/parasite-paracide.md). O caso liga callbacks
por ID, pedidos de 20 bytes, comandos de oito bytes e modificadores dinâmicos.
Foram seguidos embaralhamento, colocação, -1000 LP e classe Insect. É uma
conclusão específica de análise estática; a arquitetura de todos os efeitos
continua aberta. As observações cronológicas abaixo são histórico.

Décima primeira rodada: [pedido de 20 bytes e transporte da chave](14-parasite-request-key.md)
confirmados. A emissão com 0x365F preserva a chave de uma palavra de estado,
mas seleciona outro ramo do callback de Parasite Paracide. Criação e limpeza
não foram demonstradas como consequências da mesma chamada.

Décima rodada: [registro por ID e callback de Parasite Paracide](13-parasite-creation.md)
confirmados. A busca 0x0059DBE0 converte índice→ID e localiza registro de
24 bytes; 0x0059DEB0 chama o ponteiro em +4. Para ID 762, chega a 0x005875A0,
cujo ramo examinado cria associação de tipo 13. Há evidência de callbacks
selecionados por carta nesse caminho; não foi provada arquitetura exclusiva
para todos os efeitos nem reconstruído o comportamento completo dessa carta.

Oitava rodada: o caminho de Parasite Paracide chega a uma limpeza concreta
da associação via comando 0x4D. [Produtor, fila, dispatcher e duas escritas](11-command-4d.md).
A existência desse dispatcher de comandos não resolve a arquitetura geral
de efeitos. Criação da associação com nibble 13 e significado do vínculo
continuam em aberto. As rodadas abaixo registram o progresso anterior.

Nenhuma evidência suficiente nesta rodada para distinguir funções por carta, tabelas, IDs de comportamento, bytecode ou combinação. Arquivos `.txt` citados em strings não comprovam scripts de efeitos.

Plano futuro: estabelecer IDs com uma tabela verificável; seguir os consumidores; comparar duas cartas com comportamentos distintos; localizar dispatch por ID, tabelas de ponteiros ou loop de interpretação. Registrar também evidência contrária a cada hipótese. Nenhum endereço de efeito foi atribuído.

Quarta rodada: CARD_Prop.bin é tabela de propriedades compactadas, incluindo ATK/DEF; não há evidência de bytecode de efeitos nela. O discriminante de classe e campos que se sobrepõem conforme a classe não bastam para inferir um identificador de efeito. As funções 0x004025D0/0x00402700/0x00402740 são acessores de propriedades, não resolvedores de efeito comprovados. Próxima trilha: confirmar correspondência de IDs antes de seguir os chamadores das regras.

Quinta rodada: conversão entre IDs e índices validada nas tabelas. 0x00570BE0 e 0x00570C70 usam IDs constantes 0x2FA e 0x443, convertendo-os e comparando índices em registros globais. É evidência de condições dependentes de IDs escritas no código, mas não identifica efeito, tipo de decisão ou arquitetura geral de resolução. Evitar concluir prematuramente que há uma função por carta. [Evidência](evidence/card-id-instructions.txt).

Sexta rodada: a tabela inglesa associa esses IDs a **Parasite Paracide** e **Graverobber**, respectivamente. [Relatório de nomes](evidence/deck-name-validation.json). Próxima investigação pode seguir callers de apenas uma dessas funções para descobrir o significado dos registros comparados e a relação com regras. Nomes iguais nos pares ID/ID+2000 observados não provam que o +2000 represente estado de efeito; essa hipótese continua aberta.
