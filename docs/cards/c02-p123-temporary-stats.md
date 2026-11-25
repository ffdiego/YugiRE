# C02.9 — Atributos temporários P123

| ID | Carta | Origem | Delta |
| --- | --- | --- | --- |
| 1015 | Reinforcements | Armadilha | ATK +500 |
| 1016 | Castle Walls | Armadilha | DEF +500 |
| 1075 | Rush Recklessly | Quick-Play | ATK +700 |
| 1076 | The Reliable Guardian | Quick-Play | DEF +700 |
| 1082 | Snake Fang | Armadilha | DEF −500 |

O DAT fixa as classes e a duração durante o turno da ativação. O consumidor nativo `0056DD30` confirma os deltas. A coleta Ghidra 30 recuperou o candidato compartilhado `0057AE20`. O listing exige posição de monstro ocupada e face-up, chama `0056C510` e acrescenta `0056C460` para fonte de classe 22. Para esses dois helpers, o sandbox usa o tipo Dragon do DAT quando Lord of D. está ativo, e os IDs 1326/1329 quando Umi está ativo. Supressão dinâmica por outras cartas e cálculo de tipo alterado seguem pendentes.

O Core recebe ocorrências exatas de fonte e alvo. Admite Quick-Play da mão do jogador ativo na Main ou Battle, e Quick-Play ou armadilha de qualquer lado que já estivesse Set em turno anterior. O recorte cobre janelas ociosas entre ações. Bloqueia armadilhas sob Jinzo ou Royal Decree, e alvos marcados para destruição de batalha. O console do jogador ativo usa `activate hand 0 1 monster 0` ou a variante `set`. O parâmetro `CardTarget` do Core admite a origem Set adversária.

A fila gasta a fonte por comando e revalida o alvo antes de aplicar o delta. O modificador acumula com outros deltas uma única vez antes do clamp, inclusive no combate enfileirado. Sai quando o alvo deixa o campo, mesmo que a mesma ocorrência retorne, e ao executar o avanço de turno. O callback principal `00588AE0` e o callback de disponibilidade `005952C0` seguem ausentes no snapshot v3.

`QueuedTemporaryStatChecks.cs` cobre os cinco IDs, ambos os lados, ativação de mão/Set, gate de gasto, proteção de alvo, clamping, acúmulo, validade de turno, efeito no combate e retorno ao campo. Escopo sandbox headless sujeito ao CI. Janelas de Damage Step e correntes, produtores UI/CPU e comparação dinâmica com o original continuam pendentes.
