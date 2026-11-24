# CARDS.md — estado de cartas no YugiRE

Este é o inventário **funcional** de `Joey.Core`/`DemoDuel`, não uma contagem de cartas do DAT nem uma lista de callbacks decompilados. Atualize após implementar e testar, com escopo explícito.

**[Painel de progresso no GitHub](docs/cards/PLAYABILITY.md):** a badge no README apresenta a cobertura total e a cobertura de Joey. O GitHub Actions atualiza esses indicadores após mudanças no manifest.

**Gerador detalhado local:** `python scripts/card_playability.py --dat ../passion/data.dat` gera `scratch/card-playability/PROGRESS.md`, `summary.json` e CSV com as cartas de cada pack. Os IDs implementados ficam em [`docs/cards/playability-manifest.json`](docs/cards/playability-manifest.json); os critérios de contagem em [`docs/cards/playability-progress.md`](docs/cards/playability-progress.md). O registry antigo mede engenharia reversa, não a jogabilidade atual.

## Status

- **Pending:** não implementado no motor jogável.
- **Partial:** existe um primitivo, filtro, trace ou integração incompleta.
- **Done:** funcional **no escopo indicado**, acionável em um duelo C# e coberto por testes; não exige RE exaustiva.
- **Verified:** `Done` e resultado relevante comparado com execução observada do Joey original. Traces de uma regra ainda não implementada em C# **não** tornam a carta Verified.

## Inventário atual (sem extrapolar números do DAT)

