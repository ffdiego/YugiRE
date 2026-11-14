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

O relatório Markdown agregado fica para a próxima tarefa. Não versionar saídas reais
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
bruto**, inclusive seus Unknown, não o registry anotado. Não há relatório
Markdown agregado nem ranking nesta entrega. Use diretórios distintos se
precisar manter gerações de conjuntos de anotações diferentes; o comando não
remove arquivos de execuções anteriores.

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
