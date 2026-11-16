# Handoff — relatórios e fila conservadora, entrega 4

Branch feature/card-coverage-registry; partida 59af160. Commits:
8f35924 (relatórios, CLI e ciclo de saídas), 369e634 (goldens, testes e documentação).

Implementação em scripts/coverage/reports.py; publicação de arquivos conhecidos
em scripts/coverage/output.py; CLI de JSONs existentes em
scripts/report_card_coverage.py. CLI integrada mantém JSON/CSV e aceita
--coverage-report e --investigation-priorities. Documentação e campos de origem:
docs/coverage/generator.md. Contrato/schema v1, baseline, fingerprints, fontes
históricas, anotações, Core, hooks, DLL e arquivos originais preservados.

Cobertura usa registry.cards.coverage, não histogramas brutos de attributes.
Atributos fornecem disponibilidade; denominadores e junção por cardRef são
conferidos. Markdown inclui cartas, vínculos, mecânicas, dependências, perguntas,
limitações e evidências por eixo com commit, caminho, localizador e hash.

Fila: profiles exatos dos cinco slots, inclusive all-null e órfãos; mais cartas
anotadas sem callback. 271 candidatos no alvo. Ordenação lexicográfica por
ponteiro conhecido, alcance de cartas/linhas de callback, alcance dos packs,
versões, refs explícitas/mecânicas registradas e cartas a esclarecer; ID desempata.
Adequação científica, esforço/custo e ganho futuro comprovável: Unknown.
Nenhum grupo vira família semântica e nenhuma carta recebe promoção.

Publicação valida tudo antes de escrever; substituição por arquivo temporário.
A geração integrada retira nomes conhecidos não pedidos nesta execução; CLI
separada retira apenas Markdown não pedido. Outros arquivos são preservados.
Não há transação atômica do diretório inteiro: repetir após erro de I/O antes
de usar saídas. Falhas de validação deixam a geração anterior intacta.

Comandos executados:

```bash
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-reports --annotations docs/coverage/card-coverage.annotations.json --coverage-report --investigation-priorities
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
git diff --check
```

Resultados finais: 35 testes de cobertura com DAT/EXE reais passaram; mais 8
testes de callbacks passaram. Na execução offline final, 33 testes passaram
e 2 opcionais foram ignorados. Integração real inclui duas gerações, CLI
separada a partir de JSONs, comparação byte a byte das sete saídas e geração
sem opções para confirmar preservação bruta e retirada de resíduos.
Goldens Markdown são sintéticos com e sem anotações. Testes cobrem permutação,
ordem estável, inconsistências, packs sobrepostos, Unknown, duplicatas de
recursos e ausência de propagação semântica entre cartas com mesmos ponteiros.
Não houve build novo (nenhuma mudança .NET); a ponte existente foi executada
em leitura pelos testes. Não executados jogo, investigação RE, capturas/hook,
validação diferencial ou biblioteca jsonschema opcional.

Resumo real: 1114 registros completos e recursos distintos; união dos jogos
771; Yugi/Kaiba/Joey 155/315/350. 443 callbacks. 1114 inventários Open.
Identificação 5 Identified/1109 Unknown; compreensão e implementação
3 Partial/1111 Unknown; validação 1114 Unknown. Seis mecânicas (Rules 4,
AiStats 2, AiDecision 0), seis vínculos. JSON/CSV brutos idênticos aos anteriores.

Dez primeiros candidatos (callback rows / cartas / união packs; Yugi/Kaiba/Joey):

| Profile | Callbacks | Cartas | União | Y/K/J |
|---|---:|---:|---:|---|
| P025 | 41 | 41 | 37 | 16/6/16 |
| P007 | 23 | 23 | 23 | 0/5/19 |
| P269 | 16 | 16 | 8 | 0/6/2 |
| P036 | 13 | 13 | 13 | 6/7/6 |
| P092 | 8 | 8 | 2 | 0/2/0 |
| P164 | 6 | 6 | 6 | 0/6/6 |
| P041 | 5 | 5 | 5 | 3/1/1 |
| P127 | 5 | 5 | 5 | 1/2/2 |
| P123 | 5 | 5 | 5 | 2/3/0 |
| P045 | 5 | 5 | 4 | 3/0/1 |

Saídas reais em /tmp/yugire-reports, não versionadas. Quantidade de cartas
não garante reutilização nem ganho de implementação. Grupos amplos dominam
a fila por política declarada; escolhas humanas podem priorizar questões
estreitas. Não há percentual geral ou evidência nova de paridade.

Ponto de parada: relatórios e priorização concluídos. Nenhuma investigação
ou expansão de anotações foi iniciada; próximo trabalho exige nova tarefa.