| Família / mecânica | Estado atual | Evidência de implementação | Limitação principal |
| --- | --- | --- | --- |
| Monstros normais (264 IDs do catálogo dos packs) | **Done** (ações básicas no sandbox) | `NormalMonsterPlayabilityChecks.cs` exercita cada ID em invocação, tributo, set e ataque; `DemoDuel` e `NormalSummonRules` | Ainda sem interações completas de magias/armadilhas, efeitos externos e paridade nativa; teste integral com metadados reais do DAT disponível como validação opcional |
| Magias de dano direto `P045` (5 IDs) | **Done** (sandbox) | `DirectDamageRules.cs`, `LifePointSpellRules.cs`, `DemoDuel` e testes de ativação da mão/set | Sem correntes, negação e validação de timing nativo integral |
| Magias de recuperação de LP (5 IDs) | **Done** (sandbox) | Red Medicine, Goblin's Secret Remedy, Soul of the Pure, Dian Keto e Rain of Mercy usam o mesmo fluxo de ativação/LP/cemitério | Valores do texto das cartas; interações/negação nativa não implementadas |
| Magias normais de compra fixa `P119/P143` | **Done** (2 IDs no sandbox) | Pot of Greed (1010) e Upstart Goblin (1065). `DrawSpellRules`, `DemoDuel` e `DrawSpellChecks` cobrem mão, Set, contagens, LP e uso pela CPU. | Exigem cartas suficientes no deck. Sem correntes, negação ou comparação nativa do timing. |
| Equipamentos `P025` | **Partial** (24 IDs, 3 apenas na fila opt-in) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo; [C04](docs/cards/c04-fixed-equipment.md) | Ativação legada da mão (21 IDs); EquipCommand mão/Set (24 IDs, incluindo 3 dinâmicos). UI/CPU da fila e supressões pendentes |
| Magias de campo `P036` | **Done** (13 IDs no sandbox) | Seis magias por tipo, seis por atributo e Chorus of Sanctuary; zona FIELD, ativação/set, substituição, bônus/penalidades e combate | Regras baseadas em texto, não paridade nativa integral; Sea Serpent classCode 5 ainda é inferido |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes |
| Dream Clown (119), gatilho de posição | **Done** (escopo sandbox, testes .NET CI) | `MonsterPositionEffectRules.cs`, `DemoDuel.ChangePosition`, `PositionChangeEffectChecks.cs` | Ataque → defesa inicia seleção e destruição de monstro adversário. Seletor legado compartilhado com FLIP, gatilho distinto de FLIP; sem chain ou paridade nativa. |
| P168 / Bubonic Vermin (1307) | **Done** (sandbox, regressões .NET no CI) | `FlipEffectRules.cs`, `DemoDuel`, `BubonicVerminChecks.cs`, seletor de cartas existente | FLIP opcional: Deck → campo, 1 cópia por resource ID em defesa oculta. Gate de Royal Command integrado; sem chains ou paridade dinâmica. |
| P252 — Dark Necrofear (1514) | **Partial** | `GraveyardBanishSummonRules.cs`, `DemoDuel.SpecialSummonByGraveyardBanish` | Custo de 3 Fiends no próprio GY, invocação especial Core; UI/CPU, Spirit Elimination, End Phase/equipamento e controle ainda pendentes. |
| P188 — Dimensional Warrior (255) | **Done** (escopo sandbox, testes .NET CI) | `DimensionalWarriorRules.cs`, `DemoDuel.FinishDeferredBattle`, `DimensionalWarriorChecks.cs` | Dois combatentes banidos após cálculo de dano; sem efeitos de cemitério; callback primário `0058CC00` não decompilado nem validado em duelo nativo. |
| FLIP P065 (The Immortal of Thunder, 461) | **Done** (sandbox histórico, testes .NET no CI) | `ImmortalThunderRules.cs`, `FlipEffectRules.cs`, `DemoDuel.RemoveMonster`, `ImmortalThunderChecks.cs` | +3000 no FLIP e -5000 na saída para GY **após revelação** da ocorrência; timing/negação/controle nativos ainda não verificados. |
| FLIPs P051/P052 (Lady Assailant e Fire Sorcerer) | **Done** (sandbox validado no CI .NET) | `FlipBanishBurnRules.cs`, `FlipEffectRules.cs`, `FlipBanishBurnChecks.cs` | Custo por banimento Deck/Hand, dano 800 compartilhado e RNG determinístico sandbox. Sem paridade de seleção aleatória nativa nem callback de disponibilidade decompilado. |
| Royal Command (1530), supressão global de FLIP | **Partial** (branch, validação pendente) | `FlipEffectSuppressionRules.cs`, `DemoDuel.TriggerFlip`, `RoyalCommandSuppressionChecks.cs` | Presença/elegibilidade, precedência Jinzo e Royal Decree, bloqueio na criação e resolução de pedidos FLIP; sem cadeia/priority/supressão geral nativa. |
| FLIPs P085/P255 (Greenkappa e Tornado Bird) | **Done** (escopo sandbox, regressões .NET no CI) | `FlipSpellTrapSelectionRules.cs`, `DemoDuel`, `FlipSpellTrapSelectionChecks.cs` e UI raylib | Dois alvos S/T, destruição ou retorno à mão; sem chains, supressão Royal Command parcial integrada, paridade Ghidra de sete callbacks e confirmação nativa. |
| Monstros de efeito FLIP (Man-Eater Bug, Morphing Jar, Penguin Soldier, Hane-Hane, Morphing Jar #2) | **Done** (5 IDs no sandbox) | `FlipEffectRules.cs`, `DemoDuel`, `FlipMonsterChecks.cs`, seleção na interface | Sem chains/negações; deck-out duplo do Morphing Jar é política simplificada |
| FLIP P018 (Magician of Faith e Mask of Darkness) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `CardSelectionChecks.cs`, UI raylib | Cemitério → mão; sem chains/negação ou UI nativa verificada |
| P082/P088 (Needle Worm, Hiro's Shadow Scout) | **Done** (2 IDs no sandbox) | `DeckFlowRules.cs`, `DemoDuel`, `FlipDeckFlowChecks.cs` | Descarte do topo do deck e compra com descarte de magias; sem paridade nativa |
| P006 (Sangan, Witch of the Black Forest) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `DeckSearcherChecks.cs` | Campo → cemitério, busca Deck → mão; não modela chains, controle alterado nem RNG nativo |
| P164 (seis recrutadores de atributo) | **Done** (6 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `BattleRecruiterChecks.cs`, UI raylib | Destruição em batalha, escolha opcional Deck → campo; paridade nativa ainda não verificada |
| P168 (Nimble Momonga e Giant Germ) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `BattleGraveyardEffectChecks.cs` | LP após destruição em batalha, invoca 0–2 cartas do mesmo nome; UI provisória |
| P169 (Spear Cretin) | **Done** (1 ID no sandbox) | `DemoDuel.MonsterWasFlipped`, `BattleGraveyardEffectChecks.cs` | FLIP diferido no envio ao cemitério, seleção para cada jogador; sem paridade nativa |
| P165 (Sonic Bird, Senju of the Thousand Hands) | **Done** (2 IDs no sandbox) | `RitualSearchRules.cs`, `DemoDuel`, `RitualSearcherChecks.cs`, UI raylib | Busca opcional por Magia/Monstro de Ritual no Normal/Flip Summon; não na revelação em batalha |
| Invocação de Fusão por receita | **Done** para 31 resultados dos packs, **Partial** para outros três | `FusionRules.cs`, `DemoFusion.cs`, `FusionChecks.cs`, seleção na Alpha | Respostas a Special Summon e os efeitos próprios de três resultados seguem pendentes |
| P037/P039 (Dark Hole, Raigeki) | **Done** (2 magias no sandbox) | `MonsterWipeRules.cs`, `DemoDuel`, `MonsterWipeChecks.cs` | Destruição em massa sem alvo, enfileira buscas de Sangan/Witch e ressurreição de Spear Cretin; sem chains/negação |
| P092 (destruição por tipo) | **Done** (7 magias no sandbox) | `MonsterWipeRules.cs`, `DemoDuel`, `MonsterWipeChecks.cs` | Monstros face-up do tipo indicado nos dois lados, inclusive em defesa |
| P196/P038 (Trap Hole, Torrential Tribute) | **Done** (2 armadilhas no sandbox) | `SummonTrapRules.cs`, `DemoDuel`, `SummonTrapChecks.cs`, UI raylib | Resposta opcional a Normal/Flip Summon, sem cadeias ou paridade nativa; Special Summon pendente |
| P200/P192 (Magic Cylinder e Negate Attack) | **Done** (2 armadilhas no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackNegationTrapChecks.cs` | Dano refletido e encerramento da Battle Phase; sem chains nem paridade nativa |
| P201 (Mirror Force) | **Done** (1 armadilha no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackTrapChecks.cs`, UI raylib | Resposta à declaração de ataque antes do dano/FLIP, destruição dos monstros em ATK; sem chains ou replay nativo |
| Outras armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Restante das famílias e das janelas de resposta ainda pendente |

**Todos os 499 monstros da união dos packs possuem classificação de referência**. Há 264 Normal, 193 Effect, 34 Fusion e 8 Ritual. O grupo Normal é `Done` para o ciclo básico de invocação, tributos, Set e batalha no sandbox C#. A categoria dos resultados de fusão e dos monstros normais foi cruzada com os bits de categoria do DAT local.

## Monstros normais compartilhando uma implementação

Os 264 IDs Normal estão em `docs/cards/playability-manifest.json` no grupo `monster.normal`, e cada um é exercitado por `NormalMonsterPlayabilityChecks.Run` com propriedades sintéticas representativas dos níveis 1–8. No Windows, `Joey.Tests` com o `data.dat` também executa `RunWithCatalog`, usando nível, ATK/DEF e categoria **reais** de cada carta; essa última verificação não foi executada no CI sem DAT. Isso valida o escopo de sandbox, **não** 264 implementações de efeitos individuais ou paridade nativa. Identidades Normal históricas podem mudar entre edições, então divergências do DAT ainda devem ser revisadas.

## FLIP jogável: primeiro lote

- **Man-Eater Bug** (`resourceId=500`): ao revelar por Flip Summon ou ataque, seleciona e destrói um monstro de qualquer lado. Se foi atacado, o combate espera a escolha e só prossegue se os dois combatentes permanecerem.
- **Morphing Jar** (`resourceId=590`): descarta ambas as mãos para o cemitério e cada duelista compra cinco. O caso de ambos os decks insuficientes é um empate simplificado do sandbox.
- **Penguin Soldier** (`resourceId=601`): retorna até dois monstros para as mãos dos donos. Pode escolher a si mesmo após Flip Summon ou sobreviver ao ataque; se estiver marcado para destruição por batalha, não pode devolver a si mesmo.
- **Morphing Jar #2** (`resourceId=1163`, `P182`): embaralha monstros elegíveis do campo em cada Main Deck, escava até uma quota por jogador e coloca monstros de nível 4 ou inferior em defesa virada para baixo; as cartas não invocadas vão ao cemitério. O Jar destruído por batalha não retorna ao deck. Sem Extra Deck, tokens ou controle trocado.
- **Hane-Hane** (`resourceId=540`, `P080`): retorna exatamente um monstro para a mão de seu dono. Compartilha o resolver com Penguin, mas não permite encerrar com zero escolhas se houver alvo legal; não pode selecionar um monstro marcado para destruição por batalha.
- O cálculo de dano vem **antes** dos efeitos FLIP de combate; monstros já marcados para destruição ainda permanecem no campo até o fim da etapa, mas não podem ser retornados à mão.
- Os testes de cenário e os prompts de seleção estão descritos em [`docs/cards/flip-sandbox.md`](docs/cards/flip-sandbox.md). **Morphing Jar #2 (`1163`) possui agora um resolver próprio**, independente de Morphing Jar (590).

Magic Cylinder (P200) e Negate Attack (P192) reutilizam a janela de ataque introduzida por Mirror Force: a primeira reflete ATK e conserva a fase de batalha, a segunda cancela o ataque e a encerra. Três decks controlados foram preparados para coleta nativa antes de certificar as diferenças de timing. Consulte [P200/P192](docs/cards/p200-p192-attack-traps-sandbox.md) e [piloto Windows](docs/research/native-attack-response-pilot.md).

Mirror Force (P201) introduz janela de resposta à **declaração de ataque**, separada do cálculo de dano e da revelação do defensor. `Pass` retoma a mesma tentativa de ataque sem duplicá-la; a ativação usa destruição em massa apenas dos monstros adversários em posição de ataque. A engine completa de correntes permanece pendente. Consulte [`docs/cards/p201-mirror-force-attack-response-sandbox.md`](docs/cards/p201-mirror-force-attack-response-sandbox.md) e [`docs/research/chain-event-engine-boundaries.md`](docs/research/chain-event-engine-boundaries.md).

Trap Hole (P196) e Torrential Tribute (P038) introduzem a primeira janela explícita de resposta a invocação, exigindo armadilha Set num turno anterior e decisão Pass/Activate. O evento registra Normal/Flip Summon, sem se confundir com a revelação durante ataque. Respostas a Special Summon, chains e prioridade permanecem pendentes. Consulte [`docs/cards/p196-p038-summon-trap-reactions-sandbox.md`](docs/cards/p196-p038-summon-trap-reactions-sandbox.md).

Dark Hole (P037) e Raigeki (P039) usam o resolvedor compartilhado de destruição não direcionada; removem todos os monstros relevantes antes de abrir efeitos de envio ao cemitério. A ativação a partir da mão e de Set é validada no sandbox, incluindo ausência de monstros, FLIPs face-down e interações com Sangan/Witch/Spear Cretin. Consulte [`docs/cards/p037-p039-monster-wipe-sandbox.md`](docs/cards/p037-p039-monster-wipe-sandbox.md).

P165 compartilha o seletor `Deck → Hand` com P006, mas só é acionado por Normal/Flip Summon (não por simples revelação). Os filtros Ritual são baseados nos 16 pares nativos P269, com escolha opcional e nenhuma alteração ao deck no Skip. Consulte [`docs/cards/p165-ritual-search-sandbox.md`](docs/cards/p165-ritual-search-sandbox.md).

P018 usa `PendingCardSelection`: índice de ocorrência na zona, filtro DAT e revalidação no Core; modal raylib provisório. Sem candidatos, não abre seleção. Em combate, a destruição ocorre após a escolha; testes em `CardSelectionChecks.cs`. Deck → mão foi implementado para P006 em sandbox; Deck → campo foi implementado no sandbox para P164, com seleção opcional e invocação em ataque; UI nativa segue pendente.

Man-Eater Bug, Hane-Hane e Morphing Jar pertencem aos packs Yugi/Kaiba. Penguin Soldier e Morphing Jar #2 pertencem ao Joey; **esta iteração aumenta ambas as badges**.

## Cartas e magias jogáveis no sandbox (escopos delimitados)

| Resource ID | Carta | Status | Referência / escopo |
| ---: | --- | --- | --- |
| 500 | Man-Eater Bug | Done (sandbox) | FLIP destrói monstro selecionado, incluindo fase de batalha; sem cadeia/negação |
| 590 | Morphing Jar | Done (sandbox) | FLIP descarta as duas mãos e compra 5 por lado; deck-out simplificado |
| 601 | Penguin Soldier | Done (sandbox) | FLIP retorna até dois monstros, exceto marcados para destruição por batalha |
| 540 | Hane-Hane | Done (sandbox) | FLIP retorna exatamente um monstro, salvo alvos marcados para destruição |
| 427 | Magician of Faith | Done (sandbox) | Recupera magia elegível do cemitério pela escolha FLIP |
| 101 | Mask of Darkness | Done (sandbox) | Recupera armadilha elegível do cemitério pela escolha FLIP |
| 47 | Sangan | Done (sandbox) | Quando enviado do campo ao cemitério, busca monstro ATK ≤1500 do Main Deck para a mão |
| 573 | Witch of the Black Forest | Done (sandbox) | Quando enviada do campo ao cemitério, busca monstro DEF ≤1500 do Main Deck para a mão |
| 1123 | Mystic Tomato | Done (sandbox) | Recruta DARK ATK ≤1500 após destruição em batalha |
| 1108 | Giant Rat | Done (sandbox) | Recruta EARTH ATK ≤1500 após destruição em batalha |
| 1117 | Shining Angel | Done (sandbox) | Recruta LIGHT ATK ≤1500 após destruição em batalha |
| 1110 | UFO Turtle | Done (sandbox) | Recruta FIRE ATK ≤1500 após destruição em batalha |
| 1119 | Mother Grizzly | Done (sandbox) | Recruta WATER ATK ≤1500 após destruição em batalha |
| 1120 | Flying Kamakiri #1 | Done (sandbox) | Recruta WIND ATK ≤1500 após destruição em batalha |
| 1115 | Nimble Momonga | Done (sandbox) | Quando destruída em batalha, cura 1000 LP; invoca até 2 cópias do deck em defesa oculta |
| 1114 | Giant Germ | Done (sandbox) | Quando destruído em batalha, causa 500 dano; invoca até 2 cópias em ataque |
| 1116 | Spear Cretin | Done (sandbox) | Após ser virado, ao ir ao cemitério cada lado revive 1 monstro do próprio cemitério |
| 561 | Needle Worm | Done (sandbox) | FLIP: envia até 5 cartas do topo do deck oponente ao seu cemitério |
| 610 | Hiro's Shadow Scout | Done (sandbox) | FLIP: adversário compra até 3 cartas e descarta as magias recém-compradas; deck-out na compra obrigatória insuficiente (política do sandbox) |
| 1338 | 4-Starred Ladybug of Doom | Done (sandbox) | FLIP destrói monstros adversários face-up de nível 4 |
| 539 | Skelengel | Done (sandbox) | FLIP oferece ao controlador comprar uma carta |
| 1122 | Sonic Bird | Done (sandbox) | No Normal ou Flip Summon pode buscar 1 Magia de Ritual do próprio Main Deck para a mão |
| 1109 | Senju of the Thousand Hands | Done (sandbox) | No Normal ou Flip Summon pode buscar 1 Monstro de Ritual do próprio Main Deck para a mão |
| 335 | Dark Hole | Done (sandbox) | Destrói todos os monstros de ambos os lados, sem disparar FLIP por simples destruição |
| 336 | Raigeki | Done (sandbox) | Destrói todos os monstros do adversário; ignora a posição sem selecionar alvos |
| 671 | Harpie's Feather Duster | Done (sandbox) | Destrói todas as magias e armadilhas do adversário, inclusive sua zona de campo |
| 1061 | Heavy Storm | Done (sandbox) | Destrói todas as magias e armadilhas dos dois lados, inclusive as zonas de campo |
| 1004 | Remove Trap | Done (sandbox) | Escolhe e destrói uma armadilha face-up de qualquer lado |
| 1079 | Mystical Space Typhoon | Partial | Escolhe e destrói uma magia ou armadilha de qualquer lado, inclusive na zona de campo, na Main Phase própria |
| 652 | Warrior Elimination | Done (sandbox) | Destrói Warriors face-up dos dois lados |
| 659 | Stain Storm | Done (sandbox) | Destrói Machines face-up dos dois lados |
| 661 | Eradicating Aerosol | Done (sandbox) | Destrói Insects face-up dos dois lados |
| 662 | Breath of Light | Done (sandbox) | Destrói Rocks face-up dos dois lados |
| 663 | Eternal Draught | Done (sandbox) | Destrói Fish face-up dos dois lados |
| 1036 | Last Day of Witch | Done (sandbox) | Destrói Spellcasters face-up dos dois lados |
| 1037 | Exile of the Wicked | Done (sandbox) | Destrói Fiends face-up dos dois lados |
| 1002 | Trap Hole | Done (sandbox) | Armadilha previamente Set reage a Normal/Flip Summon adversária com ATK atual ≥1000 e destrói o monstro |
| 1425 | Torrential Tribute | Done (sandbox) | Armadilha previamente Set reage a Normal/Flip Summon e destrói todos os monstros; resposta a Special Summon pendente |
| 1214 | Magic Cylinder | Done (sandbox) | Resposta a ataque adversário: cancela o ataque e reflete ATK atual como dano ao atacante; Battle Phase continua |
| 960 | Negate Attack | Done (sandbox) | Resposta a ataque adversário: cancela ataque e encerra a Battle Phase |
| 1056 | Mirror Force | Done (sandbox) | Armadilha Set responde à declaração de ataque adversária, destruindo todos os monstros ATK desse lado; uma resposta sem chain |
| 1163 | Morphing Jar #2 | Done (sandbox) | FLIP devolve monstros ao Main Deck e escava quota; sem suporte a Extra Deck, tokens ou controle alterado |
| 342 | Sparks | Done (sandbox) | `P045`, 200 de dano direto na rota simples |
| 343 | Hinotama | Done (sandbox) | `P045`, 500 de dano direto |
| 344 | Final Flame | Done (sandbox) | `P045`, 600 de dano direto |
| 345 | Ookazi | Done (sandbox) | `P045`, 800 de dano direto |
| 1007 | Raimei | Done (sandbox) | `P045`, 300 de dano direto |
| 338 | Red Medicine | Done (sandbox) | Recupera 500 LP do ativador |
| 339 | Goblin's Secret Remedy | Done (sandbox) | Recupera 600 LP do ativador |
| 340 | Soul of the Pure | Done (sandbox) | Recupera 800 LP do ativador |
| 341 | Dian Keto the Cure Master | Done (sandbox) | Recupera 1000 LP do ativador |
| 1315 | Rain of Mercy | Done (sandbox) | Recupera 1000 LP dos dois duelistas |
| 332 | Sogen | Done (sandbox) | Warrior classe 15 e Beast-Warrior classe 12: +200 ATK/DEF; apenas Warrior observado nativamente |
| 329 | Forest | Done (sandbox) | Insect 10, Beast 11, Beast-Warrior 12, Plant 13: +200 ATK/DEF |
| 330 | Wasteland | Done (sandbox) | Dinosaur 9, Zombie 2, Rock 6: +200 ATK/DEF |
| 331 | Mountain | Done (sandbox) | Dragon 1, Winged Beast 16, Thunder 19: +200 ATK/DEF |
| 333 | Umi | Done (sandbox) | Fish 8, Sea Serpent 5 (inferido), Thunder 19, Aqua 14: +200 ATK/DEF; Machine 7, Pyro 4: -200/-200 |
| 334 | Yami | Done (sandbox) | Fiend 3, Spellcaster 18: +200 ATK/DEF; Fairy 17: -200/-200 |
| 1069 | Chorus of Sanctuary | Done (sandbox) | +500 DEF somente para monstros face-up em posição de defesa |
| 1125 | Gaia Power | Done (sandbox) | EARTH (attributeCode 5), +500 ATK/-400 DEF |
| 1126 | Umiiruka | Done (sandbox) | WATER (attributeCode 3), +500 ATK/-400 DEF |
| 1127 | Molten Destruction | Done (sandbox) | FIRE (attributeCode 4), +500 ATK/-400 DEF |
| 1128 | Rising Air Current | Done (sandbox) | WIND (attributeCode 6), +500 ATK/-400 DEF |
| 1129 | Luminous Spark | Done (sandbox) | LIGHT (attributeCode 1), +500 ATK/-400 DEF |
| 1130 | Mystic Plasma Zone | Done (sandbox) | DARK (attributeCode 2), +500 ATK/-400 DEF |
| 300 | Legendary Sword | Partial | `P025`, Warrior tipo 15, +300/+300 sob condições suportadas |
| 301 | Sword of Dark Destruction | Partial | `P025`, atributo DARK, +400/-200 sob condições suportadas |
| 307 | Beast Fangs | Partial | `P025`, Beast tipo 11, +300/+300 sob condições suportadas |
| 1427 (`0x593`) | Infinite Cards | Done (sandbox) | Desabilita limite de 6 cartas na End Phase enquanto ativa |

### Auras elementais e ataque direto

| Resource ID | Carta | Status | Escopo |
| ---: | --- | --- | --- |
| 491 | Hoshiningen | Done (sandbox) | LIGHT +500 ATK, DARK −400 ATK |
| 627 | Witch's Apprentice | Done (sandbox) | DARK +500 ATK, LIGHT −400 ATK |
| 523 | Star Boy | Done (sandbox) | WATER +500 ATK, FIRE −400 ATK |
| 597 | Little Chimera | Done (sandbox) | FIRE +500 ATK, WATER −400 ATK |
| 526 | Milus Radiant | Done (sandbox) | EARTH +500 ATK, WIND −400 ATK |
| 608 | Bladefly | Done (sandbox) | WIND +500 ATK, EARTH −400 ATK |
| 386 | Mystic Lamp | Done (sandbox) | Pode declarar ataque direto com monstros adversários em campo |
| 396 | Leghul | Done (sandbox) | Mesma permissão de ataque direto |
| 397 | Ooguchi | Done (sandbox) | Mesma permissão de ataque direto |
| 421 | Jinzo #7 | Done (sandbox) | Mesma permissão de ataque direto |
| 487 | Rainbow Flower | Done (sandbox) | Mesma permissão de ataque direto |
| 634 | Queen's Double | Done (sandbox) | Mesma permissão de ataque direto |
| 812 | Alligator's Sword Dragon | Partial | Permite ataque direto se todos os monstros adversários face-up forem WATER, FIRE ou EARTH. Exercitado com monstro já em campo |

Cada fonte de aura face-up contribui para os monstros face-up dos dois lados, inclusive ela própria. O motor soma todas as contribuições com magia de campo e equipamento antes de limitar os atributos. Usa o atributo do DAT no contexto do sandbox. Auras em defesa face-up continuam ativas.

O ataque direto normal continua disponível em campo adversário vazio. Com defensores presentes, o cliente oferece a escolha entre um monstro adversário e a espada no ponto de impacto direto existente. O uso do sprite DAT nesse ponto como controle clicável é uma decisão transitória do cliente. Os ataques passam pela janela de resposta e a permissão é conferida novamente antes do dano. O filtro de 812 ignora defensores ocultos segundo 0056A810 e ainda precisa de comparação em duelo no Joey original. A invocação por fusão de 812 continua em aberto.

Os deltas e permissões vêm do [lote 10 da campanha estática](docs/research/card-static-sweep/10-efeitos-sem-registro.jsonl). Os cenários em [MonsterBattleRuleChecks](src/tests/Joey.Tests/MonsterBattleRuleChecks.cs) cobrem as famílias no Core. A Alpha inclui Hoshiningen e Jinzo #7 no baralho padrão.

### FLIPs de destruição em massa e compra

4-Starred Ladybug of Doom compartilha a resolução de destruição em massa com o P092. O filtro de `0058F940` usa o nível 4, a face e o lado adversário. O sandbox mantém as remoções já determinadas em combate sob o próprio fluxo de batalha. [Estudo P242](docs/research/card-static-sweep/05-janelas-invocacao-batalha.jsonl)

Skelengel usa o ramo de compra única de `005887C0`. A escolha vem do texto do DAT e é oferecida no fluxo de decisões opcionais do sandbox. O motor suspende o fim da etapa de dano até a decisão e pula a oferta com deck vazio. A ordem do prompt e as interações de negação ainda precisam de comparação no Joey. As duas cartas integram o deck padrão da Alpha. [Estudo P118](docs/research/card-static-sweep/02-zonas-e-modificadores.jsonl)

### Compras por dano de batalha

| Resource ID | Carta | Status | Escopo |
| ---: | --- | --- | --- |
| 219 | Masked Sorcerer | Done (sandbox) | Ao causar dano de batalha aos LP adversários, o controlador compra uma carta |
| 522 | The Bistro Butcher | Done (sandbox) | Ao causar dano de batalha aos LP adversários, o adversário compra duas cartas |

O produtor `00578540` agenda os pedidos para 219 e 522 após o comando de dano. Os callbacks `005844A0` e `00585D30` enviam os totais de compra a `00578B60`. O sandbox resolve a compra obrigatória após dano positivo e não letal, incluindo ataque direto e dano causado pelo defensor. Se faltarem cartas, compra as disponíveis e aplica deck-out. A ordem exata do pedido nativo e seus casos de deck-out seguem pendentes. [Estudo das rotinas de compra](docs/research/card-static-sweep/02-zonas-e-modificadores.jsonl)

### Destruição em massa de magias e armadilhas (P094/P141)

Harpie's Feather Duster emite a máscara `0x7E0` no lado adversário em `00586850`. Heavy Storm emite `0x07E007E0` em `00589830` para os dois lados. A máscara abrange as cinco zonas de magia/armadilha e a zona de campo por lado. A disponibilidade de Feather Duster conta cartas adversárias nessas seis posições via `0057FC40 → 0056A320`.

No sandbox, a ativação da mão ou de Set exige outra carta elegível e inclui cartas face-up e face-down. A remoção de equipamento limpa seu vínculo e recalcula os stats do monstro sem removê-lo. A CPU usa a magia quando destrói mais cartas adversárias do que próprias. Feather Duster integra o deck padrão da Alpha. Proteções e respostas nativas de destruição de magias/armadilhas ficam pendentes. [Cenários C#](src/tests/Joey.Tests/SpellTrapWipeChecks.cs)

### Destruição com alvo de magia ou armadilha (P075/P115)

Mystical Space Typhoon (1079) usa `0057B530` para filtrar zonas ocupadas de ambos os lados, inclusive as zonas de campo, excluindo a própria carta. A resolução `00585C80` revalida o alvo via `00583470` e pede destruição em `00577880`. Remove Trap (1004) usa `0057B270` para aceitar apenas armadilhas face-up de qualquer lado e resolve em `005885A0`.

Na Main Phase própria, o sandbox permite iniciar da mão ou de Set, selecionar uma carta no campo e cancelar antes de gastar a magia. O motor revalida a carta e o filtro quando a seleção é confirmada. Remove Trap pode ser ativada no turno em que foi baixada. Mystical Space Typhoon, incluída no deck padrão da Alpha, precisa esperar um turno após o Set e segue Partial enquanto faltarem as outras janelas de Quick-Play e as correntes. A destruição de uma peça face-up ligada a um Destiny Board ativo também remove as outras peças face-up do mesmo lado, conforme `00576360` e `00577280`. O recorte foi testado em C#, com confirmação nativa pendente. [Cenários C#](src/tests/Joey.Tests/SpellTrapWipeChecks.cs)

### Destruição por tipo (P092)

O filtro nativo em `0057AAA0` exige face-up para os sete IDs, inclusive quando o DAT descreve apenas “todos”. A resolução `00586540` percorre os dois lados e entrega uma máscara a `00576940`. No sandbox, o tipo vem do DAT e a ativação da mão ou de Set exige ao menos um monstro elegível. A disponibilidade com máscara vazia e os bloqueios de proteção nativa continuam em aberto. Eternal Rest (655) permanece sem regra no sandbox enquanto a contagem dos tipos de vínculo 1, 4 e 5 está em investigação. [Campanha estática P092](docs/research/card-static-sweep/01-lp-destruicao-respostas.jsonl)

### Polymerization, Fusion Gate e Fusion Deck (P268/P265)

Polymerization (1003 e variante 1034) abre escolha do resultado e dos materiais próprios da mão ou do campo durante a Main Phase, inclusive quando veio de Set. Cada cópia ocupa sua própria posição. Se o campo estiver cheio, a seleção precisa liberar uma vaga. Antes de confirmar, o jogador pode cancelar sem gastar cartas. A confirmação envia magia e materiais ao cemitério e invoca em ataque ou defesa face-up. A seleção e o primeiro slot livre são políticas do sandbox, com controles provisórios na Alpha. A ativação usa a prévia existente e a colocação usa o evento de Special Summon.

O Fusion Deck é separado da mão e do Main Deck. A Alpha inclui Rabid Horseman (91), Battle Ox (25) e Mystic Horseman (90). O duelo importado lê os monstros de Fusão da lista 1 do YDC, conferida em 58 presets do DAT. Monstros de Fusão devolvidos do campo à mão retornam ao Fusion Deck. O mesmo se aplica à devolução por Morphing Jar #2, sem aumentar sua quota de escavação. O ramo de categoria 2 de `00576960` redireciona os destinos de mão e Main Deck para a região de Fusão. `0058C52A` exclui a categoria da contagem do Morphing Jar #2.

Os testes executam as 57 receitas do DAT, incluindo os três Blue-Eyes White Dragon de Blue-Eyes Ultimate Dragon (379). O manifesto registra 30 resultados dos packs no grupo de invocação por receita e Alligator’s Sword Dragon (812) separadamente, com ataque direto testado após a invocação. Chimera (1241), Thousand-Eyes Restrict (1334) e The Last Warrior (1526) podem ser invocados, com efeitos próprios pendentes. Polymerization é `Done` no sandbox com os quatro substitutos habilitados. Respostas, destinos substituídos por outros efeitos e timings do Joey original ainda precisam de comparação. [Receitas e endereços](docs/research/card-static-sweep/12-receitas-de-fusao.jsonl) · [Cenários C#](src/tests/Joey.Tests/FusionChecks.cs)

O sandbox permite até um substituto por receita. Goddess with the Third Eye (108), Beastking of the Swamps (257), Versago the Destroyer (268) e Mystical Sheep #1 (641) estão `Done` no recorte de Polymerization e Fusion Gate. A interface desabilita materiais que não permitem completar a receita e a CPU usa a mesma regra para obter uma combinação. Com o campo cheio, o sandbox aceita qualquer combinação que libere uma vaga. A preferência do pré-teste nativo por materiais exatos nesse caso segue sem confirmação em execução. [Análise dos substitutos](docs/cards/fusion-substitutes.md)

Fusion Gate (1547) é `Done` no sandbox e integra a Alpha. Depois de ser ativada na zona de campo, permite que qualquer lado use o próprio Fusion Deck e materiais em sua Main Phase. A mesma fonte permanece em campo para outras fusões e a confirmação bane os materiais, preservando a limpeza de equipamentos e sem abrir efeitos de cemitério dos materiais. A carta é ativável mesmo sem receita disponível. No executável, `005833D0` distingue a ativação do campo do uso da fusão e consulta `00580600` para este último. `00591739` entra na seleção compartilhada e os ramos `005927F0` e `005928AA` encaminham o banimento. A comparação no Joey original continua pendente. [Campanha estática P265](docs/research/card-static-sweep/06-combate-diferido-e-invocacao.jsonl)

### Magias LP e compra na fila (C12)

As dez magias fixas já Done têm agora caminho opt-in `TryQueueLifePointSpell` para mão/Set, com gasto e mudanças de LP em comandos separados. UI/CPU ainda legados. [Contrato](docs/cards/c12-queued-lp-spells.md). Pot of Greed e Upstart Goblin têm `TryQueueDrawSpell`, com gasto validado antes da continuação, DrawCommand por carta e HealCommand posterior de Upstart. [Contrato](docs/cards/c12-queued-draw-spells.md). Stim-Pack e Germ Infection têm apenas aritmética estática, não ativação, até fechar A07.

### Destruição de monstros na fila (C12)

Raigeki, Dark Hole e os sete destruidores por tipo usam `TryQueueMonsterWipe` na mão/Set. Reutilizam `MonsterWipeRules`, remoções individuais e limpeza de equipamentos; o gasto inválido cancela o lote e as buscas de cemitério abrem após a última remoção. UI/CPU e seleção final ainda legados. [Contrato](docs/cards/c12-queued-monster-wipes.md).

### Destruição de S/T na fila (C12)

Harpie's Feather Duster e Heavy Storm usam `TryQueueSpellTrapWipe` (mão/Set). O lote reserva ocorrências, exclui a fonte e remove S/T e FIELD por comando, incluindo vínculos. Destruição avulsa de S/T ainda limitada; MST e Remove Trap usam consulta de candidatos e admissão de ocorrências, incluindo cascata Destiny Board, sem prompt bloqueante. [Contrato seletivas](docs/cards/c12-queued-targeted-spelltrap.md). [Contrato](docs/cards/c12-queued-spelltrap-wipes.md).

### FLIPs seletivos na fila (C12)

Man-Eater Bug, Hane-Hane e Penguin Soldier: `TryQueueMonsterFlipAnswer` aceita ocorrências de fonte/alvos após Flip Summon; cada destruição/retorno e vínculo tem comando próprio. A revelação em batalha mantém resposta legada até migração da Damage Step. [Contrato](docs/cards/c12-queued-monster-flips.md).

### Recuperação de GY na fila (C12)

Magician of Faith e Mask of Darkness usam `QueuedGraveyardRecoveryCandidates` e `TryQueueGraveyardRecovery`; `MoveToHandCommand` preserva ocorrência e não é compra. Resposta de Flip Summon manual, batalha/UI/CPU ainda legados. [Contrato](docs/cards/c12-queued-graveyard-recovery.md).

### Busca Deck → mão na fila (C12)

Sangan/Witch usam `QueuedDeckSearchCandidates` e `TryQueueDeckSearch`. MoveToHandCommand aceita Deck e GY; ShuffleDeckCommand separado após transferência bem-sucedida. UI/CPU e paridade nativa ainda pendentes. [Contrato](docs/cards/c12-queued-deck-search.md).

## Evidências nativas (não confundir com paridade C# completa)

| Resource ID | Carta / família | Estado do conhecimento |
| ---: | --- | --- |
| 332 | Sogen | 69 snapshots de atributos de batalha no piloto; 6 ocorrências classe15 com +200/+200. No sandbox, também inclui Beast-Warrior (classe12) conforme descrição, mas **sem comparação dinâmica nativa dessa classe** |
| 1315 | Rain of Mercy | Aumento de 1000 LP observado em captura do original; efeito simples agora jogável no sandbox, sem paridade completa |
| 1306 | Skull Invitation | Correlação de perdas de 300 LP e controle negativo observacional |
| 1353 | Burning Land | Ticks de 500 LP relatados em evidência, ainda sem regra C# completa |
| — | Rituais `P269` | Dados de rituais em `docs/evidence/ritual-rules-catalog.json`; falta jogabilidade |

## Primeiro cenário de magia de campo

Todas as 13 magias de campo identificadas em P036 agora possuem efeitos básicos jogáveis no sandbox, reutilizando a zona FIELD e o mesmo cálculo dinâmico. Seis dependem de tipos, seis de atributos e Chorus depende da posição de defesa; Sogen se aplica a Warrior e Beast-Warrior. Os efeitos alcançam os dois lados do campo, e substituições removem os anteriores ao cemitério de seu dono. A interface existente aceita ativação da mão ou de carta setada. Para fontes e limites, consulte `docs/cards/p036-field-spells-sandbox.md`. Ainda não há paridade completa de interações/negação, e o mapeamento Sea Serpent = classe 5 precisa de conferência no DAT completo.

## Destiny Board / F-I-N-A-L (P259 + P009)

A sequência `Destiny Board` (ID 1528) + `Spirit Message`
I/N/A/L (1541–1544) possui agora uma primeira implementação
jogável no `DemoDuel`: ativação de Trap setada, uma mensagem
por End Phase do adversário, busca na mão/deck e vitória ao
reunir as cinco cartas face-up nas zonas de magia/armadilha.
O Core emite `DestinyBoardMessagePlaced` e
`DestinyBoardCompleted` para apresentação futura.

**Status: Partial / SandboxOnly**, com testes determinísticos
para ambos os jogadores. Ativação de Trap durante o turno
adversário, remoção conjunta, regras de resposta, IA e
animação nativa Vija continuam pendentes. A mera presença
dos cinco IDs conhecidos não basta para marcar o perfil
P259 como `Verified` ou completo em todos os duelos.
Ver [regras de F-I-N-A-L](docs/cards/destiny-board-sandbox.md).

## Próxima lacuna concreta

A carta **Mooyan Curry (resource ID 337)** exige escolher quem receberá 200 LP, portanto permanece fora da tabela de resolução automática. Adicionar seleção de jogador na interface/ações antes de considerá-la jogável. Famílias com efeito de resposta/negação continuam pendentes.

## Como atualizar

Ao implementar uma família: registrar os resource IDs **reais** no `docs/cards/playability-manifest.json`, mecânica acionável, testes que cobrem ativação/estado/custos, limites conhecidos e (se existir) a captura nativa. Não promover `Partial` a `Done` por testes de helper isolado; não promover a `Verified` por texto da carta ou por suposição.
