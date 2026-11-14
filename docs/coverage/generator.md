# Gerador de inventário v1

Requer .NET 10 SDK, Python 3 e Git com o commit de fontes
`7e5ca132f7185bd2c3df4c9782e080fb95a135f5` disponível. Não consulta rede,
executa o jogo. O inventário bruto não importa anotações semânticas. O baseline fixa o Core auditado;
o snapshot não atribui implementação ao baseline. Nenhuma mudança no Core.

Na raiz do worktree:

```bash
dotnet restore src/Joey.Coverage
dotnet build src/Joey.Coverage --no-restore
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-coverage
python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -v
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -v
```

Restore não tem pacotes externos; após o build, o gerador usa `--no-build`.
Recompile após qualquer alteração da ponte C#. Fontes Core diferentes do commit
pinado são rejeitadas. Para outro baseline/fingerprint, revisar fontes e atualizar
o pin explicitamente. O EXE é aberto apenas para SHA256, comparado ao catálogo
estático pinado. Os hashes de ID/nomes/propriedades também precisam coincidir.

Saídas UTF-8/LF, sem BOM, caminhos de entrada ou horário:

- `inventory.json`: snapshot normativo conforme schema v1; todas as linhas não
  sentinela de `Catalog.Load`, callbacks completos inclusive órfãos, aliases
  numéricos e do helper, ledger imutável. Quatro eixos explicitamente Unknown,
  inventários Open, zero mecânicas/vínculos. Busca negativa na tabela significa
  somente AbsentInTable.
- `attributes.json`: formato auxiliar separado, pois v1 proíbe atributos extras.
  `cardRef` liga propriedades brutas/derivadas e uint16 card_pack às identidades
  do snapshot. Inclui SHA256 de card_pack, fingerprints, refs ao ledger e resumo.
  Bits 1/2/4 registram disponibilidade Yugi/Kaiba/Joey; bits restantes são
  preservados no uint16. O resumo mantém denominadores separados.
- `inventory.csv`: projeção com colunas fixas, nomes escapados pelo módulo csv,
  disponibilidade explícita true/false, quatro estados Unknown. Ponteiros null
  explícitos permanecem no JSON normativo, não são projetados no CSV.

Os relatórios Markdown são opcionais; veja entrega 4 abaixo. Não versionar saídas reais
em massa; somente os golden sintéticos entram no repositório. As descrições
consumidas por Catalog.Load nunca saem do processo C#.

A biblioteca padrão Python `jsonschema` é apropriada como verificação adicional
Draft 2020-12. `--standard-schema` usa Draft202012Validator quando previamente
instalado; sem o pacote a opção falha, sem instalar ou buscar nada. Não foi
possível exercitar essa opção neste ambiente (pacote ausente). A validação padrão
reutiliza o verificador restrito da primeira entrega, agora em
`scripts/coverage/validation.py`: rejeita keywords desconhecidas e verifica
referências, denominadores e gates de proveniência. Não é um validador genérico.
Nenhuma biblioteca estrutural substitui a revisão científica das evidências.

Os testes golden comparam bytes e SHA256, incluindo nome vazio, acento, vírgula,
aspas e newline. Testes sintéticos preservam sentinelas, índices descontínuos,
ID zero, duplicatas e variantes +2000; rejeitam entradas inválidas e referências
soltas. O teste local opcional gera duas vezes e compara todos os bytes.

## Anotações semânticas revisadas (entrega 3)

O inventário bruto continua independente. Para gerar também o registry anotado:

```bash
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-annotated --annotations docs/coverage/card-coverage.annotations.json
```

`--annotations` pode se repetir. Os três arquivos brutos mantêm exatamente os
mesmos bytes; são acrescentados `registry.json` (schema v1 completo) e
`registry.csv` (mesmas colunas do CSV bruto, estados explicitamente anotados).
O resumo de `attributes.json` e stdout continua descrevendo **o inventário
bruto**, inclusive seus Unknown, não o registry anotado. Relatórios Markdown e fila de investigações são opções da entrega 4, abaixo.
Use diretórios distintos para manter gerações de conjuntos de anotações
diferentes; a execução atual retira saídas conhecidas que deixaram de ser pedidas.

### Formato separado

`card-coverage.annotations.json` é a anotação piloto versionável. Campos
obrigatórios e exclusivos:

