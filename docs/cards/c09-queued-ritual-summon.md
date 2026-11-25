# C09 — Invocação Ritual P269 pela fila

A admissão `DemoDuel.TryQueueRitualSummon` recebe a ocorrência da magia na mão ou Set, o monstro correspondente na mão, os materiais escolhidos, o destino e a posição face-up. A tabela compartilhada com P165 usa os 16 pares e níveis recuperados em `005ED060`. Oito pares constam do índice de 771 recursos.

O sandbox aceita materiais próprios da mão e do campo. A soma precisa atingir o nível da receita. Remover qualquer material escolhido deve deixar a soma abaixo do requisito, conforme o critério em `00599570` e `005995E0`. Excluir apenas a ocorrência preservada permite usar outra cópia. Com o campo cheio, o destino deve ser liberado por um dos materiais e o primeiro tributo precisa sair do campo.

A fila revalida a escolha antes de gastar a magia, emite um `TributeCommand` por ocorrência, executa consequências vinculadas e só admite a colocação do monstro reservado após todos os pagamentos. Cada comando respeita seu gate. A colocação preserva a ocorrência, aceita ataque ou defesa face-up e não consome a invocação normal.

O catálogo, a seleção e os pontos de entrada nativos constam no [contrato Ritual](ritual-bindings.md) e no snapshot privado v3. Os testes em `QueuedRitualSummonChecks.cs` executam os 16 pares em ambos os lados, mais Set, materiais mistos, cópias, seleção redundante, destino invalidado e espera por apresentação. A suíte opcional usa as cartas do DAT com o fingerprint registrado no índice de packs.

O console usa a mesma admissão, por exemplo `ritual hand 0 1 2 defense hand 2 field 0`. A CPU em modo fila também produz a escolha Ritual. O App em modo fila também aceita ativação de mão ou Set e permite clicar os tributos próprios que ainda completam um custo legal. O clique direito alterna ataque e defesa aberta, e Backspace cancela. O último tributo admite o lote com a primeira vaga ou, no campo cheio, na primeira zona liberada. Controles e prompt são provisórios. Soul Exchange, Mask of Restrict, Ground Collapse, respostas, correntes, restrições de reviver e efeito próprio de Relinquished ficam pendentes. A semântica de ordem e timing precisa de comparação em duelo nativo antes de Verified.

A sugestão da CPU percorre as magias da mão e Set, conserva a primeira
ocorrência do monstro Ritual e procura um conjunto suficiente de materiais
por subtotais de nível e presença de tributo no campo. Ordenar os níveis
em ordem decrescente garante que retirar o último material escolhido
devolve o subtotal a um valor abaixo do requisito. Com campo cheio, o
primeiro comando de tributo libera a zona escolhida. Esse desempate e a
posição de ataque são políticas do sandbox. Testes C# exercitam CPU nos
dois lados, mão/Set, duplicatas, campo cheio/vaga livre e espera por gate.
