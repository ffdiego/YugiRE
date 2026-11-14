# Card Coverage Registry — próximas investigações

Baseline: `7e5ca132f7185bd2c3df4c9782e080fb95a135f5`.

Fila estrutural, não ranking de implementação. Cada profile agrupa exatamente os cinco ponteiros; não representa uma família semântica.
Potencial de reutilização: alcance estático observado. Ganho futuro comprovável: Unknown para todos; nenhuma investigação foi executada.

Ordenação lexicográfica: ponteiro não nulo conhecido primeiro; cartas distintas (linhas), registros de callback, união dos packs, número de versões, quantidade de evidências semânticas registradas, mecânicas registradas, cartas a esclarecer e ID ordinal.
Para candidatos sem ponteiro executável compartilhado, alcance estrutural é Unknown e não participa dos dois critérios de tamanho.
Evidências e mecânicas são apenas desempates de contexto; quantidade não mede qualidade. Adequação científica das evidências e esforço/custo: Unknown. Lacunas são exibidas por estado, sem pesos inventados.
Estados Unknown não são ausência de efeito/implementação. Profiles órfãos e nulos ficam visíveis. Cartas sem callback e sem anotação continuam como pool de auditoria fora desta fila.

Pool fora da fila (sem callback presente e sem mecânicas): 2 cartas.

Cartas a esclarecer: perguntas abertas ou compreensão/implementação Unknown, Partial, Conflicting ou NotImplemented. É demanda de investigação, não prova de defeito; usada somente como último desempate numérico.

## Candidatos

| Posição | Candidato | Registros callback | Cartas distintas / recursos | Potencial estrutural | União packs | Yugi / Kaiba / Joey | Evidências semânticas | Mecânicas | Compreensão (estado:contagem) | Implementação (estado:contagem) | Cartas a esclarecer | Ganho comprovável |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | profile:P001 | 2 | 2 / 1 | 2 | 2 | 1 / 1 / 1 | extraction | test.question | Unknown:2 | Unknown:2 | 2 | Unknown |

## Escopo e perguntas por candidato

### profile:P001

Cartas: card:2, card:5

Callbacks: callback:0, callback:1; evidências estruturais: callbacks

Perguntas: Effect inventory unaudited, including rules outside callback table.; Which preconditions?

## Fontes dos critérios

- callbacks: SourceInspection; [tests/test_card_coverage_generator.py](https://github.com/ffdiego/YugiRE/blob/7e5ca132f7185bd2c3df4c9782e080fb95a135f5/tests/test_card_coverage_generator.py); SHA256 `dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd`; inputs(). Limitações: Not native evidence

- extraction: SourceInspection; [tests/test_card_coverage_generator.py](https://github.com/ffdiego/YugiRE/blob/7e5ca132f7185bd2c3df4c9782e080fb95a135f5/tests/test_card_coverage_generator.py); SHA256 `dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd`; inputs(). Limitações: Not native evidence

## Limites

Tamanho de grupo não garante reaproveitamento de regras nem ganho de cobertura. A disponibilidade conta linhas do catálogo, sem fundir duplicatas ou somar versões sobrepostas.
As referências semânticas resolvem no ledger do registry e do coverage-report.md. A proveniência estrutural vem dos evidenceRefs dos callbacks; nenhuma afirmação semântica é derivada desses refs.
