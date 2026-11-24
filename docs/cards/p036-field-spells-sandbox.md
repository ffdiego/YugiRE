# P036 — 13 magias de campo jogáveis no sandbox C#

**Escopo:** regras de jogo baseadas no texto das cartas; **não** uma implementação integral do callback original `0x00584A90`, do timing de 2004 ou das cadeias/negações. O piloto nativo `sogen-battle-attribute-pilot-validated-2026-11-17.md` confirmou +200/+200 **apenas para Warrior (classe15)**, não para todos os efeitos aqui descritos.

## Catálogo P036: 13 resource IDs distintos

Fonte interna: `docs/evidence/card-callback-catalog.json`, com índices internos e perfil P036.

| Resource ID | Índice interno | Carta | Regra sandbox |
| ---: | ---: | --- | --- |
| 329 | 990 | Forest | Insect (10), Beast (11), Beast-Warrior (12), Plant (13): +200 ATK/DEF |
| 330 | 287 | Wasteland | Dinosaur (9), Zombie (2), Rock (6): +200 ATK/DEF |
| 331 | 1004 | Mountain | Dragon (1), Winged Beast (16), Thunder (19): +200 ATK/DEF |
| 332 | 490 | Sogen | Warrior (15), Beast-Warrior (12): +200 ATK/DEF |
| 333 | 86 | Umi | Fish (8), Sea Serpent (5), Thunder (19), Aqua (14): +200 ATK/DEF; Machine (7) e Pyro (4): -200 ATK/DEF |
| 334 | 1008 | Yami | Fiend (3), Spellcaster (18): +200 ATK/DEF; Fairy (17): -200 ATK/DEF |
| 1069 | 460 | Chorus of Sanctuary | +500 DEF de monstro **em posição de defesa** |
| 1125 | 134 | Gaia Power | EARTH: +500 ATK, -400 DEF |
| 1126 | 79 | Umiiruka | WATER: +500 ATK, -400 DEF |
| 1127 | 691 | Molten Destruction | FIRE: +500 ATK, -400 DEF |
| 1128 | 584 | Rising Air Current | WIND: +500 ATK, -400 DEF |
| 1129 | 387 | Luminous Spark | LIGHT: +500 ATK, -400 DEF |
| 1130 | 511 | Mystic Plasma Zone | DARK: +500 ATK, -400 DEF |

As regras por tipo e posição foram cruzadas com a base oficial (texto TCG; não certificado do comportamento nativo do jogo):
- Forest https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4336&ope=2&request_locale=en
- Wasteland https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4337&ope=2&request_locale=en
- Mountain https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4338&ope=2&request_locale=en
- Umi https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4340&ope=2&request_locale=en
- Yami https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4341&ope=2&request_locale=en
- Chorus of Sanctuary https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4899&ope=2&request_locale=en

## Como interpretamos o ClassCode

O `Card.ClassCode` em `Archive.cs` vem de `(Properties >> 20) & 31`. Os exemplos de monstros já nomeados em `docs/evidence/card-callback-catalog.json` permitem associar:
- 1 Dragon (Red-Eyes); 2 Zombie (Red-Moon Baby); 3 Fiend (Sangan); 4 Pyro (Spirit of Flames)
- 6 Rock (Morphing Jar); 7 Machine (Jinzo); 8 Fish (Cure Mermaid); 9 Dinosaur (Gilasaurus)
- 10 Insect (Man-Eater Bug); 11 Beast (Giant Rat); 12 Beast-Warrior (Mother Grizzly); 13 Plant (Mystic Tomato); 14 Aqua (Penguin Soldier)
- 15 Warrior (The Legendary Fisherman); 16 Winged Beast (Sonic Bird); 17 Fairy (Shining Angel); 18 Spellcaster (Magician of Faith); 19 Thunder (Thunder Dragon)

O DAT de referência confirma `classCode=5` nas entradas dos packs: Takriminos (ID 352, índice 535) e Spike Seadra (ID 447, índice 451). As fichas oficiais classificam as duas cartas como Sea Serpent. `TypeFieldSpellChecks.RunWithCatalog` verifica o código no teste opcional com DAT. [Takriminos no catálogo oficial](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4359&ope=2&request_locale=en) · [Spike Seadra no catálogo oficial](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4454&ope=2&request_locale=en).

Os códigos de atributo vêm de `Properties >> 29` e dos filtros de equipamentos em `NativeAiAttributes.cs`:
LIGHT=1 (Elf's Light), DARK=2 (Sword of Dark Destruction), WATER=3 (Steel Shell), FIRE=4 (Burning Spear), EARTH=5 (Invigoration), WIND=6 (Gust Fan).

## Semântica de jogo no DemoDuel

- Uma zona FIELD por lado, independente dos cinco slots comuns de magia/armadilha. Só um campo permanece ativo globalmente; quando outro ativa, o anterior vai ao cemitério do proprietário.
- Qualquer um dos 13 campos pode ser ativado da mão ou setado e ativado durante a Main Phase. A UI raylib já usa a zona FIELD para ativar campos setados.
- Campos aplicam seus modificadores aos **dois jogadores**, calculados por consulta de ATK/DEF; não alteram o `Properties` base do monstro e não acumulam bônus antigos após remoção/substituição.
- Bônus de equipamentos e de campo somam antes do clamp final 0 em `NativeAiAttributes.ApplyResolvedTail`. Penalty de Umi/Yami aplica -200 em ambas dimensões; Chorus aplica +500 DEF **somente** em posição de defesa. Monstros virados para baixo são omitidos do cálculo dinâmico nesta política sandbox; na batalha, a defesa é revelada antes do cálculo.
- Atenção: field spell não é vínculo de equipamento. O `NativeAiAttributeContext.Resolved` de RE antiga não implica resolver campos sem modelagem explícita.

## Testes e limites

`FieldSpellChecks`, `AttributeFieldSpellChecks` e `TypeFieldSpellChecks` cobrem tipos/atributos/posições, ambos os jogadores, ativação/set, substituição, dano real de batalha, clamp, cemitério e composições com equipamento. Não exigem DAT original.

Pendências em duelo nativo: comparar o modificador para a classe 5, validar a visualização e investigar janelas de resposta, negação, efeitos de mudança de tipo/atributo, faces ocultas e efeitos contínuos. O status continua Done no sandbox.
