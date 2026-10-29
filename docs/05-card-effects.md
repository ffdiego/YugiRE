# Efeitos das cartas — questão em aberto

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
