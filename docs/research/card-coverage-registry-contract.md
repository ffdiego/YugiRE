# Card Coverage Registry — contrato v1, primeira entrega

Status: proposta executável de formato e critérios; nenhum gerador foi implementado.
Esta fase inventaria conhecimento e cobertura, sem ampliar `Joey.Core`.

## Baseline e fontes

Branch de trabalho: `feature/card-coverage-registry`, criada da `main` em
`e0ae044bbd9994c33c66630e4a74f8c95ef15831`. O head remoto foi confirmado pelo
conector GitHub; o fetch SSH local falhou por configuração de permissões em
`/etc/ssh/ssh_config.d/20-systemd-ssh-proxy.conf`. O objeto já existia no clone.
O checkout original `yugire` e seus arquivos não rastreados foram preservados;
a entrega está no worktree `YugiRE-card-coverage`.

Fontes inspecionadas:

- [Archive.cs](../../src/Joey.Core/Archive.cs): `Card` e `Catalog.Load`.
- [NativeAiAttributes.cs](../../src/Joey.Core/NativeAiAttributes.cs): política
  fechada, contexto resolvido, contribuições e aliases de recursos.
- [Catálogo de callbacks](../17-card-callback-catalog.md), seu
  [JSON](../evidence/card-callback-catalog.json) e
  [script](../../scripts/catalog_card_callbacks.py). Não executar esse gerador
  para validar o novo contrato: ele pode reescrever evidências existentes.
- [Contrato de observação](ai-combat-observation-contract.md),
  [testes .NET](../../src/tests/Joey.Tests/Program.cs),
  [testes de atributos](../../src/tests/Joey.Tests/AiAttributeChecks.cs),
  [Sparks](../cards/sparks-ookazi.md),
  [Legendary Sword](../cards/legendary-sword.md) e
  [observações Witch/Spear](../31-joeytrace-duel-evidence.md).
