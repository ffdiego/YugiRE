# Cobertura jogável — YugiRE

Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.

- **Trilogia:** 369/771 = 47.86%
- **Joey:** 110/350 = 31.43%
- **Partial:** 8; **Verified:** 0 (trilogia).

## Por categoria

| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trilogia | Monster | 499 | 333 | 3 | 163 | 0 | 66.73% |
| Trilogia | Spell | 172 | 31 | 5 | 136 | 0 | 18.02% |
| Trilogia | Trap | 100 | 5 | 0 | 95 | 0 | 5.00% |
| Joey | Monster | 194 | 98 | 2 | 94 | 0 | 50.52% |
| Joey | Spell | 84 | 10 | 2 | 72 | 0 | 11.90% |
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
