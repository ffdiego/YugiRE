# Integração phase1 — Sol e Terra

As branches `origin/work/native-analysis` (5f8dd1d) e `origin/work/joey-lab` (cfd6852) foram integradas com merges preservando histórico, sem conflitos. Os contratos, hooks passivos, comparação offline e leitor de contextos coexistem; nenhuma equivalência dinâmica é afirmada.

Validação no checkout integrado: `dotnet restore YugiRE.sln -m:1` e `dotnet build YugiRE.sln --no-restore -m:1` passaram; `dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc` passou 669 verificações. `python3 -m unittest discover -s native_hooks/tests` passou 87 testes; `yugilab/tests`, 49; `scripts/tests`, 14 com um skip por captura privada ausente no caminho esperado. Os três testes Node `test_loader.cjs`, `test_deck_trace.cjs` e `test_demo_rng_seed.cjs` passaram. SHA256 do EXE local corresponde ao fixado no plano, sem execução.

O comando `Joey.Lab combat-contexts` leu a captura privada do piloto com SHA256 esperado `424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f`: 64 contextos `Incomplete`, zero problemas globais e zero eventos não correlacionados. A captura não atesta o EXE; flags no consumo e cálculo independente de atributos permanecem sem prova. Não há avaliação pelo Core nem caso completo observado. A captura original não foi alterada ou versionada.

Próximo passo: revisão do merge e, para avançar M4, evidência de flags no consumo, atributos independentes e atestado vinculado à captura, conforme os handoffs das trilhas. O piloto não comprova ausência de interferência nem resolve as divergências históricas.
