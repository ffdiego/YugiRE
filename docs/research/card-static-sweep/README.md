# Campanha estática de efeitos

A unidade de cobertura é o resource ID. Os lotes 01–09 examinam o [catálogo de callbacks](../../evidence/card-callback-catalog.json). Os lotes 10–15 usam IDs do DAT ausentes da tabela. A classificação descreve o entendimento estático do comportamento no recorte da família. Mapeado indica operação principal sustentada e informação suficiente para começar uma implementação. Parcial indica evidência útil com lacuna material. Pendente indica que a ligação entre a carta e a operação nativa permanece aberta. A campanha teve zero execuções do Joey.

Cada arquivo JSONL define registros `family` seguidos dos registros `card` que herdam os campos da família pelo nome. `detail` descreve o ramo ou parâmetro individual e `pending_card` registra lacunas específicas. Campos nulos no registro de família representam informação ausente. Em um registro de carta, `profile: null` indica ausência na tabela de callbacks, distinta de um perfil registrado com slots vazios. Os endereços são VAs hexadecimais do executável alvo.

<!-- cobertura -->

| Lote | Famílias | Registros | Mapeado | Parcial | Pendente |
|---|---:|---:|---:|---:|---:|
| [01-lp-destruicao-respostas](01-lp-destruicao-respostas.jsonl) | 7 | 35 | 29 | 6 | 0 |
| [02-zonas-e-modificadores](02-zonas-e-modificadores.jsonl) | 18 | 59 | 40 | 19 | 0 |
| [03-controle-e-eventos](03-controle-e-eventos.jsonl) | 20 | 36 | 27 | 9 | 0 |
| [04-regras-globais-e-continuacoes](04-regras-globais-e-continuacoes.jsonl) | 9 | 52 | 38 | 13 | 1 |
| [05-janelas-invocacao-batalha](05-janelas-invocacao-batalha.jsonl) | 8 | 21 | 10 | 11 | 0 |
| [06-combate-diferido-e-invocacao](06-combate-diferido-e-invocacao.jsonl) | 12 | 34 | 20 | 14 | 0 |
| [07-selecao-vinculos-e-substituicao](07-selecao-vinculos-e-substituicao.jsonl) | 14 | 32 | 9 | 23 | 0 |
| [08-efeitos-compostos-e-eventos](08-efeitos-compostos-e-eventos.jsonl) | 15 | 41 | 16 | 23 | 2 |
| [09-efeitos-fora-da-tabela](09-efeitos-fora-da-tabela.jsonl) | 7 | 16 | 10 | 4 | 2 |
| [10-efeitos-sem-registro](10-efeitos-sem-registro.jsonl) | 6 | 33 | 30 | 3 | 0 |
| [11-ataque-invocacao-e-fases](11-ataque-invocacao-e-fases.jsonl) | 11 | 39 | 22 | 17 | 0 |
| [12-receitas-de-fusao](12-receitas-de-fusao.jsonl) | 1 | 53 | 53 | 0 | 0 |
| [13-resultados-de-ritual](13-resultados-de-ritual.jsonl) | 1 | 15 | 15 | 0 | 0 |
| [14-monstros-normais](14-monstros-normais.jsonl) | 1 | 526 | 526 | 0 | 0 |
| [15-reconciliacao-residual](15-reconciliacao-residual.jsonl) | 3 | 6 | 2 | 0 | 4 |
| **Total** | 133 | 998 | 847 | 142 | 9 |

<!-- fim cobertura -->

O inventário cobre **1.114 dos 1.114 IDs do DAT inglês**, incluindo **771/771** dos três packs. São 997 IDs do DAT classificados no JSONL e 117 com estudos anteriores ligados pela [reconciliação](reconciliacao.md). O 998º registro do JSONL, 1245, tem callback e não está no inventário do DAT. Os 672 IDs do DAT sem callback estão nos lotes 10–15. Entre os 998 registros, permanecem Pendente quatro sem nome confirmado (1245, 1251, 1257 e 1258), The Unfriendly Amazon (1431) e quatro recursos de classes 23/24 com pack zero. IDs distintos com o mesmo nome permanecem distintos.

