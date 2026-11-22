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
| Equipamentos `P025` | **Partial** (3 IDs) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo | Faltam demais famílias, supressões e interações |
| Magias de campo `P036` | **Done** (13 IDs no sandbox) | Seis magias por tipo, seis por atributo e Chorus of Sanctuary; zona FIELD, ativação/set, substituição, bônus/penalidades e combate | Regras baseadas em texto, não paridade nativa integral; Sea Serpent classCode 5 ainda é inferido |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes |
| Monstros de efeito FLIP (Man-Eater Bug, Morphing Jar, Penguin Soldier, Hane-Hane, Morphing Jar #2) | **Done** (5 IDs no sandbox) | `FlipEffectRules.cs`, `DemoDuel`, `FlipMonsterChecks.cs`, seleção na interface | Sem chains/negações; deck-out duplo do Morphing Jar é política simplificada |
| FLIP P018 (Magician of Faith e Mask of Darkness) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `CardSelectionChecks.cs`, UI raylib | Cemitério → mão; sem chains/negação ou UI nativa verificada |
| P082/P088 (Needle Worm, Hiro's Shadow Scout) | **Done** (2 IDs no sandbox) | `DeckFlowRules.cs`, `DemoDuel`, `FlipDeckFlowChecks.cs` | Descarte do topo do deck e compra com descarte de magias; sem paridade nativa |
| P006 (Sangan, Witch of the Black Forest) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `DeckSearcherChecks.cs` | Campo → cemitério, busca Deck → mão; não modela chains, controle alterado nem RNG nativo |
| P164 (seis recrutadores de atributo) | **Done** (6 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `BattleRecruiterChecks.cs`, UI raylib | Destruição em batalha, escolha opcional Deck → campo; paridade nativa ainda não verificada |
| P168 (Nimble Momonga e Giant Germ) | **Done** (2 IDs no sandbox) | `CardSelectionRules.cs`, `DemoDuel`, `BattleGraveyardEffectChecks.cs` | LP após destruição em batalha, invoca 0–2 cartas do mesmo nome; UI provisória |
| P169 (Spear Cretin) | **Done** (1 ID no sandbox) | `DemoDuel.MonsterWasFlipped`, `BattleGraveyardEffectChecks.cs` | FLIP diferido no envio ao cemitério, seleção para cada jogador; sem paridade nativa |
| P165 (Sonic Bird, Senju of the Thousand Hands) | **Done** (2 IDs no sandbox) | `RitualSearchRules.cs`, `DemoDuel`, `RitualSearcherChecks.cs`, UI raylib | Busca opcional por Magia/Monstro de Ritual no Normal/Flip Summon; não na revelação em batalha |
| P037/P039 (Dark Hole, Raigeki) | **Done** (2 magias no sandbox) | `MonsterWipeRules.cs`, `DemoDuel`, `MonsterWipeChecks.cs` | Destruição em massa sem alvo, enfileira buscas de Sangan/Witch e ressurreição de Spear Cretin; sem chains/negação |
| P196/P038 (Trap Hole, Torrential Tribute) | **Done** (2 armadilhas no sandbox) | `SummonTrapRules.cs`, `DemoDuel`, `SummonTrapChecks.cs`, UI raylib | Resposta opcional a Normal/Flip Summon, sem cadeias ou paridade nativa; Special Summon pendente |
| P200/P192 (Magic Cylinder e Negate Attack) | **Done** (2 armadilhas no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackNegationTrapChecks.cs` | Dano refletido e encerramento da Battle Phase; sem chains nem paridade nativa |
| P201 (Mirror Force) | **Done** (1 armadilha no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackTrapChecks.cs`, UI raylib | Resposta à declaração de ataque antes do dano/FLIP, destruição dos monstros em ATK; sem chains ou replay nativo |
| Outras armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Restante das famílias e das janelas de resposta ainda pendente |

**Todos os 499 monstros da união dos packs possuem classificação de referência:** 264 Normal, 193 Effect, 34 Fusion e 8 Ritual. O grupo Normal é `Done` **somente para o ciclo básico (invocação/tributos/set/batalha) no sandbox C#**, sem alegar reproduzir o jogo original. Efeitos de monstros, fusões e rituais permanecem `Pending`. A classificação de cartas foi cruzada por fontes públicas, e os bits de categoria originais devem ser conferidos com o DAT no Windows.

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
| 1122 | Sonic Bird | Done (sandbox) | No Normal ou Flip Summon pode buscar 1 Magia de Ritual do próprio Main Deck para a mão |
| 1109 | Senju of the Thousand Hands | Done (sandbox) | No Normal ou Flip Summon pode buscar 1 Monstro de Ritual do próprio Main Deck para a mão |
| 335 | Dark Hole | Done (sandbox) | Destrói todos os monstros de ambos os lados, sem disparar FLIP por simples destruição |
| 336 | Raigeki | Done (sandbox) | Destrói todos os monstros do adversário; ignora a posição sem selecionar alvos |
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
