# Campanha estática de efeitos

A unidade de cobertura é o resource ID do [catálogo de callbacks](../../evidence/card-callback-catalog.json). A classificação descreve o entendimento estático do efeito. Mapeado indica operação principal sustentada e informação suficiente para começar uma implementação. Parcial indica evidência útil com lacuna material. Pendente indica que a ligação entre a carta e a operação nativa permanece aberta. A campanha teve zero execuções do Joey.

Cada arquivo JSONL define registros `family` seguidos dos registros `card` que herdam os campos da família pelo nome. `detail` descreve o ramo ou parâmetro individual e `pending_card` registra lacunas específicas. Campos nulos representam informação ausente no nível da família. Os endereços são VAs hexadecimais do executável alvo.

<!-- cobertura -->

| Lote | Famílias | Registros | Mapeado | Parcial | Pendente |
|---|---:|---:|---:|---:|---:|
| [01-lp-destruicao-respostas](01-lp-destruicao-respostas.jsonl) | 7 | 35 | 23 | 12 | 0 |
| [02-zonas-e-modificadores](02-zonas-e-modificadores.jsonl) | 18 | 59 | 31 | 28 | 0 |
| [03-controle-e-eventos](03-controle-e-eventos.jsonl) | 20 | 36 | 23 | 13 | 0 |
| [04-regras-globais-e-continuacoes](04-regras-globais-e-continuacoes.jsonl) | 9 | 52 | 28 | 22 | 2 |
| [05-janelas-invocacao-batalha](05-janelas-invocacao-batalha.jsonl) | 8 | 21 | 8 | 13 | 0 |
| **Total** | 62 | 203 | 113 | 88 | 2 |

<!-- fim cobertura -->

São 202 IDs com identidade de carta e um registro sem identidade (1245). Os dois pendentes são 1245 e Minor Goblin Official (1302). IDs distintos com o mesmo nome permanecem distintos.

## Evidência e reprodução

- Base `scratch/joey-knowledge.sqlite`, aberta via `file:scratch/joey-knowledge.sqlite?mode=ro`. A tabela `metadata` identifica o executável por SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Há 6.518 decompilações e 36 dos 482 endereços de callback têm entrada decompilada.
- Para reconstruir o caminho de uma carta, consultar `card_callbacks` e `card_callback_slots` pelo ID, `decompilations` pelos endereços e `refs` pelos chamadores. Para entradas ausentes, os lotes podem usar instruções locais via `objdump -d -Mintel --start-address=<VA> --stop-address=<VA> ../passion/joey_pc.exe`, após conferir o SHA-256. As descrições do DAT inglês orientam a leitura e são parafraseadas.
- O lote 01 usou decompilação para 00584E60, 00585C80, 00588D90 e 00581790 e seus auxiliares disponíveis. As entradas 00584D00, 00585130, 00586540 e 0057AAA0 foram conferidas por instruções. A tabela em 0057AC28 indexa o salto em 0057AC14 a partir do ID 652. Os slots indicados como pendência são pontos de entrada para continuação.
- O lote 02 consultou a decompilação de 00579A00, 0057F360, 0057A630 e dos emissores disponíveis. As resoluções da tabela de callbacks foram conferidas por instruções, incluindo o desvio menor/maior em 00588442/0058844C e as três quantidades de compra em 005887E9–0058881A.
- O lote 03 cruzou instruções nos callbacks 00589030–0058BA80 com decompilações de 00585F50, 0058C080, 0058E2F0, 00591040 e 005914A0. Os caminhos de banimento em 0058C080 distinguem 1157 por classificação FLIP e 1158 pela classe armadilha.
- O lote 04 cruzou IDs em rotinas globais decompiladas, especialmente 0056B7D0, 0059CE90/0059CF30 e 0059D560–0059D790. As entradas que usam instruções registram o endereço da rotina ou o endereço exato do teste de ID. O salto de P248 foi conferido nas tabelas 005903F4/005903E4.
- O lote 05 inspecionou os callbacks por instruções e cruzou os filtros e custos de 0057C2B0 e 0057C450 com o SQLite. O ramo de Enchanted Javelin em 0058D6B8 e o de Mask of Weakness em 0058D801 mostram por que P200 e o endereço principal compartilhado precisam ser analisados por ID.
- A campanha reaproveita os estudos consolidados de [destruição por máscara](../../cards/monster-destruction.md), [custos de descarte](../../cards/tribute-discard-cost.md), [P025](../../cards/p025-preliminary.md), [rituais](../../cards/ritual-bindings.md) e [dano fixo P045](../../cards/p045-direct-damage.md). As cartas já cobertas por esses estudos ficam fora da contagem de novas investigações.

