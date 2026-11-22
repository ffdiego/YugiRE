# Cobertura jogável — YugiRE

Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.

- **Trilogia:** 337/771 = 43.71%
- **Joey:** 103/350 = 29.43%
- **Partial:** 4; **Verified:** 0 (trilogia).

## Por categoria

| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trilogia | Monster | 499 | 302 | 1 | 196 | 0 | 60.52% |
| Trilogia | Spell | 172 | 30 | 3 | 139 | 0 | 17.44% |
| Trilogia | Trap | 100 | 5 | 0 | 95 | 0 | 5.00% |
| Joey | Monster | 194 | 91 | 1 | 102 | 0 | 46.91% |
| Joey | Spell | 84 | 10 | 0 | 74 | 0 | 11.90% |
| Joey | Trap | 72 | 2 | 0 | 70 | 0 | 2.78% |

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
