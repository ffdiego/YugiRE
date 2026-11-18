# Primeira família FLIP jogável — Man-Eater Bug e Morphing Jar

**Escopo:** C# `DemoDuel` / `Joey.App`, sem cadeias, negação, ativação de armadilhas na Damage Step ou paridade nativa integral.

O catálogo original `docs/evidence/card-callback-catalog.json` distingue **Man-Eater Bug resource ID 500, índice interno 753, P072** e **Morphing Jar resource ID 590, índice 972, P086**. Não confundir com **Morphing Jar #2 resource ID 1163** (efeito diferente). Texto da carta consultado na base oficial:
- Man-Eater Bug: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4507&ope=2&request_locale=en
- Morphing Jar: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4597&ope=2&request_locale=en

## Como jogar

1. No demo local raylib, os dois FLIPs destacados entram nas primeiras cartas da mão, quando existem no DAT.
2. Setar um deles em defesa virada para baixo com clique direito na mão e `Set`. Na próxima própria Main Phase, clicar no monstro setado para Flip Summon (o bloqueio de posição no turno da invocação é preservado).
3. Se Man-Eater Bug for revelado, o jogo bloqueia outras ações até o dono do FLIP selecionar **um monstro ocupado de qualquer jogador**. O monstro escolhido vai ao cemitério do próprio dono (inclusive se a escolha for Man-Eater Bug).
4. Se o oponente atacar o Man-Eater Bug virado para baixo, o combate fica suspenso. Destruir o atacante cancela o dano; escolher um terceiro monstro permite ao combate continuar depois da resolução.
5. Se Morphing Jar for revelado (Flip Summon ou ataque), **ambos descartam a mão inteira para seus respectivos cemitérios e compram cinco**. Os cards descartados não retornam ao deck. Após o efeito, o combate prossegue se o duelo continuar.
6. Em caso de deck insuficiente, o sandbox declara derrotado quem não conseguir completar cinco compras; se ambos tiverem menos de cinco, registra empate com `IsDraw`. **Política local simplificada, ainda sem paridade de chain/deck-out com o Joey original.**

## Contrato técnico

- `FlipEffectRules.Effect(id)` retorna `DestroyMonster` (500), `ReplaceHands` (590) ou `None`. Futuros FLIPs são adicionados em uma tabela de regras e em testes.
- `DemoDuel.ChangePosition` ativa FLIP apenas ao sair de face-down para face-up attack, respeitando a trava de posição.
- `DemoDuel.Attack` revela o defensor antes de calcular ATK/DEF. Man-Eater Bug cria `PendingFlipTarget`; `ResolveFlipTarget(player,zone)` valida o alvo e só então continua um combate suspenso se ambos os combatentes originais ainda estiverem presentes. Enquanto há pendência, ações normais são bloqueadas.
- `Morphing Jar` lê as mãos dos dois jogadores, descarta, compra cinco por lado e preserva ordem/atribuição de dono. Sem deck original na fixture `FromPartialObservation`, o empate simplificado é esperado; testes de compra completa usam `FromDecks` com 40 cartas por lado.
- `Joey.App/DuelScreen.cs` mostra o prompt e aceita seleção de um dos dois lados. A tela local é hot-seat: não há autenticação distinta dos jogadores.
- `FlipMonsterChecks.cs` cobre flip summon, combate atacando face-down, interrupção/continuação, consumo único, ambos os cemitérios, draw 5, ações bloqueadas e ids similares não suportados.

## Cobertura e limites

As duas cartas contam como `Done` **somente neste recorte jogável C#**, nunca como `Verified`. No `pack-index.json`, Man-Eater Bug pertence à lista Yugi (mask 1) e Morphing Jar à Kaiba (mask 2), logo o indicador **trilogia** aumenta em duas cartas e o indicador **Joey isolado** não muda. As cartas ainda podem ser utilizadas pelo demo unificado quando presentes no DAT.

A próxima fatia natural é **Penguin Soldier (601, pack Joey, P087)**: seleção de até dois alvos e devolução de monstros para a mão dos donos. Não tratar `Morphing Jar #2` (1163, pack Joey) como alias de Morphing Jar; ele exige outro resolver.

Pendências maiores: eventos de chain e resposta, efeitos negados, re-trigger sob regras complexas, destruição/substituições alternativas, deck-out nativo rigoroso, visual/timings do jogo original e validação manual no Windows.
