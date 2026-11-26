# Cobertura jogável — YugiRE

Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.

- **Trilogia:** 471/771 = 61.09%
- **Joey:** 163/350 = 46.57%
- **Partial:** 9; **Verified:** 0 (trilogia).
- **Partial no Joey:** 7.
A badge mostra Done+Verified e, separadamente, o número de Partial. Migração de regras já Done para a fila (C12) não altera a cobertura jogável.

## Por categoria

| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trilogia | Monster | 499 | 382 | 7 | 110 | 0 | 76.55% |
| Trilogia | Spell | 172 | 81 | 1 | 90 | 0 | 47.09% |
| Trilogia | Trap | 100 | 8 | 1 | 91 | 0 | 8.00% |
| Joey | Monster | 194 | 131 | 5 | 58 | 0 | 67.53% |
| Joey | Spell | 84 | 30 | 1 | 53 | 0 | 35.71% |
| Joey | Trap | 72 | 2 | 1 | 69 | 0 | 2.78% |

## Subtipos de monstros (classificação, não implementação)

| Subtipo | Trilogia | Joey |
| --- | ---: | ---: |
| Normal | 264 | 74 |
| Effect | 193 | 109 |
| Fusion | 34 | 9 |
| Ritual | 8 | 2 |
| ToReview | 0 | 0 |

## Como interpretar

- Os 771 recursos da união dos packs e os 350 do Joey vêm do DAT original, mas o índice versionado tem somente IDs, flags e categorias, nunca textos ou arquivos de jogo.
- Monster Normal/Effect/Fusion/Ritual são referências externas por nome exato ou normalizado; ToReview permanece explicitamente desconhecido.
- O status Done vem exclusivamente do manifest C# com testes de cenário; classificação não promove carta a Done.
- A infraestrutura genérica de invocação não significa que cada monstro esteja completo.
- Verified exige comparação do mesmo efeito com o jogo original, não só um teste C#.

### Fontes externas para subtipos

- https://github.com/omar39/yugioh-dataanalysis/blob/main/card_data.csv
- https://github.com/mahinb/yugioh-card-data-analysis/blob/main/card_data_small.csv
- https://github.com/maxDepa/YGOllama/tree/main/DataSet/Cards
- https://www.db.yugioh-card.com/yugiohdb/
- https://www.yugioh.com/cards
- https://ftp.yugipedia.com/wiki/
