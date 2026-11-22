# Campanha estática de efeitos

A unidade de cobertura é o resource ID do [catálogo de callbacks](../../evidence/card-callback-catalog.json). A classificação descreve o entendimento estático do efeito. Mapeado indica operação principal sustentada e informação suficiente para começar uma implementação. Parcial indica evidência útil com lacuna material. Pendente indica que a ligação entre a carta e a operação nativa permanece aberta. A campanha teve zero execuções do Joey.

Cada arquivo JSONL define registros `family` seguidos dos registros `card` que herdam os campos da família pelo nome. `detail` descreve o ramo ou parâmetro individual e `pending_card` registra lacunas específicas. Campos nulos representam informação ausente no nível da família. Os endereços são VAs hexadecimais do executável alvo.

<!-- cobertura -->

| Lote | Famílias | Cartas | Mapeado | Parcial | Pendente |
|---|---:|---:|---:|---:|---:|
| [01-lp-destruicao-respostas](01-lp-destruicao-respostas.jsonl) | 7 | 35 | 23 | 12 | 0 |
| [02-zonas-e-modificadores](02-zonas-e-modificadores.jsonl) | 18 | 59 | 31 | 28 | 0 |
| [03-controle-e-eventos](03-controle-e-eventos.jsonl) | 20 | 36 | 23 | 13 | 0 |
| [04-regras-globais-e-continuacoes](04-regras-globais-e-continuacoes.jsonl) | 9 | 52 | 28 | 22 | 2 |
| **Total** | 54 | 182 | 105 | 75 | 2 |

<!-- fim cobertura -->

## Evidência e reprodução

- Base `scratch/joey-knowledge.sqlite`, aberta via `file:scratch/joey-knowledge.sqlite?mode=ro`. A tabela `metadata` identifica o executável por SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Há 6.518 decompilações e 36 dos 482 endereços de callback têm entrada decompilada.
- Para reconstruir o caminho de uma carta, consultar `card_callbacks` e `card_callback_slots` pelo ID, `decompilations` pelos endereços e `refs` pelos chamadores. Para entradas ausentes, os lotes podem usar instruções locais via `objdump -d -Mintel --start-address=<VA> --stop-address=<VA> ../passion/joey_pc.exe`, após conferir o SHA-256. As descrições do DAT inglês orientam a leitura e são parafraseadas.
- O lote 01 usou decompilação para 00584E60, 00585C80, 00588D90 e 00581790 e seus auxiliares disponíveis. As entradas 00584D00, 00585130, 00586540 e 0057AAA0 foram conferidas por instruções. A tabela em 0057AC28 indexa o salto em 0057AC14 a partir do ID 652. Os slots indicados como pendência são pontos de entrada para continuação.
- O lote 02 consultou a decompilação de 00579A00, 0057F360, 0057A630 e dos emissores disponíveis. As resoluções da tabela de callbacks foram conferidas por instruções, incluindo o desvio menor/maior em 00588442/0058844C e as três quantidades de compra em 005887E9–0058881A.
- O lote 03 cruzou instruções nos callbacks 00589030–0058BA80 com decompilações de 00585F50, 0058C080, 0058E2F0, 00591040 e 005914A0. Os caminhos de banimento em 0058C080 distinguem 1157 por classificação FLIP e 1158 pela classe armadilha.
- O lote 04 cruzou IDs em rotinas globais decompiladas, especialmente 0056B7D0, 0059CE90/0059CF30 e 0059D560–0059D790. As entradas que usam instruções registram o endereço da rotina ou o endereço exato do teste de ID. O salto de P248 foi conferido nas tabelas 005903F4/005903E4.
- A campanha reaproveita os estudos consolidados de [destruição por máscara](../../cards/monster-destruction.md), [custos de descarte](../../cards/tribute-discard-cost.md), [P025](../../cards/p025-preliminary.md), [rituais](../../cards/ritual-bindings.md) e [dano fixo P045](../../cards/p045-direct-damage.md). As cartas já cobertas por esses estudos ficam fora da contagem de novas investigações.

## Reuso e validação

O lote 01 sustenta emissores genéricos de ganho/perda de LP, contagem de zona, destruição por predicado e destruição de alvo com revalidação. O resolvedor das respostas distingue cancelar um pedido de destruir sua origem e de substituir a operação por outra máscara.

As observações com maior valor no executável são a legalidade de respostas concorrentes, a reação de Michizure a diferentes causas de saída do campo e o comportamento de Eternal Rest com vínculos de tipos 4/5. O próximo passo estático para cada um já está indicado no catálogo. Para implementação, a primeira fatia recomendada é destruição em massa por tipo, com face-down, tipos dinâmicos e os dois lados como casos de teste.
