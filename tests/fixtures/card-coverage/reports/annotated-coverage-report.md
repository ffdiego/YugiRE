# Card Coverage Registry — cobertura

Baseline: `7e5ca132f7185bd2c3df4c9782e080fb95a135f5`.

Escopo: AllNonSentinelRows; All rows returned by Catalog.Load; no card_pack filter.

Estados vêm do registry selecionado. Atributos fornecem somente disponibilidade e denominadores.
Não há percentual geral; domínios, eixos, cartas e vínculos são universos separados.

## Universos

| Universo | Registros |
| --- | --- |
| Catálogo selecionado completo | 4 |
| Recursos distintos no catálogo | 3 |
| União dos três jogos (pack &amp; 7) | 2 |
| Fora da união dos jogos | 2 |
| Yugi | 1 |
| Kaiba | 1 |
| Joey | 1 |
| Tabela de callbacks (inclui órfãos) | 2 |

As versões se sobrepõem; não somar suas contagens. A união dos packs não substitui o catálogo completo.

## Fingerprints

| Fonte | SHA256 |
| --- | --- |
| cardIdSha256 | aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa |
| datSha256 | aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa |
| exeSha256 | cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc |
| namesSha256 | aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa |
| propertiesSha256 | aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa |
| card_pack | bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb |

## Histogramas de cartas

| Eixo | Estado | Cartas |
| --- | --- | --- |
| identification | Unknown | 3 |
| identification | Identified | 1 |
| identification | Conflicting | 0 |
| understanding | Unknown | 4 |
| understanding | Partial | 0 |
| understanding | UnderstoodInScope | 0 |
| understanding | Conflicting | 0 |
| implementation | Unknown | 4 |
| implementation | NotImplemented | 0 |
| implementation | Partial | 0 |
| implementation | ImplementedInScope | 0 |
| validation | Unknown | 4 |
| validation | SyntheticOnly | 0 |
| validation | ObservedOnly | 0 |
| validation | ConditionalMatch | 0 |
| validation | Incomplete | 0 |
| validation | Failed | 0 |
| validation | ValidatedInScope | 0 |

## Inventários e buscas de callbacks

| Inventário | Cartas |
| --- | --- |
| Open | 4 |
| Closed | 0 |
| Unknown | 0 |

| Busca de callback | Cartas |
| --- | --- |
| Present | 2 |
| AbsentInTable | 2 |
| Unknown | 0 |

## Mecânicas por domínio e estado

Mecânicas registradas não são denominador de todas as regras do jogo.

| Domínio | Mecânicas |
| --- | --- |
| Rules | 1 |
| AiStats | 0 |
| AiDecision | 0 |

| Domínio | Eixo | Estado | Mecânicas |
| --- | --- | --- | --- |
| Rules | identification | Identified | 1 |
| Rules | understanding | Unknown | 1 |
| Rules | implementation | Unknown | 1 |
| Rules | validation | Unknown | 1 |

## Vínculos (contados separadamente)

| Eixo | Estado | Vínculos |
| --- | --- | --- |
| identification | Identified | 1 |
| understanding | Unknown | 1 |
| implementation | Unknown | 1 |
| validation | Unknown | 1 |

## Cartas, associações e lacunas

Ordem dos estados: identificação / compreensão / implementação / validação.

### card:0 — 

Recurso: 0; inventário: Open; packs: nenhum.

Callbacks: AbsentInTable (nenhum); evidências: callbacks.

| Eixo | Estado | Escopo | Evidências | Limitações |
| --- | --- | --- | --- | --- |
| identification | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| understanding | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| implementation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| validation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |

| Mecânica | Relação | Quatro eixos | Evidências do vínculo e eixos | Escopos e limitações |
| --- | --- | --- | --- | --- |

Perguntas abertas: Effect inventory unaudited, including rules outside callback table.

### card:2 — A, "é"<br>B

Recurso: 77; inventário: Open; packs: Yugi, Kaiba.

Callbacks: Present (callback:0); evidências: callbacks.