- Evidência adicional de `research/esi-crash`, lida via `git show` no commit
  `a7de8c016af3d4c9c857b4be8c8bb6a6b1af9bfb`:
  [handoff v3](https://github.com/ffdiego/YugiRE/blob/a7de8c016af3d4c9c857b4be8c8bb6a6b1af9bfb/docs/research/handoffs/native-v3-attribute-triage.md)
  e [política de atributos](https://github.com/ffdiego/YugiRE/blob/a7de8c016af3d4c9c857b4be8c8bb6a6b1af9bfb/src/Joey.Core/NativeAiAttributes.cs).
  A branch contém expansão de suporte que **não integra este baseline**. Seu
  relato de dois matches Gemini Elf é condicional; há zero contextos
  integralmente independentes nesse relatório. Não houve merge/cherry-pick.

Fingerprints SHA256, em hexadecimal minúsculo:

| Artefato | SHA256 |
| --- | --- |
| EXE | `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de` |
| DAT | `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed` |
| CARD_ID | `a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c` |
| CARD_Nameeng | `74848d0b2cd7d5fac7a767075bd5b01e0366183ce934afa6070ce24394a24032` |
| CARD_Prop | `1a772642a04d86a2cfcebba2d0f4ff40a1737761296d0ea6a2645f5ed2bf0b9e` |

EXE, DAT e os três recursos foram conferidos localmente em leitura, sem
executar o jogo. Para os recursos, somente o decoder LZSS existente foi
reutilizado em memória, sem importar os efeitos de escrita do script. As cinco
identidades coincidem com as fontes versionadas. Não se exportaram payloads,
descrições completas, executáveis, DATs, decks ou capturas privadas.

## Arquitetura e unidade de cobertura

O [schema JSON](../coverage/card-coverage.schema.json) define um documento v1
com `$defs.card` (Card Coverage), `$defs.mechanic` (Mechanic Coverage),
`$defs.binding` (cobertura específica da associação), callbacks, aliases e
um ledger de evidências. Os [cinco exemplos](../coverage/card-coverage.examples.json)
são um subconjunto manual, marcado `kind=Example`, e não um inventário completo.
O formato é independente de Python, Windows e de tipos de runtime do Core.

```text
snapshot (baselineCommit + fingerprints + catalogScope)
  card (uma linha original de Catalog.Load)
    callbackLookup -> callback (registro estático com cinco slots)
    mechanics[] -> binding -> mechanic (regra/questão com escopo e dependências)
    coverage -> quatro avaliações separadas -> evidenceRefs
  mechanic -> coverage / dependencies
  binding -> coverage própria / evidências / callbacks pertinentes
  alias -> duas cards preservadas, relação limitada a um escopo
  evidence -> commit + caminho + locator + SHA256 + tipo + limites
```

`baselineCommit` fixa a implementação avaliada. Todo estado de implementação
conhecido exige inspeção de fonte **nesse commit**. Um método existente em
`research/esi-crash` não conta como implementado na `main`.

Uma mecânica tem ID estável e domínio `Rules`, `AiStats` ou `AiDecision`.
Por exemplo, aplicar dano, selecionar um alvo, criar um vínculo e calcular uma
contribuição de atributos são unidades distintas. Não registrar “P025” como
mecânica sem investigação. O vocabulário inicial não pretende enumerar todas
as regras: questões ainda abertas podem receber IDs de candidatos.

O nó de mecânica descreve sua cobertura no escopo declarado. Cada associação
carta–mecânica tem cobertura própria; não herda automaticamente os estados do
nó nem dos outros membros de uma família. A associação `Candidate` registra
hipótese, enquanto `Established` exige evidência da ligação. Dependências
formam um DAG, podem incluir mecanismos globais e devem ser explicitadas antes
de fechar o inventário da carta. Listas vazias significam “nenhuma entrada
registrada neste levantamento”; não demonstram ausência de regras.

## Identidade, aliases e callbacks

A chave de carta dentro de um snapshot é `card:<internalIndex>`. A identidade
entre snapshots é `(fingerprints, internalIndex)`; o `resourceId` continua
campo separado e deve concordar com o mapeamento de recursos daquela versão.
O nome serve para apresentação, nunca como chave. A posição na lista retornada
por `Catalog.Load` não substitui `Card.Index`, pois a carga ignora sentinelas.

- Índice interno é inteiro não negativo, sem impor o limite de ocupação de uma
  API de combate ao catálogo inteiro. ID é uint16, com `65535` excluído de
  cards porque é sentinela em `Catalog.Load`. ID zero e nome vazio são válidos.
- Várias linhas com o mesmo ID permanecem separadas. Variantes `+2000` e nomes
  iguais não são fundidos. As cinco cartas escolhidas não exigem arestas alias;
  os testes incluem duplicatas e variantes sintéticas.
- `SameResourceId`, `Plus2000Variant` e `NativeHelperAlias` preservam ambas as
  pontas, escopo e evidência. Os aliases de `EquivalentResources` pertencem à
  comparação daquele helper; não provam equivalência de efeitos. O registro
  nunca executa `NormalizeResource` sobre a chave, índice ou ID original.
- Slot em campo, ocorrência, índice interno, ID de recurso, índice do registro
  de callback e VA são conceitos diferentes. Slots/ocorrências só aparecem
  nos localizadores de observação, não na identidade de catálogo.
- Callback é identificado por `callback:<row>` **neste fingerprint de EXE e
  tabela**. `row` é zero-based; `rowVa` é VA preferida, oito dígitos hex. Para
  esta tabela: VA `0x005ed0a8`, offset de arquivo `0x001ed0a8`, stride 24,
  443 registros, base de imagem `0x00400000`. Não confundir VA com RVA/offset.
- Preservar `profileId` Pxxx do catálogo e os cinco slots nomeados. O schema
  distingue `Unknown` (busca não comprovada), `Present` e `AbsentInTable`
  (busca negativa no inventário fechado, com evidência). Um slot `null` em
  registro conhecido representa ponteiro zero observado; não é callback
  desconhecido. Para observação desconhecida usa-se `callbackLookup=Unknown`.
- Callback sem correspondência em CARD_ID continua no inventário de callbacks
  sem criar uma carta fictícia. Ausência de callback não prova ausência de
  efeitos; igualdade até dos cinco slots não prova equivalência semântica.
  Os Pxxx são identificadores locais da versão do catálogo, não chaves globais.

## Quatro eixos, sem pontuação única

Cada avaliação exige `state`, `scope`, `evidenceRefs` e `limitations`.
Validação também exige `acceptance`, nulo enquanto não validada.
Todos os eixos começam em `Unknown`. O `default` do JSON Schema é uma anotação:
não preenche JSON ausente. Documentos publicados omitem nenhum eixo; campos
faltantes são erro estrutural, e um futuro importador deve materializar
`Unknown` explicitamente antes de emitir um documento.

| Eixo | Estados permitidos e significado |
| --- | --- |
| Identificação | `Unknown`; `Identified` = identidade de carta/mecanismo delimitada; `Conflicting` = fontes incompatíveis. Identificar uma pergunta de pesquisa não demonstra sua causalidade. |
| Compreensão | `Unknown`; `Partial` = alguns caminhos conhecidos; `UnderstoodInScope` = contrato fechado somente no escopo escrito; `Conflicting` = interpretações incompatíveis. |
| Implementação | `Unknown`; `NotImplemented` = ausência explicitamente auditada **na API/escopo**; `Partial`; `ImplementedInScope` = todos os requisitos daquele escopo presentes no baseline. |
| Validação | `Unknown`; `SyntheticOnly` = testes/modelos sem validação diferencial independente; `ObservedOnly` = observação sem comparação completa; `ConditionalMatch` = igualdade dependente de hipóteses ou inputs nativos; `Incomplete` = tentativa sem dados obrigatórios; `Failed` = divergência/falha documentada; `ValidatedInScope` = todos os critérios abaixo satisfeitos. |

Estados não formam uma escala numérica. Uma implementação pode existir antes
de entendermos o mecanismo nativo; observar um efeito não prova a implementação.
Uma mudança de fingerprint, baseline, contrato ou hipótese exige reavaliação,
não promoção automática. Contradições, perdas ou falhas não são descartadas em
favor de um match mais recente. `Unknown` não significa `NotImplemented`.
`Unsupported` de uma API é evidência de uma limitação dessa API; não é um
estado global da carta e não classifica o efeito como inexistente.

Nos exemplos, a avaliação da **carta inteira** mantém validação `Unknown`.
O conhecimento de um mecanismo estreito aparece no vínculo correspondente,
como `ConditionalMatch` para o buffer de atributos de Axe Raider. Isso evita
que um êxito parcial pareça validação integral.

## Evidências e proveniência

Cada evidência tem ID local único, tipo, afirmação delimitada, limitações,
localizador e referência imutável (`repository`, commit completo, caminho
relativo, SHA256 dos bytes do blob Git). `locator` indica seção, símbolo,
JSON pointer ou evento; número de linha isolado não basta. Um hash de documento
não é hash de captura. Refs soltas, IDs duplicados e ciclos são erros.

Tipos: `ResourceIdentity`, `StaticAnalysis`, `SourceInspection`, `SyntheticTest`,
`NativeObservation`, `Inference`, `DeclaredContext`, `Review`, `ValidationMatrix`.
Análise estática, relato de execução e teste sintético nunca são convertidos
entre si. Uma evidência sintética preexistente pode ser **inspecionada** nesta
entrega sem alegar que foi executada agora; isso está marcado nos exemplos.

Uma observação nativa exige objeto `capture`, inclusive quando seus hashes são
`null`. Ele preserva hash da captura, hash do EXE atestado, origem do atestado
(`Unknown`, `Header`, `External`), completude e eventos. Atestado externo deve
ser localizado na fonte referenciada. Hash do EXE no cabeçalho de um documento
ou fixture não preenche retroativamente o hash de uma captura. Dados ausentes
continuam desconhecidos. Evidência privada é referenciada por hash e resumo
versionável; o arquivo bruto não entra no registry.

O schema verifica forma e condições locais. Os testes também exercitam
referências, unicidade, DAG, baseline e gates de proveniência. Nenhum schema
pode julgar sozinho se uma inferência é correta ou uma matriz científica é
suficiente: a revisão com evidências continua obrigatória.

## Quando uma carta pode ser considerada validada

“Validada” sempre inclui fingerprint, baseline e escopo. Não equivale a prova
universal de toda interação possível, todas as versões ou paridade completa
com o jogo. `ValidatedInScope` de uma fórmula, callback ou buffer não valida a
carta inteira. Para esta última, todos os itens abaixo são obrigatórios:

1. Identidade inequívoca (`Identified`), sem normalização destrutiva, com hashes
   de EXE e recursos compatíveis e aliases preservados.
2. Inventário de mecanismos `Closed`, sem questões abertas. A revisão precisa
   justificar sua completude, inclusive regras por ID fora dos callbacks,
   gatilhos, custos, alvos, legalidade, execução, movimentos/limpeza, atributos
   e dependências globais aplicáveis. Itens considerados não aplicáveis
   precisam de justificativa na matriz; silêncio não os remove.
3. Compreensão `UnderstoodInScope` e implementação `ImplementedInScope`, no
   mesmo escopo que a validação, com fonte do baseline. Todos os vínculos são
   `Established` e individualmente `ValidatedInScope`; todas as dependências
   necessárias estão representadas e validadas para a carta. Não basta que o
   nó genérico de uma mecânica tenha testes para outra carta.
4. Matriz versionada, revisada, com IDs estáveis de casos e requisitos, cenário,
   pré-condições, entradas, oráculo, resultado esperado, tolerâncias (se
   houver), comandos, resultados e evidências. Cobrir caminho positivo,
   rejeições sem mutação, fronteiras, ordenação, interações e regressões.
   Descrever riscos residuais e o que o escopo exclui; não escolher casos
   olhando apenas os resultados que coincidiram.
5. Comparações nativas reproduzíveis com captura e EXE atestados, eventos
   identificados, sequência/correlação íntegras, sem perda/readError relevante.
   Todos os inputs necessários devem ser observados no instante de consumo ou
   derivados de fonte independente comprovada. Separar estado real, scratch
   hipotético, stats reais e estimativa do rival oculto. Contexto completo é
   requisito, não inferência a partir de flags/vínculos zero.
6. O C# calcula as saídas a partir dessas entradas independentes. É proibido
   completar o manifesto de efeitos ou ajustar inputs a partir dos outputs
   esperados. Estatísticas nativas, scores ou retornos de predicados usados
   como inputs só permitem validação condicional do trecho escalar.
7. Todos os casos exigidos passam; nenhum mismatch ou caso obrigatório
   incomplete/unsupported fica oculto. Regressões sintéticas e comparação
   nativa são ambas necessárias. Registro de revisão explícita atesta os
   critérios e o fechamento do inventário.

O recibo `acceptance` referencia matriz, observações nativas, testes e revisão;
exige `independentInputs`, `completeContext` e `allRequiredCasesPassed` iguais
a true. A estrutura rejeita recibo ausente, tipos de evidência incorretos,
hashes ausentes/incompatíveis, associação candidata e inventário aberto.
Marcar esses booleanos não constitui prova; a revisão deve conferir as fontes.
Um resultado condicional nunca muda de categoria pela contagem de matches.

## Exemplos conservadores

| Carta | Índice / ID | Callback | Cobertura que se pode registrar | Lacuna que bloqueia validação integral |
| --- | --- | --- | --- | --- |
| Axe Raider | 24 / 77 (`0x004d`) | `AbsentInTable` | `FromPlainCard` implementado no escopo fechado; buffer event24 coincide sob manifesto declarado. Vínculo `ConditionalMatch`. | Manifesto real independente e inventário completo das regras. Ausência da tabela não classifica a carta. |
| Sparks | 755 / 342 (`0x0156`) | row 66, P045, primary `0x00584e60` | Ramo estático solicita 200 ao oponente; implementação demo parcial e testes sintéticos. | Timing, flags de bloqueio e interações. Ookazi compartilha P045 e solicita 800. |
| Witch of the Black Forest | 261 / 573 (`0x023d`) | row 100, P006, primary `0x00583d20` | Identidade e registro conhecidos; candidato de busca com observação parcial de saída/mão. Compreensão e implementação da carta `Unknown`. | Causalidade, gatilho, IDs buscados e manipulação do deck. Compra de turno é confundidor. API de atributos fechada rejeita 573. |
| Zombyra the Dark | 515 / 1523 (`0x05f3`) | `AbsentInTable` | Identidade conhecida; política intrínseca/de ataque como questão de pesquisa, demais eixos `Unknown`. | Investigar regras fora da tabela. A rejeição de 1523 na API fechada não o torna vanilla nem prova ausência de implementação global. |
| Legendary Sword | 614 / 300 (`0x012c`) | row 24, P025, primary `0x00584500` | Seleção/vínculo/retirada parcialmente entendidos e implementados na demo; contribuição aritmética separada, implementada em `EquipmentDelta`. | Elegibilidade real, supressão, revalidação, mudanças de tipo e timing. Bônus isolado não fecha ciclo do equipamento. |

As únicas cinco linhas de identidade foram conferidas localmente. Para Witch
e Zombyra não se copiaram regras externas ou texto moderno das cartas como se
fossem comportamento comprovado do Joey.

## Relatórios determinísticos: contrato para a próxima entrega

Esta seção especifica o gerador futuro; não introduz CLI, extração ou relatório
computado de todo o catálogo nesta entrega.

1. Entrada explícita: snapshot com versão, baseline, fingerprints, seleção e
   ledger. Sem caminhos absolutos, hora atual, nomes de máquina, branch móvel,
   descoberta automática de capturas ou consulta de rede na transformação.
   Alterar fonte pinada exige nova revisão; não usar o conteúdo “mais recente”.
2. Validação estrutural **e** de referências antes de publicar. Falha não vira
   lista vazia, zero suporte ou sucesso. Campos/versões desconhecidos são
   rejeitados no contrato v1. Uma mudança incompatível exige nova versão.
3. Ordenação: cards por `internalIndex` numérico; callbacks por `row` numérico;
   mecanismos, aliases e evidências por ID ordinal; vínculos por `mechanicRef`;
   conjuntos de refs por ID ordinal. Preservar ordem dos passos/eventos em
   artefatos de evidência. Não ordenar nem modificar dados brutos capturados.
4. JSON canônico do projeto: UTF-8 sem BOM, LF, chaves ordenadas ordinalmente,
   indentação de dois espaços, newline final, `ensure_ascii=false`, sem NaN,
   floats de porcentagens ou timestamps. O serializer do gerador deverá
   manter a mesma política de escapes. Comparar bytes e SHA256 dos relatórios
   em testes golden, inclusive com entradas equivalentes em ordens diferentes.
5. JSON é a saída normativa; Markdown/CSV são projeções com colunas e ordem
   fixas. Células escapam separadores/newlines. Exibir `Unknown`/`null`
   explicitamente. Nenhuma média ou “percentual geral de cobertura”.
6. Denominadores explícitos: número de linhas de catálogo selecionadas, IDs de
   recurso distintos, registros da tabela e mecanismos conhecidos são métricas
   diferentes. `Catalog.Load` mantém 1114 linhas não sentinela neste alvo; não
   confundir isso com as 771 cartas da união de `card_pack`, os 443 registros
   de callback ou as cinco cartas do exemplo. Filtros exigem `catalogScope`.
   `expectedCardCount=null` significa denominador não atestado, nunca zero.
7. Contar estados separadamente em cada eixo e domínio; contar vínculos
   separadamente de cartas/mecanismos; não somar amostras sobrepostas. Publicar
   Unknown, Open, ConditionalMatch, Incomplete, Failed e ausências de fonte.
   Mecanismos descobertos não constituem denominador de “todas as regras”.
8. Resumo deve listar baseline/fingerprints, escopo/filtros, número de cartas,
   histograma de cada eixo, inventário aberto/fechado, callbacks presentes/
   ausentes/desconhecidos, gaps por carta/mecanismo e IDs de evidência. Contagem
   de ponteiros é metadado, nunca numerador de compreensão ou validação.

Expectativas golden para este exemplo, sem gerar relatório:

| Métrica | Valor |
| --- | ---: |
| Cartas / IDs distintos | 5 / 5 |
| Identificação Identified | 5 |
| Compreensão Partial / Unknown | 3 / 2 |
| Implementação Partial / Unknown | 3 / 2 |
| Validação de carta Unknown / ValidatedInScope | 5 / 0 |
| Inventário Open / Closed | 5 / 0 |
| Callback Present / AbsentInTable / Unknown | 3 / 2 / 0 |
| Mecanismos / vínculos | 6 / 6 |
| Validação de vínculo ConditionalMatch / SyntheticOnly / ObservedOnly / Unknown | 1 / 3 / 1 / 1 |

Os testes desta entrega verificam forma, gates, fontes e serialização fixa do
exemplo. Testes golden de **relatório**, permutação de entradas e contagens do
catálogo inteiro são critérios da entrega futura, ainda não executáveis.

## Testes e critérios de aceite desta entrega

Executar na raiz do worktree, sem arquivos do jogo ou pacotes adicionais:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_card_coverage_contract.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -v
```

Requer Python 3 e Git com o commit baseline disponível localmente. Os hashes
de fontes são conferidos nos blobs Git, preservando bytes mesmo com autocrlf
no Windows. Os testes não fazem fetch da branch de pesquisa: suas referências
externas são verificadas como metadados pinados; os blobs foram inspecionados
nesta entrega. Uma cópia sem histórico não substitui essa verificação.

O helper estrutural dos testes suporta somente o vocabulário JSON Schema usado
neste documento e falha se aparecer keyword não tratada. Não é um validador
Draft 2020-12 genérico nem um leitor de produção. Não foi possível instalar
`jsonschema` no ambiente isolado (pacote ausente, DNS indisponível); o schema
pode ser consumido por um validador padrão na futura integração. Testes de
refs e critérios semânticos são necessários mesmo com tal biblioteca.

Aceite desta primeira entrega:

- Schema v1 com quatro eixos obrigatórios, Unknown explícito e rejeição de
  campos inesperados, IDs inválidos, referências órfãs e promoção indevida.
- Cinco exemplos coerentes com as identidades e registros pinados; nenhuma
  carta integralmente validada; comparação condicional preservada.
- Tipos de evidência, aliases, endereço e baseline distintos, inclusive
  fontes de `research/esi-crash` imutáveis e sem transferência de suporte.
- Critérios de validação integral e relatório determinístico documentados,
  com denominadores e gaps visíveis.
- Testes offline positivos e negativos passam; mudanças limitadas a docs e
  testes de contrato. Core, hooks, DLLs, originais e capturas permanecem fora
  da entrega. Gerador e expansão de engine aguardam nova etapa.

Riscos: inventário de mecanismos ainda aberto, evidências históricas com
proveniência incompleta, descrições de escopo em linguagem natural exigindo
revisão, schema local incapaz de comprovar causalidade e revisão necessária ao
mudar baseline. A estabilidade de um ponteiro/ID não elimina esses riscos.
