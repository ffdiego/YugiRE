# Handoff — anotações semânticas, entrega 3

Branch feature/card-coverage-registry, partida em 1a19946.
Commits: 7b775c0 (importador estrito, CLI e piloto), af026e0 (testes e documentação).

Formato de anotações separado em docs/coverage/card-coverage.annotations.json;
importador em scripts/coverage/annotations.py. Nenhuma mudança no schema v1,
Core, regras, capturas, hooks, DLL, evidências históricas ou branches externas.

CLI e instruções para novas cartas/mecânicas: docs/coverage/generator.md.
--annotations é repetível: definições iguais coalescem, diferenças são conflito
independentemente da ordem. Baseline/fingerprints/identidade precisam coincidir;
fontes imutáveis são verificadas por Git e SHA256 sem rede. Não há promoção
por nome, resourceId, Pxxx ou mecânica compartilhada. Cartas não anotadas
permanecem intactas. Dados brutos não são alterados pelo merge.

Revisão do baseline: Archive.cs, NativeAiAttributes.cs e Demo.cs em e0ae044
foram comparados byte a byte com 7e5ca13 e coincidem. Somente as novas entradas
SourceInspection foram registradas no baseline reconferido. Capturas, testes
históricos e os dois registros experimentais mantêm os commits originais.
O helper da branch research não passa a representar implementação na main.

Piloto: 5 cartas, 6 mecânicas e 6 vínculos, todos os inventários Open.
Dependência modelada: rules.equip.sword -> ai.stats.sword-delta, sem herança
entre a cobertura do ciclo completo e da contribuição aritmética isolada.
Candidatos Witch/Zombyra permanecem Candidate, sem causalidade presumida.

Comandos executados:

```bash
dotnet build src/Joey.Coverage --no-restore
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-annotated --annotations docs/coverage/card-coverage.annotations.json
git diff --check
```

Resultados: build sem avisos/erros. Com DAT real: 27 testes passaram; sem
variáveis de ambiente: 26 passaram, 1 opcional ignorado. Mais 8 testes de
callbacks passaram. Golden SHA256 de todos os 5 artefatos do piloto sintético;
permutação dos registros extraídos, documentos, definições, vínculos e refs
compara bytes de JSON, attributes.json e ambos os CSV. Teste real compara
as duas gerações anotadas e preservação byte a byte dos três arquivos brutos.
Testes negativos: conflitos, duplicatas, refs, ciclos, versão, fingerprints,
baseline, identidade, hash incorreto, blob ausente, promoção indevida e fonte
experimental usada como implementação no baseline.

Contagens reais preservadas: 1114 cartas/recursos distintos; 443 callbacks,
1 órfão; packs 155/315/350, união 771. Registry anotado: identificação
5 Identified/1109 Unknown; compreensão e implementação 3 Partial/1111 Unknown;
validação 1114 Unknown. Seis mecânicas e seis vínculos. attributes.json e stdout
continuam descrevendo o inventário bruto, não um resumo de cobertura anotada.
Os dois JSONs reais e CSVs estão em /tmp, sem versionamento em massa.

Limites: hashes/estrutura não provam causalidade nem suficiência científica;
sete critérios do contrato permanecem necessários. Biblioteca jsonschema
opcional não testada (ausente); verificador restrito v1 e gates relacionais
executados. Não houve novas execuções dos testes históricos citados como
proveniência, nem validação Windows/diferencial. Sem testes gerais da engine
porque não foi alterada. Markdown agregado e ranking ficam para próxima tarefa.

Ponto de parada: importação e piloto concluídos. Não avançar automaticamente
para mais cartas ou regras. A tabela das cinco cartas, estados, evidências e
lacunas está em docs/coverage/generator.md e no JSON revisável do piloto.
