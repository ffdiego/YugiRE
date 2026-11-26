# CARDS.md — estado de cartas no YugiRE

Este é o inventário **funcional** de `Joey.Core`/`DemoDuel`, não uma contagem de cartas do DAT nem uma lista de callbacks decompilados. Atualize após implementar e testar, com escopo explícito.

**[Painel de progresso no GitHub](docs/cards/PLAYABILITY.md):** a badge no README apresenta Done+Verified da trilogia e a contagem separada de Partial; a cobertura de Joey consta no relatório. O GitHub Actions atualiza esses indicadores após mudanças no manifest.

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
| Blue Medicine (1006/P041) | **Done** (escopo sandbox na fila) | [C05](docs/cards/c05-queued-blue-medicine.md), `TimedEquipmentAndQueuedLpChecks.cs` | Mão/Set, gasto e HealCommand +400 LP; sem caminho legado novo ou paridade nativa. |
| Tremendous Fire (346/P053) | **Done** (escopo sandbox na fila) | `LifePointSpellRules.cs`, `TimedEquipmentAndQueuedLpChecks.cs` | App/CPU, mão/Set. O sandbox declara empate quando esse efeito zera os dois LP. |
| Monstros com atributos por contagem (10 IDs) | **Done** (escopo sandbox na fila) | `DemoCountedMonsterAttributes.cs`, `CountedMonsterAttributeChecks.cs` | ATK/DEF derivados de mão, campo e cemitério. Invocação e combate usam os produtores comuns. |
| White Magical Hat (113/P019) | **Done** (escopo sandbox na fila) | `DemoQueuedBattleDamageEffects.cs`, `MonsterBattleRuleChecks.cs` | Dano de batalha positivo e não letal causa um descarte aleatório, após o gate do dano. |
| Princess of Tsurugi (265/P044) | **Done** (escopo sandbox na fila) | `FlipEffectRules.cs`, `MonsterBattleRuleChecks.cs` | FLIP manual ou em batalha causa 500 por Magia ou Armadilha no campo adversário, incluindo a zona de Campo. |
| Curas de Standby (1437, 1438, 1441, 1446) | **Done** (4 IDs no sandbox na fila) | `DemoQueuedTurnDraw.cs`, `QueuedStandbyLifeChecks.cs` | Spirit of the Breeze ganha 1000 em ataque revelado, Dancing Fairy 1000 em defesa revelada, Cure Mermaid 800 revelada e Marie the Fallen One 200 no próprio cemitério. |
| Magias normais de compra fixa `P119/P143` | **Done** (2 IDs no sandbox) | Pot of Greed (1010) e Upstart Goblin (1065). `DrawSpellRules`, `DemoDuel` e `DrawSpellChecks` cobrem mão, Set, contagens, LP e uso pela CPU. | Exigem cartas suficientes no deck. Sem correntes, negação ou comparação nativa do timing. |
| Card Destruction (1221/P215) | **Done** (escopo sandbox na fila) | `DemoQueuedCardDestruction.cs`, `QueuedCardDestructionChecks.cs` | Gasta a magia, descarta ambas as mãos e repõe por quantidade descartada. Cobre deck-out e empate por Exodia simultâneo. |
| Equipamentos `P025` | **Done** (24 IDs, escopo sandbox) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo; [C04](docs/cards/c04-fixed-equipment.md) | Ativação legada da mão (21 IDs); EquipCommand mão/Set (24 IDs). [Ciclo fixo testado](docs/cards/c04-fixed-equipment-lifecycle.md); UI/CPU da fila e supressões pendentes; [dinâmicos](docs/cards/c04-dynamic-equipment-lifecycle.md) |
| Eternal Rest (655/P092) | **Done** (sandbox na fila, condicionado ao CI) | [C02](docs/cards/c02-eternal-rest.md), `QueuedMonsterWipeChecks.cs` | Destruição não seletiva de monstros equipados, ambos os lados; vínculos e limpeza por comando. Sem UI/CPU, correntes ou paridade nativa. |
| Fusion Sage (1040/P134) | **Done** (sandbox na fila, condicionado ao CI) | [C02.2](docs/cards/c02-fusion-sage.md), `QueuedDeckSearchChecks.cs` | Mão/Set, gasto, seleção obrigatória de Polymerization, transferência de ocorrência e shuffle. Sem UI/CPU ou paridade nativa. |
| Giant Trunade (1080/P152) | **Done** (sandbox na fila, condicionado ao CI) | [C02.3](docs/cards/c02-giant-trunade.md), `QueuedSpellTrapWipeChecks.cs` | Retorno das outras S/T dos dois lados, incluindo FIELD e equipamentos; sem destruição, UI/CPU, correntes ou paridade nativa. |
| Spiritualism (1539/P262) | **Done** (sandbox na fila, condicionado ao CI) | [C02.4](docs/cards/c02-spiritualism.md), `QueuedTargetedSpellTrapChecks.cs` | Retorno seletivo de S/T adversária, FIELD, vínculos e consequências de Destiny Board. Imunidade à negação depende de correntes; sem paridade nativa. |
| Tribute to The Doomed (1023/P074) | **Done** (sandbox na fila, condicionado ao CI) | [C02.5](docs/cards/c02-tribute-to-the-doomed.md), `QueuedMonsterWipeChecks.cs` | Descarte explícito de outra ocorrência, gasto e destruição seletiva, ambos lados e poses; consequências por comando. Sem correntes ou paridade nativa. |
| Thousand Knives (1211/P077) | **Done** (sandbox na fila, condicionado ao CI) | [C02.6](docs/cards/c02-thousand-knives.md), `QueuedMonsterWipeChecks.cs` | Dark Magician próprio face-up (34/1210/2034), alvo adversário, gasto e destruição seletiva sem custo. Guarda nativa especial e correntes pendentes. |
| Fissure (1001/P114) | **Done** (sandbox na fila, condicionado ao CI) | [C02.7](docs/cards/c02-fissure.md), `QueuedMonsterWipeChecks.cs` | Menor ATK efetivo adversário face-up, único automático, empate por ocorrência explícita. Sem correntes, ordem/prompt nativo não recuperados. |
| Rituais P269 | **Done** (8 magias, 7 monstros Ritual no sandbox, condicionado ao CI); **Partial** (Relinquished, 730) | [C09](docs/cards/c09-queued-ritual-summon.md), `QueuedRitualSummonChecks.cs` | Materiais próprios da mão/campo por ocorrência, gasto e tributos na fila. No modo fila, jogador e CPU usam a mesma admissão do console. Restrições de reviver, interações e efeito de 730 pendentes. |
| Offerings to the Doomed (1451/P105) | **Done** (sandbox na fila, condicionado ao CI) | [C02.8](docs/cards/c02-offerings-to-the-doomed.md), `QueuedMonsterWipeChecks.cs`, `RevivalJamDecisionChecks.cs` | Quick-Play na Main própria, alvo face-up de qualquer lado, skip da próxima Draw Phase sem bloquear compras por efeito ou Standby. Correntes/turno adversário pendentes. |
| Final Destiny (1067/P144) | **Done** (sandbox na fila, condicionado ao CI) | [C02.9](docs/cards/c02-final-destiny.md), `QueuedSpellTrapWipeChecks.cs` | Cinco descartes por ocorrência, gasto e destruição integral de ambos os campos; equipamentos sem duplicação. Sem correntes ou paridade nativa. |
| Modificadores temporários P123 (1015/1016/1075/1076/1082) | **Done** (sandbox headless na fila, condicionado ao CI) | [C02.9](docs/cards/c02-p123-temporary-stats.md), `QueuedTemporaryStatChecks.cs` | ATK/DEF por ocorrência até avanço de turno, Quick-Play de mão ou Set anterior. Sem correntes, Damage Step, UI/CPU ou comparação dinâmica. |
| Magias de campo `P036` | **Done** (13 IDs no sandbox) | Seis magias por tipo, seis por atributo e Chorus of Sanctuary; zona FIELD, ativação/set, substituição, bônus/penalidades e combate | DAT confirma Sea Serpent `classCode=5`. Comparação em duelo nativo pendente. Ativação Hand/Set migrada à fila e console em [C12.1](docs/cards/c12-queued-persistent-spells.md); UI mão/Set e ativação Fusion Gate CPU no modo fila; fusão e comparação gráfica pendentes. |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes. Ativação opt-in Hand/Set e produtor App migrados em C12.1; sem mudança de cobertura. |
| Dream Clown (119), gatilho de posição | **Done** (escopo sandbox, testes .NET CI) | `MonsterPositionEffectRules.cs`, `DemoDuel.ChangePosition`, `PositionChangeEffectChecks.cs` | Ataque → defesa inicia seleção e destruição de monstro adversário. O seletor de monstros é compartilhado com FLIP. O App em modo fila, a CPU e o console admitem um comando de alvo de posição, que preserva o gatilho mesmo sob Royal Command. Correntes e paridade nativa pendentes. |
| P168 / Bubonic Vermin (1307) | **Done** (sandbox, regressões .NET no CI) | `FlipEffectRules.cs`, `DemoDuel`, `BubonicVerminChecks.cs`, seletor de cartas existente | FLIP opcional: Deck → campo, 1 cópia por resource ID em defesa oculta. Gate de Royal Command integrado; sem chains ou paridade dinâmica. |
| P252 — Dark Necrofear (1514) | **Partial** | `GraveyardBanishSummonRules.cs`, `DemoQueuedGraveyardBanishSummon.cs`, `QueuedGraveyardBanishSummonChecks.cs` | Custo de 3 Fiends no próprio GY, invocação na fila via tabuleiro, CPU e console; Spirit Elimination, End Phase/equipamento e controle ainda pendentes. |
| C16 — Soul of Purity and Light, Spirit of Flames, recurso 1518 | **Done** (escopo headless C#) | [Atributos de batalha](docs/cards/c16-elemental-battle-attributes.md), `ElementalBattleAttributeChecks.cs` | Invocação por fila e modificadores de ATK em batalha; Tabuleiro, CPU e console admitem a invocação; comparação nativa pendente. |
| C16 — Aqua Spirit e Garuda (1517/1519) | **Partial** | [Contrato C16](docs/cards/c16-queued-graveyard-banish-summon.md) | Procedimento de invocação via tabuleiro, CPU e console; contrato puro de mudança de posição (Aqua Standby, Garuda End Phase adversárias), execução pendente. [P253](docs/cards/c16-elemental-position-opportunities.md). |
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
| P169 (Spear Cretin) | **Done** (1 ID no sandbox) | `BattleGraveyardEffectChecks.cs`, [contrato](docs/cards/c12-queued-graveyard-revival.md) | A fila de combate preserva o histórico de FLIP e oferece as duas escolhas após a remoção. Comparação nativa pendente. |
| P165 (Sonic Bird, Senju of the Thousand Hands) | **Done** (2 IDs no sandbox) | `RitualSearchRules.cs`, `DemoDuel`, `RitualSearcherChecks.cs`, UI raylib | Busca opcional por Magia/Monstro de Ritual no Normal/Flip Summon; não na revelação em batalha |
| Invocação de Fusão por receita | **Done** para 31 resultados dos packs, **Partial** para outros três | `FusionRules.cs`, `DemoFusion.cs`, `FusionChecks.cs`, seleção na Alpha | Respostas a Special Summon e os efeitos próprios de três resultados seguem pendentes |
| P037/P039 (Dark Hole, Raigeki) | **Done** (2 magias no sandbox) | `MonsterWipeRules.cs`, `DemoDuel`, `MonsterWipeChecks.cs` | Destruição em massa sem alvo, enfileira buscas de Sangan/Witch e ressurreição de Spear Cretin; sem chains/negação |
| P092 (destruição por tipo) | **Done** (7 magias no sandbox) | `MonsterWipeRules.cs`, `DemoDuel`, `MonsterWipeChecks.cs` | Monstros face-up do tipo indicado nos dois lados, inclusive em defesa |
| P196/P038 (Trap Hole, Torrential Tribute) | **Done** (2 armadilhas no sandbox) | `SummonTrapRules.cs`, `DemoQueuedSummonResponse.cs`, `SummonTrapChecks.cs`, UI raylib | Resposta opcional a Normal/Flip Summon. Passe e ativação pela fila após Normal Summon e no recorte de Flip Summon, incluindo seleções FLIP de cartas, monstros e pares de Magias/Armadilhas. Correntes, Special Summon e paridade nativa seguem pendentes. |
| P200/P192 (Magic Cylinder e Negate Attack) | **Done** (2 armadilhas no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackNegationTrapChecks.cs` | As duas armadilhas têm passe e ativação pela fila no piloto de combate direto e com alvo. Correntes e validação no original seguem pendentes. |
| P201 (Mirror Force) | **Done** (1 armadilha no sandbox) | `AttackTrapRules.cs`, `DemoDuel`, `AttackTrapChecks.cs`, UI raylib | Resposta à declaração de ataque antes do dano/FLIP. No piloto de combate, gasta a armadilha e destrói cada ocorrência em ATK separadamente. Correntes e replay nativo seguem pendentes. |
| Outras armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Restante das famílias e das janelas de resposta ainda pendente |

**Todos os 499 monstros da união dos packs possuem classificação de referência**. Há 264 Normal, 193 Effect, 34 Fusion e 8 Ritual. O grupo Normal é `Done` para o ciclo básico de invocação, tributos, Set e batalha no sandbox C#. A categoria dos resultados de fusão e dos monstros normais foi cruzada com os bits de categoria do DAT local.

## Monstros normais compartilhando uma implementação

Os 264 IDs Normal estão em `docs/cards/playability-manifest.json` no grupo `monster.normal`. `NormalMonsterPlayabilityChecks.Run` usa propriedades sintéticas representativas dos níveis 1–8. A execução local em Fedora/.NET 10 também passou em `RunWithCatalog` para os 264 IDs do DAT de referência, usando nível, ATK/DEF e categoria do jogo nos cenários do sandbox. O CI sem DAT executa a suíte sintética.

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
| 346 | Tremendous Fire | Done (sandbox) | `P053`, 1000 de dano no adversário e 500 no ativador pela fila |
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
| 333 | Umi | Done (sandbox) | Fish 8, Sea Serpent 5, Thunder 19, Aqua 14: +200 ATK/DEF; Machine 7, Pyro 4: -200/-200 |
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

White Magical Hat (113/P019) usa o mesmo encadeamento de dano de
batalha. Após o gate, o RNG do duelo escolhe uma ocorrência na mão
atual de quem sofreu o dano e a fila aplica um descarte. Mão vazia,
ataque cancelado e dano letal não geram descarte. A comparação do RNG
e das interações de descarte com o Joey segue pendente.
[Contrato de dano de batalha](docs/cards/c03-battle-damage-effects.md).

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

C05.2 acrescenta Tremendous Fire (346/P053) ao produtor de LP.
Os valores conferem com o DAT local e o estudo estático registra
a ordem adversário → ativador em `00585130`. A fila declara o
resultado após ambas as perdas. O sandbox trata como empate os
dois LP a zero no mesmo efeito. A comparação do resultado com
o executável original segue pendente.
[Estudo estático](docs/research/card-static-sweep/01-lp-destruicao-respostas.jsonl) ·
[Regra de empate](https://www.yugioh-card.com/eu/wp-content/uploads/2022/07/Rulebook_v9_en.pdf).

As magias fixas de LP usam `TryQueueLifePointSpell` para mão/Set no
App e na CPU, com gasto e mudanças de LP em comandos separados.
[Contrato](docs/cards/c12-queued-lp-spells.md). Pot of Greed e Upstart
Goblin usam `TryQueueDrawSpell`, com gasto antes da continuação,
DrawCommand por carta e HealCommand posterior de Upstart.
[Contrato](docs/cards/c12-queued-draw-spells.md).

Card Destruction (1221/P215) gasta a ocorrência ativada da mão ou
Set, descarta ambas as mãos e compra uma carta por descarte
concluído. O sandbox requer Deck suficiente para o ativador e
ao menos um descarte possível entre os dois lados. Se faltar Deck
ao adversário, a fila compra as cartas disponíveis antes de declarar
deck-out. As duas compras são tratadas como um efeito para decidir
Exodia e a execução apresenta as cartas do ativador primeiro. Essa
ordem, a prioridade de deck-out e os gatilhos de descarte aguardam
validação de interação com o Joey.
[Estudo estático](docs/research/card-static-sweep/04-regras-globais-e-continuacoes.jsonl) ·
[Disponibilidade e compras na regra oficial](https://www.db.yugioh-card.com/yugiohdb/faq_search.action?cid=4821&ope=4&request_locale=ja).

### Atributos por contagem (C03)

| Recursos | Modificador quando revelado |
| --- | --- |
| Swamp Battleguard (11), Lava Battleguard (553) | +500 ATK por cópia revelada do parceiro no próprio campo |
| Wodan the Resident of the Forest (234), Machine King (406) | +100 ATK por Planta ou Máquina, respectivamente, revelada em qualquer campo |
| Harpie's Pet Dragon (385) | +300 ATK/DEF por Harpie Lady revelada nos dois campos (61 ou 1249) |
| Shadow Ghoul (367) | +100 ATK por monstro no próprio cemitério |
| Dark Magician Girl (759) | +300 ATK por Dark Magician (34, 1210, 2034) ou Magician of Black Chaos (721) em ambos os cemitérios |
| Buster Blader (808) | +500 ATK por Dragão revelado no campo adversário ou no cemitério adversário |
| Muka Muka (515), Flash Assailant (1111) | +300 ou −400 ATK/DEF, respectivamente, por carta na própria mão |

A fila recalcula os atributos após movimentos e revelação. O sandbox
usa o tipo do DAT no filtro de campo. Mudanças dinâmicas de tipo e
comparação pareada com o Joey original continuam pendentes.
[Contagens de campo e cemitério](docs/research/card-static-sweep/10-efeitos-sem-registro.jsonl) ·
[Contagens adicionais](docs/research/card-static-sweep/11-ataque-invocacao-e-fases.jsonl).

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

### Recrutamento elemental na fila (C12)

P164: Mystic Tomato, Giant Rat, Shining Angel, UFO Turtle, Mother Grizzly, Flying Kamakiri #1 usam `TryQueueDeckRecruitment` após a batalha legada. Invocação especial em ataque e shuffle separados, recusa sem RNG. [Contrato](docs/cards/c12-queued-elemental-recruiters.md).

### Recrutamento de mesmo nome na fila (C12)

Nimble Momonga, Giant Germ e Bubonic Vermin usam `TryQueueSameNameRecruitment`; cada cópia tem uma invocação separada, com posição exigida e shuffle único após o lote. LP de batalha e FLIP de ataque ainda legados. [Contrato](docs/cards/c12-queued-same-name-recruitment.md).

### Ressurreição na fila (C12)

Spear Cretin usa `TryQueueGraveyardRevival`: um monstro do próprio GY por solicitação, excluindo a ocorrência disparadora, ataque face-up ou defesa face-down. A próxima escolha aguarda o gate. [Contrato](docs/cards/c12-queued-graveyard-revival.md).

### Movimentação do deck na fila (C12)

Needle Worm e Hiro's Shadow Scout: Flip Summon manual emite movimentações individuais, com auditoria `LastDeckFlow` após o gate. Mill não é compra; Scout descarta apenas Spells recém-compradas. Batalha/reveal externo continuam legados. [Contrato](docs/cards/c12-queued-flip-deck-flow.md).

## Evidências nativas (não confundir com paridade C# completa)

| Resource ID | Carta / família | Estado do conhecimento |
| ---: | --- | --- |
| 332 | Sogen | 69 snapshots de atributos de batalha no piloto; 6 ocorrências classe15 com +200/+200. No sandbox, também inclui Beast-Warrior (classe12) conforme descrição, mas **sem comparação dinâmica nativa dessa classe** |
| 1315 | Rain of Mercy | Aumento de 1000 LP observado em captura do original; efeito simples agora jogável no sandbox, sem paridade completa |
| 1306 | Skull Invitation | Correlação de perdas de 300 LP e controle negativo observacional |
| 1353 | Burning Land | Ticks de 500 LP relatados em evidência, ainda sem regra C# completa |
| — | Rituais `P269` | Jogador, CPU e console admitem pela fila; restrições restantes em C09 |

## Primeiro cenário de magia de campo

Todas as 13 magias de campo identificadas em P036 agora possuem efeitos básicos jogáveis no sandbox, reutilizando a zona FIELD e o mesmo cálculo dinâmico. Seis dependem de tipos, seis de atributos e Chorus depende da posição de defesa; Sogen se aplica a Warrior e Beast-Warrior. Os efeitos alcançam os dois lados do campo, e substituições removem os anteriores ao cemitério de seu dono. A interface existente aceita ativação da mão ou de carta setada. O DAT de referência confirma a classe 5 para Sea Serpent. A validação de interações e negações em duelo nativo permanece pendente. [Evidências](docs/cards/p036-field-spells-sandbox.md).

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