- `annotationVersion`: inteiro 1 (independente de schemaVersion do registry).
- `baselineCommit` e `fingerprints`: cópias exatas do inventário selecionado.
- `cards`: objetos com `cardRef`, `identity`, `coverage`, `mechanicInventory`,
  `mechanics`, `openQuestions`. A identidade completa deve coincidir com o
  inventário; o nome é apenas uma verificação adicional. A chave estável é
  fingerprints + internalIndex, com resourceId preservado. Não se pode alterar
  callbacks, atributos ou identidades por meio das anotações.
- `mechanics`: definições v1 completas, com ID estável, domínio, escopo,
  cobertura própria, dependências por ID e perguntas abertas.
- `evidence`: entradas v1 completas, com tipo, afirmação, limitações e
  localizador; commit completo, caminho relativo e SHA256 do blob Git.
  Observação nativa preserva `capture`, inclusive campos desconhecidos.

O importador valida campos e versões estritamente e reutiliza as definições
v1 para cartas, vínculos, mecânicas e evidências. Não há patches implícitos de
eixos: cada carta anotada informa os quatro eixos completos. Carta ausente
fica intacta. Cobertura de mecânica e vínculo nunca é herdada.

Definições repetidas **idênticas** em arquivos diferentes são aceitas; mesmo
ID com qualquer diferença causa erro, independentemente da ordem dos arquivos.
Duplicatas no mesmo arquivo são erro. Revisões concorrentes exigem resolução
manual, inclusive se uma parecer mais favorável. Não existe last-writer-wins,
maior estado ou promoção por Pxxx. A ordenação canônica alcança entidades,
vínculos, dependências e conjuntos de refs; mantém ordem de limitações e
perguntas textuais. Não reordena eventos históricos.

O merge verifica todos os blobs do ledger localmente via Git, sem fetch.
Commit ausente ou hash incorreto bloqueia a geração; não vira Unknown silencioso.
Um clone raso precisa receber previamente os commits pinados por seu operador.
Os gates do contrato rejeitam referências inválidas, ciclos, implementação sem
fonte no baseline, Candidate validado e recibos de validação incompletos.
Verificar hashes e estrutura não comprova a veracidade de uma revisão: os sete
critérios científicos do contrato continuam obrigatórios.

### Adicionar carta ou mecânica sem alterar código

1. Gere o inventário e copie o baseline, fingerprints, cardRef e identity da
   linha exata. Não use nome, resourceId ou Pxxx como chave substituta.
2. Crie um documento com o formato acima (arrays podem estar vazios). Registre
   evidências com `git show <commit>:<caminho>` e SHA256 dos bytes do blob;
   escreva localizador e limites. Não use SHA de documento como SHA de captura.
3. Acrescente mecânicas com IDs explícitos e escopo delimitado. Liste as
   dependências conhecidas como DAG; arrays vazios não atestam completude.
4. Adicione vínculos carta–mecânica com Candidate ou Established, evidências,
   callbacks pertinentes e os quatro eixos próprios. Mantenha Unknown em
   tudo que não foi demonstrado; inventário Open enquanto houver questões.
5. Execute a CLI com o novo arquivo e os anteriores. Confira o diff do JSON,
   as fontes no baseline e os testes. Resolva conflitos no documento, nunca
   pela ordem de argumentos. Uma revisão de baseline exige nova anotação
   explícita; o importador não migra evidências.

### Revisão conservadora do piloto

O piloto reutiliza os cinco exemplos históricos sem editá-los. Para adequar
as afirmações de implementação ao baseline do inventário (`7e5ca13`), foram
comparados os blobs de Archive.cs, NativeAiAttributes.cs e Demo.cs com o
baseline original `e0ae044`: são byte a byte idênticos. Somente as novas
entradas SourceInspection dessas fontes registram o commit reconferido.
Observações, testes históricos e fontes experimentais conservam commits,
hashes e limites originais. A fonte experimental `research-policy` não sustenta
nenhuma promoção de implementação na main; `research-v3` permanece relato
histórico condicional, sem nova execução.

A dependência explícita `rules.equip.sword -> ai.stats.sword-delta` representa
a contribuição aritmética separada necessária ao ciclo do equipamento no
escopo modelado. Não significa que a fórmula resolva vínculos, elegibilidade
ou supressão. A fórmula continua isolada, sem depender do ciclo completo.
Nenhuma dependência desconhecida é inventada para as outras quatro cartas;
todos os inventários continuam Open.