| Recorte do DAT | Mapeado no JSONL | Parcial no JSONL | Pendente no JSONL | Estudo anterior | Total |
|---|---:|---:|---:|---:|---:|
| Todos os packs | 534 | 132 | 1 | 104 | 771 |
| Pack Joey | 215 | 87 | 1 | 47 | 350 |

Duas revisões passaram 39 entradas a Mapeado após confirmar rotinas de pagamento, modificadores, invocação, encerramento, restrições de ataque e supressão. O campo `additional_evidence` complementa a interpretação original no registro de família ou carta. A duração dos modificadores temporários vem do DAT, com os deltas confirmados no consumidor em 0056DD30. As rotinas 00598D70 e 00598C40 separam fonte de supressão, máscara por classe e estado de cada carta aberta.

As receitas cobrem os 57 resultados de fusão do DAT, dos quais 34 têm indicação de pack. Há 52 linhas de dois materiais a partir de `005ECF10` e três de três materiais a partir de `005ED048`. Flame Swordsman (2014) e Thousand Dragon (2068) são variantes que usam a receita dos IDs 14 e 68. O ID 0 da receita de Blue-Eyes Ultimate Dragon é Blue-Eyes White Dragon. Os campos `existing_card_recipes` e `existing_card_rules` complementam entradas existentes sem contar seus IDs de novo. Os 16 resultados de Ritual têm par e nível documentados, dos quais oito aparecem nos packs. A classificação por receita de invocação preserva separadamente a classificação do efeito próprio de cada monstro.

Os 528 monstros de categoria nativa Normal se dividem em 264 com indicação de pack e 264 de pack zero. O lote 14 documenta 526 desses IDs pela regra base. Os IDs 81 e 390 continuam com as entradas anteriores que registram seu papel em efeitos de associação. O lote 15 liga Exodia ao [predicado central de vitória](../../23-terminal-conditions.md) e registra a Normal Summon sem tributo do ID 1250 quando é a única carta da mão e existe vaga.

## Evidência e reprodução

