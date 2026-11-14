# Card Coverage Registry — relatório da integração

Snapshot revisado de implementação/testes: **12f560fc1eb270aa0ed28b8cab9982524ea2c5bc**,
com [p025.annotations.json](p025.annotations.json). A integração de documentação
não muda esse baseline, o schema v1, os estados ou as evidências pinadas.
O [histórico de consolidação](consolidation.md) e os overlays anteriores são preservados.

## Progresso por carta e por mecânica

| Medida | Quantidade |
| --- | ---: |
| Cartas catalogadas, excluída somente a sentinela | 1.114 |
| Cartas com anotações revisadas | 11 |
| Implementação de carta inteira Partial | 9 |
| Cartas dos pilotos P045/P025 com efeitos específicos disponíveis no demo | 8 |
| Cartas integralmente validadas | 0 |
| Mecânicas registradas (inclui candidatas de pesquisa) | 9 |
| Vínculos carta–mecânica | 27 |

As nove cartas Partial são os cinco P045, os três P025 e Axe Raider. Witch of the
Black Forest e Zombyra the Dark completam as 11 anotações, com implementação
Unknown. Axe Raider tem escopo fechado de atributos de IA sob contexto resolvido;
não é uma nona magia/equipamento. Infinite Cards conserva um comportamento legado
no demo, fora do conjunto P045/P025 e das anotações quantificadas neste snapshot.

| Cartas | Escopo disponível no demo |
| --- | --- |
| Sparks, Hinotama, Final Flame, Ookazi, Raimei | Dano fixo de 200, 500, 600, 800 e 300, respectivamente; ativação da mão ou de magia setada |
| Legendary Sword, Beast Fangs, Sword of Dark Destruction | Elegibilidade explícita, ativação da mão, vínculo e contribuição ATK/DEF; limpeza comum de equipamentos |

Os inventários de **todas as 1.114 cartas** permanecem Open e a validação integral
Unknown. Compreensão/implementação: nove Partial e 1.105 Unknown; identificação:
11 Identified e 1.103 Unknown. Catálogo não equivale a jogabilidade: a união dos
packs tem 771 registros e o filtro Joey, 350. Versões se sobrepõem, não se somam.

Mecânica é um comportamento com escopo próprio, compartilhável entre cartas.
Uma contribuição aritmética ImplementedInScope/SyntheticOnly não fecha o ciclo
nativo de ativação/vínculo/retirada, que continua Partial. Relação de callback não
é equivalência semântica. Teste sintético, contexto declarado e observação nativa
são evidências diferentes; nenhum total acima demonstra paridade com o Joey original.

O piloto diferencial [Sparks/Sword](../research/sparks-sword-differential-pilot.md)
compara campos em dossiês pinados e retorna Match/Mismatch/Inconclusive. Suas
fixtures Synthetic testam infraestrutura; o template nativo vazio é Inconclusive.
Não há promoção automática. A captura nativa recente de Sparks fica para entrega
separada: nenhum dado ou resultado dessa captura foi incorporado nesta integração.

<a id="reproduzir-o-snapshot-e-os-relatorios"></a>

## Reproduzir o snapshot e os relatórios

Da raiz, com DAT/EXE locais legítimos, .NET10 e Python3:

```bash
dotnet build YugiRE.sln --no-restore -m:1
dotnet build src/Joey.Coverage --no-restore
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/integration-registry --baseline-commit 12f560fc1eb270aa0ed28b8cab9982524ea2c5bc --annotations docs/coverage/p025.annotations.json --coverage-report --investigation-priorities
python3 scripts/report_card_coverage.py --registry /tmp/integration-registry/registry.json --attributes /tmp/integration-registry/attributes.json --output /tmp/integration-reports-only --coverage-report --investigation-priorities
```

No Windows, trocar `/tmp/integration-registry` e `/tmp/integration-reports-only`
por pastas sob `scratch/`, e ajustar os caminhos DAT/EXE. O gerador é read-only
para os arquivos do jogo; nenhum comando acima executa o EXE. Não combinar overlays
de baselines diferentes. `p025.annotations.json` já contém a consolidação revisada.

Saídas completas: registry.json/csv, inventory.json/csv, attributes.json,
**coverage-report.md** e **investigation-priorities.md** no diretório de saída.
A projeção independente pelo segundo comando produziu os mesmos bytes dos dois
relatórios. Os testes de integração também repetem a geração e comparam todos
os bytes e a preservação do inventário bruto.

| Relatório gerado no baseline acima | SHA256 nesta verificação |
| --- | --- |
| coverage-report.md | e21e01fb2a088c12d191c3031f398d375417307d14cfd87f7554ac6b6b37b12c |
| investigation-priorities.md | 43e79fc5665b04f1716fa859dc685b90367620051e6cd78968156635de51dc13 |

Esses relatórios completos são gerados localmente; este resumo versionado não
duplica os 1.114 registros. [Formato, validações e comandos do gerador](generator.md),
[revisão do baseline P025](p025-implementation.md) e
[contrato do registry](../research/card-coverage-registry-contract.md).

