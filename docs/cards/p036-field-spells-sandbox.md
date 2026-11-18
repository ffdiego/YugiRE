# P036 — primeiras sete magias de campo jogáveis no sandbox

Escopo **C# sandbox**, não afirma paridade integral com as funções nativas. Este arquivo é um guia de implementação para agentes, não uma campanha adicional de RE.

## Identidades (catálogo de callback local)

`docs/evidence/card-callback-catalog.json` associa os resource IDs ao perfil P036 (callback primário `0x00584A90`):

| ID | Índice interno | Carta | Alvo |
| ---: | ---: | --- | --- |
| 332 | 490 | Sogen | Warrior tipo nativo 15: +200/+200, comprovado em seis snapshots de `0x31` |
| 1125 | 134 | Gaia Power | EARTH: +500 ATK/-400 DEF |
| 1126 | 79 | Umiiruka | WATER: +500 ATK/-400 DEF |
| 1127 | 691 | Molten Destruction | FIRE: +500 ATK/-400 DEF |
| 1128 | 584 | Rising Air Current | WIND: +500 ATK/-400 DEF |
| 1129 | 387 | Luminous Spark | LIGHT: +500 ATK/-400 DEF |
| 1130 | 511 | Mystic Plasma Zone | DARK: +500 ATK/-400 DEF |

Os valores das seis cartas por atributo provêm de suas descrições (cartas de efeito de campo na base oficial, no contexto do baralho da época): https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=8b6a781a7a1c6706863119d19463a27d&dno=27&ope=1&request_locale=en&wname=MemberDeck&ytkn=841630361e23375bb7ed3398802155ebd9532dfe11cf9e41794a1978616d50

## Mapeamento do atributo nativo

O bitfield de `Card.Properties` coloca o atributo de monstro em `Properties >> 29` (3 bits). A tabela já usada pela RE em `NativeAiAttributes.cs` para elegibilidade dos equipamentos ajuda a vincular código a nome:

| AttributeCode | Atributo | Carta de equipamento cujo filtro aponta para o código |
| ---: | --- | --- |
| 1 | LIGHT | Elf's Light (306) |
| 2 | DARK | Sword of Dark Destruction (301) |
| 3 | WATER | Steel Shell (308) |
| 4 | FIRE | Burning Spear (1012) |
| 5 | EARTH | Invigoration (323) |
| 6 | WIND | Gust Fan (1013) |

A interpretação desses códigos para o sandbox é apoiada por esses filtros e pelos textos dessas cartas; **não** valida transformações dinâmicas de atributo, supressão, equip alterado ou todos os ramos do cálculo nativo.

## Contrato jogável

- Cada jogador tem uma zona FIELD independente dos cinco slots comuns; ao ativar uma magia de campo, a anterior ativa sai para o cemitério de seu dono.
- A carta setada fica inativa e pode ser ativada na Main Phase clicando na zona visual FIELD. A ativação de uma mão livre também é suportada.
- `FieldSpellRules.Bonus` recebe o resource ID do campo e código de tipo/atributo do monstro; a contribuição é calculada ao consultar ATK/DEF, não aplicada ao valor base.
- Atribuição de bônus é global (dois lados), apenas aos monstros que não estão virados para baixo. Um mesmo campo não acumula bônus duplicados. Equipamentos e campo somam seus deltas **antes da saturação final** em `NativeAiAttributes.ApplyResolvedTail`.
- O resultado afeta a batalha pelo uso de `DemoDuel.AttackValue`/`DefenseValue` na comparação de atributos.
- Os seis efeitos por atributo são funcionais no escopo da descrição. `Sogen` **não** inclui Beast-Warrior ainda: só há cobertura de Warrior classCode 15, de acordo com `sogen-battle-attribute-pilot-validated-2026-11-17.md`.

## Ainda pendente

Os outros seis resource IDs de P036 (329 Forest, 330 Wasteland, 331 Mountain, 333 Umi, 334 Yami, 1069 Chorus of Sanctuary) não foram implementados. Falta determinar a associação completa tipo de monstro → classCode e particularidades de cada campo, incluindo penalidades. Não inferir tipos pela posição do campo ou pelo índice interno; não promover a Verified sem experimento comparativo.