- Base `scratch/joey-knowledge.sqlite`, aberta via `file:scratch/joey-knowledge.sqlite?mode=ro`. A tabela `metadata` identifica o executável por SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Há 6.518 decompilações e 36 dos 482 endereços de callback têm entrada decompilada.
- Para reconstruir o caminho de uma carta, consultar `card_callbacks` e `card_callback_slots` pelo ID, `decompilations` pelos endereços e `refs` pelos chamadores. Para entradas ausentes, os lotes podem usar instruções locais via `objdump -d -Mintel --start-address=<VA> --stop-address=<VA> ../passion/joey_pc.exe`, após conferir o SHA-256. As descrições do DAT inglês orientam a leitura e são parafraseadas.
- O lote 01 usou decompilação para 00584E60, 00585C80, 00588D90 e 00581790 e seus auxiliares disponíveis. As entradas 00584D00, 00585130, 00586540 e 0057AAA0 foram conferidas por instruções. A tabela em 0057AC28 indexa o salto em 0057AC14 a partir do ID 652. Os slots indicados como pendência são pontos de entrada para continuação.
- O lote 02 consultou a decompilação de 00579A00, 0057F360, 0057A630 e dos emissores disponíveis. As resoluções da tabela de callbacks foram conferidas por instruções, incluindo o desvio menor/maior em 00588442/0058844C e as três quantidades de compra em 005887E9–0058881A.
- O lote 03 cruzou instruções nos callbacks 00589030–0058BA80 com decompilações de 00585F50, 0058C080, 0058E2F0, 00591040 e 005914A0. Os caminhos de banimento em 0058C080 distinguem 1157 por classificação FLIP e 1158 pela classe armadilha.
- O lote 04 cruzou IDs em rotinas globais decompiladas, especialmente 0056B7D0, 0059CE90/0059CF30 e 0059D560–0059D790. As entradas que usam instruções registram o endereço da rotina ou o endereço exato do teste de ID. O salto de P248 foi conferido nas tabelas 005903F4/005903E4.
- O lote 05 inspecionou os callbacks por instruções e cruzou os filtros e custos de 0057C2B0 e 0057C450 com o SQLite. O ramo de Enchanted Javelin em 0058D6B8 e o de Mask of Weakness em 0058D801 mostram por que P200 e o endereço principal compartilhado precisam ser analisados por ID.
- O lote 06 inspecionou as entradas de resolução e cruzou a elegibilidade de Elegant Egotist e Polymerization com 0057F640 e 00580600. Os callbacks 0058CDF0 e 0058D970 sustentam a infraestrutura compartilhada; as diferenças de gatilho, filtro e benefício recorrente ficaram registradas por ID.
- O lote 07 complementou as instruções dos resolvedores com 0057A470, 0057B3B0, 00580360 e 00589B70 no SQLite. 00586B30 distingue o efeito original por ID ao proteger armadilhas, e 00586FB0 dá a Relinquished e Thousand-Eyes Restrict o mesmo caminho de transferência e vínculo tipo 5.
- O lote 08 cruzou as continuações de 00589EB0–00591890 com a disponibilidade de Chosen One em 005829D0 e o evento de Major Riot em 00581790. P166/P167 e os efeitos de destruição em batalha foram mantidos separados quando o callback confirmava apenas a ativação principal.
- O lote 09 combinou os predicados globais do SQLite com instruções que verificam IDs nos estágios de batalha e de turno. Os ramos 005A627D, 005A637D e 005A647E distinguem os ganhos de LP por posição; 00582330 e 005A6D06 fecharam disponibilidade e dano de Minor Goblin Official.
- O lote 10 cruzou `CARD_ID` com a tabela e verificou as identidades no DAT inglês. A tabela de seis auras em 005DCF98 contém IDs e sete deltas int16 por linha, lidos a partir do offset 0x001DCF98 no EXE identificado acima. Os saltos de 00583F40 confirmaram destinos distintos para Red-Eyes e Magical Labyrinth, corrigidos nos lotes 02 e 07.
- O lote 11 cruzou os predicados de combate, turnos e invocação com o campo que 00402740 lê em `CARD_PROP`. Entre os caminhos de resolução examinados estão o débito de ataque em 005A1B98/005A1D5B, os três tributos de Gate Guardian em 0059DF40 e a seleção de Sinister Serpent em 005A7188–005A72E6.
- O lote 12 leu palavras uint16 little-endian da seção `.data`, nos offsets de arquivo `0x1ECF10` e `0x1ED048`, segundo os limites e strides de 00591D10. 00591A10 confirmou as origens e a exclusão de ocorrências já usadas. Os saltos em 005927F0 e 005928AA distinguem o destino dos materiais sob Fusion Gate. O lote 13 conferiu os pares e níveis do [estudo de Ritual](../../cards/ritual-bindings.md) contra a tabela em `005ED060`, a sentinela e os níveis do DAT.
- Os lotes 14–15 usam o [acessor de categoria](../../02-file-formats.md#card_propbin--quarta-rodada) e os [gates de invocação normal](../../26-normal-summon-tributes.md). O conjunto integral dos 264 monstros de categoria 0 com pack coincide por ID com `monster.normal` do manifesto. Para 1250, a comparação em 005AAC8C e os estados 0x5A/0x5B de 0059DF40 confirmam o ramo; o DAT local deixa o nome vazio.
- A campanha reaproveita os estudos consolidados de [destruição por máscara](../../cards/monster-destruction.md), [custos de descarte](../../cards/tribute-discard-cost.md), [P025](../../cards/p025-preliminary.md), [rituais](../../cards/ritual-bindings.md) e [dano fixo P045](../../cards/p045-direct-damage.md). As cartas já cobertas por esses estudos ficam fora da contagem de novas investigações.

## Próximos grupos para o Core

1. **Destruição em massa por predicado.** Começar pelos sete destruidores por tipo de P092. Reusar uma operação de máscara com filtro de lado, face-up, tipo dinâmico e proteção. Acrescentar 4-Starred Ladybug of Doom com o filtro de nível e Heavy Storm com o filtro de zona. Eternal Rest depende de fechar os tipos de vínculo que entram na contagem.
2. **Compra, LP e movimentação entre zonas.** Reusar quantidade e lado como parâmetros. Começar por Pot of Greed, Jar of Greed, Skelengel e Upstart Goblin. Em seguida, usar um seletor com origem, filtro, cota e destino para Fusion Sage e P248. Graceful Charity e Card Destruction acrescentam composição ordenada e eventos de descarte.
3. **Auras e ataque direto.** Usar uma tabela de contribuições por atributo dinâmico e quantidade de fontes, junto ao recálculo de ATK. Para ataque direto, usar permissões por carta e um predicado de campo separado para Alligator’s Sword Dragon.
4. **Seleção revalidada e efeitos vinculados.** Aplicar validação de ocorrência para Mystical Space Typhoon, Block Attack e Fissure. Expandir para renascimento com vínculo e modificadores temporários quando o motor puder expirar associações por movimento ou mudança de turno.
5. **Respostas e efeitos contínuos.** Modelar eventos de compra, descarte, invocação e ataque. Separar negar o pedido, destruir a origem e substituir sua operação, como mostram as respostas a Raigeki e Harpie’s Feather Duster. Reusar os predicados de autorização de ação em Gravity Bind, Insect Barrier e Mask of Restrict.
6. **Invocação por receita.** Reusar seleção de ocorrências e liberação de vaga para Fusion e Ritual. Começar com Polymerization, uma receita de dois materiais e outra de materiais repetidos. Na família Ritual, separar a cópia a ser invocada do conjunto de tributos e modelar a soma mínima sem material redundante.

## Quando abrir o Joey original

- **Janelas concorrentes e ordem observável.** Comparar Solemn Judgment e Horn of Heaven em invocação aberta e durante outro efeito, os três caminhos de Magic Drain (sem candidato, recusa, descarte) e Michizure após saídas de campo com causas diferentes. Capturar a ordem dos pedidos e se as cartas chegam efetivamente ao campo ou ao cemitério.
- **Seleção e desempate.** Em Fissure e Widespread Ruin, colocar dois candidatos elegíveis com ATK igual e conferir quem escolhe e quais opções aparecem. Em Graceful Charity, observar compra insuficiente e se a interface permite encerrar antes de completar o descarte.
- **Estado por turno e ocorrências simultâneas.** Verificar se Minor Goblin Official continua causando dano após os LP adversários subirem acima de 3000. Comparar combate interrompido de Hayabusa Knight e a sequência ataque → defesa de Goblin Attack Force. Em Jam Breeding Machine, comparar Standby com zona livre e cheia. Em Solemn Wishes e Skull Invitation, distinguir um evento com várias cartas de vários eventos independentes. Comparar o contador do Cocoon of Evolution com as três primeiras janelas de invocação correspondentes.
- **Proteção e dependências em campo.** Comparar Ring of Destruction com alvo protegido e dano letal aos dois lados, além de renascimento vinculado quando a fonte é destruída, devolvida à mão ou usada em tributo.

O próximo recorte estático é seguir os consumidores de modificadores, contadores e vínculos já identificados. Para The Unfriendly Amazon, o próximo recorte é procurar o produtor de tributo de Standby em tabelas indexadas. Para os quatro IDs sem nome, a pendência é identificar ocorrências alcançáveis no jogo. A execução do jogo agrega evidência de fluxo, escolha e temporização aos casos acima.

## Verificações da campanha

O SQLite retornou `ok` em `PRAGMA quick_check` e manteve o SHA-256 `cf5a29df75fe6fa86a655ecae82ead8e7c41c06477bbd2fd653f10b9b02664b5` do início ao fim. A conferência automatizada validou o JSONL, famílias referenciadas, unicidade de IDs, nomes, perfis, ponteiros dos slots e contagens contra a base e o catálogo versionado. A checagem de endereços confirma formato e faixa executável. As interpretações de comportamento são estáticas.
