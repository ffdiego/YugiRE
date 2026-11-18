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
| Equipamentos `P025` | **Partial** (3 IDs) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo | Faltam demais famílias, supressões e interações |
| Magias de campo `P036` | **Done** (13 IDs no sandbox) | Seis magias por tipo, seis por atributo e Chorus of Sanctuary; zona FIELD, ativação/set, substituição, bônus/penalidades e combate | Regras baseadas em texto, não paridade nativa integral; Sea Serpent classCode 5 ainda é inferido |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes |
| Armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Falta ciclo de resolução/gatilhos |

**Todos os 499 monstros da união dos packs possuem classificação de referência:** 264 Normal, 193 Effect, 34 Fusion e 8 Ritual. O grupo Normal é `Done` **somente para o ciclo básico (invocação/tributos/set/batalha) no sandbox C#**, sem alegar reproduzir o jogo original. Efeitos de monstros, fusões e rituais permanecem `Pending`. A classificação de cartas foi cruzada por fontes públicas, e os bits de categoria originais devem ser conferidos com o DAT no Windows.

## Monstros normais compartilhando uma implementação

Os 264 IDs Normal estão em `docs/cards/playability-manifest.json` no grupo `monster.normal`, e cada um é exercitado por `NormalMonsterPlayabilityChecks.Run` com propriedades sintéticas representativas dos níveis 1–8. No Windows, `Joey.Tests` com o `data.dat` também executa `RunWithCatalog`, usando nível, ATK/DEF e categoria **reais** de cada carta; essa última verificação não foi executada no CI sem DAT. Isso valida o escopo de sandbox, **não** 264 implementações de efeitos individuais ou paridade nativa. Identidades Normal históricas podem mudar entre edições, então divergências do DAT ainda devem ser revisadas.

## Cartas e magias jogáveis no sandbox (escopos delimitados)

| Resource ID | Carta | Status | Referência / escopo |
| ---: | --- | --- | --- |
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

## Próxima lacuna concreta

A carta **Mooyan Curry (resource ID 337)** exige escolher quem receberá 200 LP, portanto permanece fora da tabela de resolução automática. Adicionar seleção de jogador na interface/ações antes de considerá-la jogável. Famílias com efeito de resposta/negação continuam pendentes.

## Como atualizar

Ao implementar uma família: registrar os resource IDs **reais** no `docs/cards/playability-manifest.json`, mecânica acionável, testes que cobrem ativação/estado/custos, limites conhecidos e (se existir) a captura nativa. Não promover `Partial` a `Done` por testes de helper isolado; não promover a `Verified` por texto da carta ou por suposição.