| Carta | Mecânicas/vínculos | Cobertura da carta (identificação / compreensão / implementação / validação) | Evidências principais | Lacunas |
|---|---|---|---|---|
| Axe Raider | ai.stats.closed-plain: UnderstoodInScope / ImplementedInScope / ConditionalMatch | Identified / Partial / Partial / Unknown | attributes, attribute-tests, axe-buffer | Manifesto independente e inventário completo |
| Sparks | rules.lp.sparks: Partial / Partial / SyntheticOnly | Identified / Partial / Partial / Unknown | sparks-static, demo, demo-tests | Timing, bloqueio e interações |
| Witch of the Black Forest | rules.search.witch-candidate: Unknown / Unknown / ObservedOnly, Candidate | Identified / Unknown / Unknown / Unknown | witch-observation | Causalidade, gatilho, IDs e deck; compra de turno é confundidor |
| Zombyra the Dark | rules.intrinsic.zombyra-candidate: Unknown nos quatro eixos, Candidate | Identified / Unknown / Unknown / Unknown | identities; pergunta de pesquisa sem regra afirmada | Regras por ID fora da tabela |
| Legendary Sword | ai.stats.sword-delta: UnderstoodInScope / ImplementedInScope / SyntheticOnly; rules.equip.sword: Partial / Partial / SyntheticOnly | Identified / Partial / Partial / Unknown | attributes, sword-static, cleanup-static, demo, testes | Elegibilidade, supressão, revalidação e timing |

Os três estados listados nos vínculos são compreensão/implementação/validação;
identificação é Identified salvo a pergunta de Zombyra. Os IDs das evidências
resolvem para commit, caminho, localizador e hash no próprio JSON de anotações.
Todas as cinco cartas têm validação integral Unknown.

## Relatórios e priorização (entrega 4)

Geração integrada, com ou sem `--annotations`:

```bash
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-reports --annotations docs/coverage/card-coverage.annotations.json --coverage-report --investigation-priorities
```

A partir dos JSONs já gerados, sem DAT/EXE ou build .NET:

```bash
python3 scripts/report_card_coverage.py --registry /tmp/yugire-reports/registry.json --attributes /tmp/yugire-reports/attributes.json --output /tmp/yugire-reports --coverage-report --investigation-priorities
```

Sem anotações, passe `inventory.json` como `--registry`. As duas opções são
independentes; a CLI de relatórios exige pelo menos uma. Os nomes são fixos:
`coverage-report.md` e `investigation-priorities.md`. Todas as entradas são
validadas antes da publicação. A CLI separada confere os blobs de evidência
por Git, sem rede, como o importador. Os renderizadores puros validam forma,
referências e consistência das entradas; não dependem de Git para testes
sintéticos. Nenhuma classificação é escrita de volta nos JSONs.

A geração integrada administra os sete nomes conhecidos (cinco JSON/CSV e dois
Markdown): após gerar e validar todos os bytes, publica por substituição de
arquivo temporário e retira nomes conhecidos que não pertencem à execução atual.
Assim, desligar anotações retira registry.json/registry.csv e desligar relatórios
retira seus Markdown antigos. A CLI separada administra somente os dois nomes
Markdown, preservando JSON/CSV. Arquivos com outros nomes não são removidos.
A publicação é atômica por arquivo, não uma transação do diretório; uma falha de
I/O durante a publicação deve ser resolvida repetindo o comando antes de usar o
conjunto de saídas. Falha na validação não publica resultados novos.

### Fontes por seção

| Seção | Campos usados |
|---|---|
| Baseline, seleção e fingerprints | registry.baselineCommit, catalogScope, fingerprints; attributes.packSha256 |
| Universos e disponibilidade | registry.cards.identity; attributes.cards.cardRef, pack e availability; registry.callbacks |
| Histogramas dos quatro eixos | registry.cards.coverage; nunca attributes.summary.coverage |
| Inventários e callbacks | registry.cards.mechanicInventory e callbackLookup |
| Mecânicas por domínio/eixo/estado | registry.mechanics.domain e coverage |
| Vínculos (denominador separado) | registry.cards.mechanics[].coverage |
| Cartas, classificações e lacunas | identity, coverage por eixo, mechanics por vínculo, openQuestions e limitations |
| Definições e dependências | registry.mechanics, dependencies, openQuestions, coverage |
| Evidências de cada classificação | evidenceRefs dos eixos/vínculos e ledger: kind, claim, location (commit/path/locator/SHA256), limitations e capture |
| Candidatos estruturais | callback.profileId e cinco slots; cards.callbackLookup.callbackRefs; identidades distintas por internalIndex |
| Disponibilidade dos candidatos | junção exata por cardRef com attributes.cards.availability e pack & 7 |
| Contexto e lacunas dos candidatos | refs explícitas de cartas/vínculos/mecânicas; mecânicas registradas; estados de compreensão/implementação; perguntas abertas |

