# C09 — Invocação Ritual P269 pela fila

A admissão `DemoDuel.TryQueueRitualSummon` recebe a ocorrência da magia na mão ou Set, o monstro correspondente na mão, os materiais escolhidos, o destino e a posição face-up. A tabela compartilhada com P165 usa os 16 pares e níveis recuperados em `005ED060`. Oito pares constam do índice de 771 recursos.

O sandbox aceita materiais próprios da mão e do campo. A soma precisa atingir o nível da receita. Remover qualquer material escolhido deve deixar a soma abaixo do requisito, conforme o critério em `00599570` e `005995E0`. Excluir apenas a ocorrência preservada permite usar outra cópia. Com o campo cheio, o destino deve ser liberado por um dos materiais e o primeiro tributo precisa sair do campo.

A fila revalida a escolha antes de gastar a magia, emite um `TributeCommand` por ocorrência, executa consequências vinculadas e só admite a colocação do monstro reservado após todos os pagamentos. Cada comando respeita seu gate. A colocação preserva a ocorrência, aceita ataque ou defesa face-up e não consome a invocação normal.

O catálogo, a seleção e os pontos de entrada nativos constam no [contrato Ritual](ritual-bindings.md) e no snapshot privado v3. Os testes em `QueuedRitualSummonChecks.cs` executam os 16 pares em ambos os lados, mais Set, materiais mistos, cópias, seleção redundante, destino invalidado e espera por apresentação. A suíte opcional usa as cartas do DAT com o fingerprint registrado no índice de packs.

Escopo headless C# sujeito ao CI. O console usa a mesma admissão, por exemplo `ritual hand 0 1 2 defense hand 2 field 0`. A UI de duelo e a CPU ainda não produzem a escolha Ritual. Soul Exchange, Mask of Restrict, Ground Collapse, respostas, correntes, restrições de reviver e efeito próprio de Relinquished ficam pendentes. A semântica de ordem e timing precisa de comparação em duelo nativo antes de Verified.