<a id="prioridades-de-investigacao"></a>

## Prioridades de investigação

Recorte inicial do `investigation-priorities.md` gerado, **ordem estrutural**:

| Posição | Perfil | Cartas / registros de callback | Ganho futuro comprovado |
| --- | --- | ---: | --- |
| 1 | P025 | 41 / 41 | Unknown |
| 2 | P007 | 23 / 23 | Unknown |
| 3 | P269 | 16 / 16 | Unknown |
| 4 | P036 | 13 / 13 | Unknown |
| 5 | P092 | 8 / 8 | Unknown |

O relatório completo contém todos os candidatos, evidências e perguntas.
Compartilhar cinco ponteiros indica alcance estrutural, não comportamento igual,
custo baixo ou permissão para implementar o grupo. P025 mantém somente os três
IDs autorizados; os outros 38 não foram promovidos. Este recorte não inicia nem
aprova outra investigação. Política de ordenação e limites:
[relatórios e fila estrutural](generator.md#relatórios-e-priorização-entrega-4).

<a id="validacao-da-integracao"></a>

## Validação da integração

Reexecutado a partir de 727081c, com alterações apenas de documentação:

```bash
dotnet build YugiRE.sln --no-restore -m:1
dotnet build src/Joey.Coverage --no-restore
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
dotnet run --project src/tests/Joey.Tests --no-build
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_*coverage*.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_*registry.py' -q
python3 -m unittest discover -s tests -p 'test_p025_inventory.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
dotnet run --project src/Joey.App --no-build -- ../passion/data.dat --check-data
dotnet run --project src/Joey.App --no-build -- ../passion/data.dat --check-graphics
git diff --check
```

Resultados: builds sem avisos/erros; **1.051 checks .NET com DAT/deck, 853 offline**;
**58 testes Python PASS**, sem SKIP com os artefatos configurados (35 cobertura,
14 registry, um inventário P025, oito callbacks). Incluem regressões Core/Lab de
P045, P025 e comparação por campo. Dados/imagens: 3.568 entradas DAT, 1.114 registros,
771 na Card List, 793 BMPs decodificados sem janela.

A validação estrita própria do registry passou, incluindo proveniência dos blobs,
determinismo e ausência de promoções. A opção adicional `--standard-schema` foi
tentada, mas não executou a validação jsonschema porque esse pacote Python não
está instalado. A geração foi repetida sem essa opção e passou; não atribuímos
PASS à tentativa opcional nem alteramos dependências/schema para contorná-la.

SHA256 dos originais lidos: EXE
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
DAT `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
Arquivos originais não foram alterados nem versionados.

**Smoke test gráfico pendente.** DISPLAY/WAYLAND_DISPLAY não estão definidos;
as conexões aos sockets X11 não estão utilizáveis pela sessão. Diagnóstico externo
retornou `Authorization required, but no authorization protocol specified` e foi
encerrado. Joey.App normal terminou com código2 e mensagem de ausência de sessão
gráfica. `--check-data` e `--check-graphics` passaram, mas não validam abertura ou
interação da janela. [Roteiro visual pendente](../../src/README.md#validação).
O Joey original, hooks e capturas não foram executados.

## Revisão do escopo do PR

`origin/main=e0ae044` foi atualizada via fetch e é ancestral da branch de trabalho
(merge-base idêntico; 25 commits de trabalho até727081c, zero commits de merge).
A main local265f132 também é ancestral, mas está atrasada; a revisão usa a referência
remota atualizada. GitHub confirmou ahead25/behind0 antes do commit documental.
Não houve integração de outra branch experimental.

Foram revisados os 63 arquivos do diff acumulado contra origin/main: implementação
Core/UI/Lab, ponte read-only, gerador/validador/relatórios, fixtures/testes, schema,
overlays e evidências/handoffs. Mudanças funcionais permanecem nos escopos aprovados.
Esta preparação acrescenta somente README principal, src/README e documentação
de integração. Baseline/overlays/schema e evidências anteriores conferidos byte
a byte contra727081c, sem alterações.

Auditoria do conjunto do PR: nenhum EXE, DLL, DAT, save/deck/replay original, ZIP,
trace bruto privado ou arquivo binário novo; JSONs parseáveis e fixtures mínimas
identificadas. As referências a hashes/nomes de capturas em documentos históricos
não incluem seus brutos. Saídas reais/logs desta validação ficam em /tmp/integration-*
e não são incluídos no commit. Revisão das alterações staged repetida antes do envio.
A captura recente de Sparks não faz parte deste PR.

Os testes e o gerador resolvem evidências por `git show <commit>:<path>`.
A integração futura precisa manter os commits pinados alcançáveis no histórico
clonado. Preferir merge que preserve os commits desta branch; squash/rebase sem
preservar essas referências exige revisão de proveniência e pode impedir a
reprodução em clones novos. Nenhuma estratégia de merge foi aplicada nesta tarefa.