`attributes.summary` tem denominadores conferidos contra as linhas originais,
mas seus histogramas brutos não alimentam a cobertura anotada. Fingerprints,
versão, refs e conjuntos de cartas devem coincidir. Duplicatas de atributos,
decodificação inconsistente, disponibilidade incompatível com pack, denominador
incorreto, busca contraditória e profile com assinaturas diferentes são erros.

### Política conservadora da fila

Cada candidato `profile:Pxxx` representa somente igualdade dos cinco slots
na versão pinada. Todos os profiles, inclusive órfãos e all-null, são listados.
Cartas anotadas sem callback entram como candidatos individuais (Axe Raider e
Zombyra no piloto). Cartas sem callback e sem mecânicas registradas são um pool
de auditoria contado à parte, sem fabricar centenas de hipóteses.

Não há score composto nem ganho garantido. A ordenação é lexicográfica:

1. Profiles com pelo menos um ponteiro não nulo conhecido primeiro.
2. Quantidade de cartas distintas por índice e registros de callback, em ordem
   decrescente; somente para profiles com ponteiros conhecidos.
3. Alcance na união dos packs e quantidade de versões representadas.
4. Quantidade de refs semânticas explícitas e mecânicas registradas, como
   desempates de contexto disponível, sem avaliação automática da qualidade.
5. Cartas a esclarecer: questões abertas ou compreensão/implementação Unknown,
   Partial, Conflicting ou NotImplemented. É demanda de investigação, não
   prova de comportamento ausente. Usa-se apenas o número de linhas que
   satisfazem esse predicado explícito, sem pesos para estados diferentes.
6. ID ordinal como desempate final.

Potencial estrutural é o alcance observado da triagem, não probabilidade de
reutilização de regras. Para all-null ou carta sem callback, ele é Unknown.
Ganho futuro comprovável, esforço/custo e adequação científica das evidências
ficam Unknown: nenhum desses valores pode ser deduzido do tamanho do grupo.
Os estados existentes aparecem separadamente; não são promovidos por ranking.
A fila favorece grupos amplos por uma política explícita e pode deixar questões
estreitas importantes abaixo deles. Uma decisão humana pode escolher outra
ordem sem alterar a cobertura.

### Verificação dos relatórios

```bash
python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
```

Golden Markdown bruto/anotado é exclusivamente sintético. Testes comparam bytes
sob permutação de entidades/refs e preservação dos inputs; verificam Unknown,
aliases/duplicatas, packs sobrepostos, ausência de propagação semântica, entradas
inconsistentes e retirada de arquivos residuais conhecidos. A integração opcional
real compara todas as saídas em duas gerações e entre CLI integrada e CLI de
JSONs existentes; depois desliga as opções e confere bytes brutos e limpeza.

## Fase 2: baseline explícito do piloto P045

O padrão da CLI permanece 7e5ca13 para reproduzir a Fase 1. A opção
`--baseline-commit` aceita somente um commit completo existente localmente;
Archive.cs e NativeAiAttributes.cs nesse commit precisam ser idênticos às
fontes pinadas da extração. Não troca fingerprints, tabela de callbacks ou
fontes de extração. Não migra anotações nem altera evidências automaticamente.

O piloto novo tem anotações separadas para as cinco cartas P045, avaliadas em
`0a9b4434dd7d185b5c44c20680b11258f7f6e164` (implementação e testes C#):

```bash
dotnet build src/Joey.Coverage --no-restore
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/p045-registry --baseline-commit 0a9b4434dd7d185b5c44c20680b11258f7f6e164 --annotations docs/coverage/p045.annotations.json --coverage-report --investigation-priorities
```

Não combinar esse documento com o piloto da Fase 1, que continua atestado no
baseline anterior. No novo snapshot, as cartas fora de P045 permanecem Unknown
porque não foram reavaliadas neste baseline; isso não afirma remoção de código
ou regressão das cartas históricas. As anotações antigas permanecem reproduzíveis
com o comando anterior, sem a opção de baseline. A futura revisão conjunta
exige reavaliar explicitamente suas fontes, sem trocar commits por conveniência.

P045 tem três mecânicas estreitas, separadas: projeção do comando LP, preparação
escalar do oponente e integração no demo. A integração depende das duas primeiras;
não herda sua cobertura. Quinze vínculos explícitos sustentam cinco cartas com
compreensão/implementação Partial e validação integral Unknown. As duas funções
escalares são ImplementedInScope/SyntheticOnly, não paridade integral. O ganho
verificável no demo é suporte a três IDs novos: Hinotama, Final Flame e Raimei;
Sparks e Ookazi conservam os valores anteriores.