| Eixo | Estado | Escopo | Evidências | Limitações |
| --- | --- | --- | --- | --- |
| identification | Identified | Whole card; semantics unaudited | extraction | Automatic extraction does not establish coverage. |
| understanding | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| implementation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| validation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |

| Mecânica | Relação | Quatro eixos | Evidências do vínculo e eixos | Escopos e limitações |
| --- | --- | --- | --- | --- |
| test.question | Candidate | Identified / Unknown / Unknown / Unknown | extraction | identification: Whole card; semantics unaudited — Automatic extraction does not establish coverage.; evidências: extraction; understanding: Whole card; semantics unaudited — Automatic extraction does not establish coverage.; evidências: Unknown; implementation: Whole card; semantics unaudited — Automatic extraction does not establish coverage.; evidências: Unknown; validation: Whole card; semantics unaudited — Automatic extraction does not establish coverage.; evidências: Unknown |

Perguntas abertas: Effect inventory unaudited, including rules outside callback table.

### card:5 — Duplicate

Recurso: 77; inventário: Open; packs: Joey.

Callbacks: Present (callback:0); evidências: callbacks.

| Eixo | Estado | Escopo | Evidências | Limitações |
| --- | --- | --- | --- | --- |
| identification | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| understanding | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| implementation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| validation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |

| Mecânica | Relação | Quatro eixos | Evidências do vínculo e eixos | Escopos e limitações |
| --- | --- | --- | --- | --- |

Perguntas abertas: Effect inventory unaudited, including rules outside callback table.

### card:9 — Variant

Recurso: 2077; inventário: Open; packs: nenhum.

Callbacks: AbsentInTable (nenhum); evidências: callbacks.

| Eixo | Estado | Escopo | Evidências | Limitações |
| --- | --- | --- | --- | --- |
| identification | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| understanding | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| implementation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| validation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |

| Mecânica | Relação | Quatro eixos | Evidências do vínculo e eixos | Escopos e limitações |
| --- | --- | --- | --- | --- |

Perguntas abertas: Effect inventory unaudited, including rules outside callback table.

## Definições de mecânicas

### test.question

Synthetic question &#124; &lt;tag&gt; — Synthetic only

Dependências: nenhuma registrada

Perguntas abertas: Which preconditions?

| Eixo | Estado | Escopo | Evidências | Limitações |
| --- | --- | --- | --- | --- |
| identification | Identified | Whole card; semantics unaudited | extraction | Automatic extraction does not establish coverage. |
| understanding | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| implementation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |
| validation | Unknown | Whole card; semantics unaudited | Unknown | Automatic extraction does not establish coverage. |

## Ledger de evidências

### callbacks (SourceInspection)

Fonte: [tests/test_card_coverage_generator.py](https://github.com/ffdiego/YugiRE/blob/7e5ca132f7185bd2c3df4c9782e080fb95a135f5/tests/test_card_coverage_generator.py); commit `7e5ca132f7185bd2c3df4c9782e080fb95a135f5`; SHA256 `dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd`.

Localizador: inputs()

Synthetic fixture only

Limitações: Not native evidence

### extraction (SourceInspection)

Fonte: [tests/test_card_coverage_generator.py](https://github.com/ffdiego/YugiRE/blob/7e5ca132f7185bd2c3df4c9782e080fb95a135f5/tests/test_card_coverage_generator.py); commit `7e5ca132f7185bd2c3df4c9782e080fb95a135f5`; SHA256 `dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd`.

Localizador: inputs()

Synthetic fixture only

Limitações: Not native evidence

### pack (SourceInspection)

Fonte: [tests/test_card_coverage_generator.py](https://github.com/ffdiego/YugiRE/blob/7e5ca132f7185bd2c3df4c9782e080fb95a135f5/tests/test_card_coverage_generator.py); commit `7e5ca132f7185bd2c3df4c9782e080fb95a135f5`; SHA256 `dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd`.

Localizador: inputs()

Synthetic fixture only

Limitações: Not native evidence
