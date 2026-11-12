# Handoff T — comparação offline, 2026-11-11

Objetivo: iniciar T1 com comparação offline de capturas seedadas sem atribuir vencedor.

Implementação: commit `fe4828a` em `work/joey-lab`. `Joey.Lab compare LEFT RIGHT` aceita JSONL, diretórios e ZIP; valida ranges, configurações, sequência, seed mix32-v1 aplicada e footer; compara embaralhamentos, eventos de IA/executor, estrutura de comandos, valores raw e low16, estados CE/terminal, wrapper 86, pacotes de três opcodes e reseeds. Preserva diferenças raw e classifica os callsites de resíduos de registrador do Python. `--strict-raw` e `--strict-unverified` alteram o código de saída. O relatório não identifica vencedores. As duas fixtures são sintéticas, geradas pelo produtor de fixtures de `yugilab/tests/test_compare.py`, não são capturas nativas.

Validação executada:
- `dotnet restore YugiRE.sln -m:1` (NuGet acessível após aprovação); `dotnet build YugiRE.sln --no-restore -m:1`: passou, sem avisos.
- `dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc`: 631 verificações passaram, incluindo fixture sintética e footer incompleto.
- `python3 -m unittest yugilab.tests.test_compare yugilab.tests.test_command_words`: 17 testes passaram.
- Comparação CLI de ZIPs sintéticos gerados pelo teste Python: dois duelos observados iguais, dois argumentos raw diferentes, ambos classificados como resíduo de ponteiro; consistente com Python nesse cenário.
- SHA256 local de `../passion/joey_pc.exe`: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Não foi executado.

Limitações: T1 ainda não tem paridade completa demonstrada. O JSON do relatório é diferente do Python e não inclui fingerprints, agregações por comando, limite de exemplos ou `--output`; contagem de diferenças não verificadas é por argumento, não por comando. A suíte C# cobre apenas parte dos cenários Python; não foi comparado um par de capturas reais seedadas completas. Uma igualdade de checkpoints não prova igualdade integral de estado. Nada foi validado no Windows. T2/T3 aguardam contrato S1 publicado e captura de contexto; T4 depende da conclusão e verificação de T1/T2. Não houve alteração em arquivos da trilha S ou Joey.Core.

Próximo passo: fornecer/identificar um par de execuções seedadas completas e portáveis para confronto contra `python3 -m yugilab.compare LEFT RIGHT`; completar testes de paridade, formato/contagens e opções faltantes de T1 antes de considerá-lo concluído. Em paralelo, integrar o contrato S1 somente após publicação e revisão; não inventar offsets ou posições.

## Retomada T1: capturas solicitadas ausentes

Foram procurados `parallel-mjj1z5ia.zip` e `parallel-a1xf_v2y.zip` na árvore `/home/diego` e em `/tmp` (incluindo arquivos ocultos no projeto). Nenhum dos dois apareceu nas áreas acessíveis; algumas pastas privadas de serviços em `/tmp` negaram leitura. A evidência `docs/evidence/joey-rng-seed-reproducibility-two-runs-2026-11-08.md` documenta os resultados históricos, mas não contém os ZIPs necessários para reexecutar a comparação. Nenhuma captura foi alterada, nenhum teste sintético foi usado como substituto e nenhuma comparação C#/Python foi executada nesta retomada.

Dependência: disponibilizar os dois ZIPs originais em um caminho local acessível. Depois, calcular e registrar seus SHA256 sem modificar os arquivos e executar `python3 -m yugilab.compare LEFT RIGHT --output /tmp/joey-python-comparison.json` e `dotnet run --project src/Joey.Lab -- compare LEFT RIGHT`, confrontando checkpoints por índice global e diferenças raw. Preservar os relatórios somente em local apropriado para dados de captura; não versionar traces brutos. O documento histórico relata 51 e 68 comandos com diferenças de palavras altas; esses números são evidência anterior, não resultado desta retomada.
