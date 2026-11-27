# Cobertura jogável — YugiRE

Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.

- **Trilogia:** 468/771 = 60.70%
- **Joey:** 159/350 = 45.43%
- **Partial:** 35; **Verified:** 0 (trilogia).
- **Partial no Joey:** 21.
A badge mostra Done+Verified e, separadamente, o número de Partial. C12 pode reduzir temporariamente a cobertura; o manifesto deve refletir suporte real.

## Por categoria

| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Trilogia | Monster | 499 | 376 | 16 | 107 | 0 | 75.35% |
| Trilogia | Spell | 172 | 84 | 14 | 74 | 0 | 48.84% |
| Trilogia | Trap | 100 | 8 | 5 | 87 | 0 | 8.00% |
| Joey | Monster | 194 | 124 | 14 | 56 | 0 | 63.92% |
| Joey | Spell | 84 | 33 | 3 | 48 | 0 | 39.29% |
| Joey | Trap | 72 | 2 | 4 | 66 | 0 | 2.78% |

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
- O status Done vem do manifesto de implementação jogável; classificação ou testes históricos não comprovam paridade.
- A infraestrutura genérica de invocação não significa que cada monstro esteja completo.
- Verified exige comparação do mesmo efeito com o jogo original, não só um teste C#.

### Fontes externas para subtipos

- https://github.com/omar39/yugioh-dataanalysis/blob/main/card_data.csv
- https://github.com/mahinb/yugioh-card-data-analysis/blob/main/card_data_small.csv
- https://github.com/maxDepa/YGOllama/tree/main/DataSet/Cards
- https://www.db.yugioh-card.com/yugiohdb/
- https://www.yugioh.com/cards
- https://ftp.yugipedia.com/wiki/
