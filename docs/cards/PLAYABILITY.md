# Cobertura jogável — YugiRE

Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.

- **Trilogia:** 307/771 = 39.82%
- **Joey:** 96/350 = 27.43%
- **Partial:** 3; **Verified:** 0 (trilogia).

## Por categoria

| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trilogia | Monster | 499 | 284 | 0 | 215 | 0 | 56.91% |
| Trilogia | Spell | 172 | 23 | 3 | 146 | 0 | 13.37% |
| Trilogia | Trap | 100 | 0 | 0 | 100 | 0 | 0.00% |
| Joey | Monster | 194 | 87 | 0 | 107 | 0 | 44.85% |
| Joey | Spell | 84 | 9 | 0 | 75 | 0 | 10.71% |
| Joey | Trap | 72 | 0 | 0 | 72 | 0 | 0.00% |

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
