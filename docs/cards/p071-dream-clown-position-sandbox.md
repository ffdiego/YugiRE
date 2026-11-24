# P071 Dream Clown — posição e seleção (sandbox parcial)

Resource ID: **119**. `00575210` examina o estado anterior, emite o comando `0x7F`
e pode encaminhar a criação do pedido via `005BA490`. O callback primário
`00585C80` é compartilhado com Man-Eater Bug (500); entretanto, Dream Clown
usa preparação de alvo `00593970`, ausente das decompilações do SQLite.

Este recorte implementa **ataque face-up → defesa face-up** como gatilho.
A mudança registrada por `DemoMonsterPositionChange` gera uma seleção de
monstro adversário, reutiliza o resolvedor de destruição já empregado por
Man-Eater Bug e preserva seus gatilhos FLIP independentes.

`PendingFlipTarget` conserva o nome legado por compatibilidade com a UI;
o campo `Trigger` identifica a origem do efeito. A separação genérica de
pedidos e cadeias é uma pendência consciente.

A CPU também pode escolher a mudança de posição para ativar esse efeito,
e a UI mostra uma instrução específica de alvo adversário.

Os testes sintéticos em `PositionChangeEffectChecks.cs` exercitam os
dois sentidos da mudança, alvo próprio inválido, campo sem alvo, consumo
único, callback de Man-Eater Bug e escolha da CPU.

**Status: Done no escopo sandbox.** A CI .NET 10 compilou a solução e executou
com sucesso os 3.228 testes sintéticos (workflow 45098338059), incluindo os
10 casos novos. Não houve comparação com o Joey original.
Janelas de resposta, supressão, imunidade, revalidação após cadeia e timing
nativo precisam de investigação/implementação separadas.