## Próximos grupos para o Core

1. **Destruição em massa por predicado.** Começar pelos sete destruidores por tipo de P092. Reusar uma operação de máscara com filtro de lado, face-up, tipo dinâmico e proteção. Acrescentar 4-Starred Ladybug of Doom com o filtro de nível e Heavy Storm com o filtro de zona. Eternal Rest depende de fechar os tipos de vínculo que entram na contagem.
2. **Compra, LP e movimentação entre zonas.** Reusar quantidade e lado como parâmetros. Começar por Pot of Greed, Jar of Greed, Skelengel e Upstart Goblin. Em seguida, usar um seletor com origem, filtro, cota e destino para Fusion Sage e P248. Graceful Charity e Card Destruction acrescentam composição ordenada e eventos de descarte.
3. **Seleção revalidada e efeitos vinculados.** Aplicar validação de ocorrência para Mystical Space Typhoon, Block Attack e Fissure. Expandir para renascimento com vínculo e modificadores temporários quando o motor puder expirar associações por movimento ou mudança de turno.
4. **Respostas e efeitos contínuos.** Modelar eventos de compra, descarte, invocação e ataque. Separar negar o pedido, destruir a origem e substituir sua operação, como mostram as respostas a Raigeki e Harpie’s Feather Duster. Reusar os predicados de autorização de ação em Gravity Bind, Insect Barrier e Mask of Restrict.

## Quando abrir o Joey original

- **Janelas concorrentes e ordem observável.** Comparar Solemn Judgment e Horn of Heaven em invocação aberta e durante outro efeito, os três caminhos de Magic Drain (sem candidato, recusa, descarte) e Michizure após saídas de campo com causas diferentes. Capturar a ordem dos pedidos e se as cartas chegam efetivamente ao campo ou ao cemitério.
- **Seleção e desempate.** Em Fissure e Widespread Ruin, colocar dois candidatos elegíveis com ATK igual e conferir quem escolhe e quais opções aparecem. Em Graceful Charity, observar compra insuficiente e se a interface permite encerrar antes de completar o descarte.
- **Estado por turno e ocorrências simultâneas.** Em Jam Breeding Machine, comparar Standby com zona livre e cheia. Em Solemn Wishes e Skull Invitation, distinguir um evento com várias cartas de vários eventos independentes. Comparar o contador do Cocoon of Evolution com as três primeiras janelas de invocação correspondentes.
- **Proteção e dependências em campo.** Comparar Ring of Destruction com alvo protegido e dano letal aos dois lados, além de renascimento vinculado quando a fonte é destruída, devolvida à mão ou usada em tributo.

O próximo recorte estático é seguir os consumidores de modificadores, contadores e vínculos já identificados. Os dois pendentes podem ser retomados em paralelo com a procura dos produtores de Standby e das entradas sem identidade. A execução do jogo agrega evidência de fluxo, escolha e temporização aos casos acima.

## Verificações da campanha

O SQLite retornou `ok` em `PRAGMA quick_check` e manteve o SHA-256 `cf5a29df75fe6fa86a655ecae82ead8e7c41c06477bbd2fd653f10b9b02664b5` do início ao fim. A conferência automatizada validou o JSONL, famílias referenciadas, unicidade de IDs, nomes, perfis, ponteiros dos slots e contagens contra a base e o catálogo versionado. A checagem de endereços confirma formato e faixa executável. As interpretações de comportamento são estáticas.
