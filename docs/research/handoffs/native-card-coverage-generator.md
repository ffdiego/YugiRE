# Handoff — gerador Card Coverage Registry, segunda entrega

Branch existente feature/card-coverage-registry. Entrega limitada ao gerador;
contrato, schema v1 e evidências históricas preservados. Sem jogo, hooks, DLL,
merge da research ou alterações no Joey.Core.

Commits: `848ca6d` (ponte Catalog.Load, gerador e extração do validador restrito),
`7b24544` (testes, golden sintéticos e documentação). Este handoff é separado
do histórico da primeira entrega.

Comandos executados na raiz:

```bash
dotnet build src/Joey.Coverage
dotnet build src/Joey.Coverage --no-restore
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-coverage
git diff --check
```

Resultados: build sem avisos/erros; 21 testes de contrato/gerador, incluindo
7 do gerador e execução opcional real, mais 8 testes de callbacks passaram.
O teste real compara bytes de duas gerações. DAT e EXE foram apenas lidos;
o EXE teve SHA256 validado. Não executados testes gerais da engine/UI,
Windows ou capturas nativas: não são alterados por esta entrega.

Contagens locais: 1114 linhas e recursos distintos; 443 callbacks; 442 cartas
com registro, 672 ausentes nesta tabela; callback:318 (recurso 1245) órfão
preservado. União card_pack 771; Yugi 155, Kaiba 315, Joey 350. Os quatro
eixos têm 1114 Unknown cada; zero mecânicas conhecidas, nenhuma promoção.

Limites: v1 não admite atributos/disponibilidade; preservados em JSON auxiliar
com referências e CSV, sem alterar schema. Catálogo selecionado completo é o
retorno de Catalog.Load, sem filtro de packs. Anotações manuais não são
importadas. Comparações de aliases do helper têm escopo limitado, não efeitos.
JSON Schema padrão é opção offline (--standard-schema); jsonschema ausente,
portanto essa opção não foi exercitada. Verificador restrito e gates relacionais
foram executados. Markdown adiado. Saídas reais somente em /tmp, não commitadas.

Documentação: docs/coverage/generator.md. Próximo passo requer nova tarefa:
relatório Markdown ou revisão de anotações; não avançar automaticamente para
classificação semântica ou regras. Ponto de parada: gerador concluído.
