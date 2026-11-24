# Handoff — consolidação revisada do registry

Branch `investigate/p025-registry`, criada em c93b52a; P045 preservada.
Unidade de commit: `docs: consolidate reviewed card coverage registry` (localizar
por `git log --oneline -- docs/coverage/consolidated.annotations.json`).

Entregas: overlay consolidated.annotations.json, revisão consolidation.md,
cinco testes específicos. Baseline c93b52a revisado contra arquivos C# e testes;
fontes e overlays históricos pinados por SHA256. Nove identidades, oito mecânicas,
vinte vínculos. Sparks aparece uma vez; rules.lp.sparks substituído por escopos
P045, avaliação antiga preservada como evidência. Sete cartas Partial em
implementação; Witch/Zombyra Unknown; validação integral 1114 Unknown.
Sem alteração de schema ou de código da engine.

Executado nesta sessão:

```bash
sha256sum ../passion/joey_pc.exe
dotnet build YugiRE.sln --no-restore -m:1
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_*coverage*.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p045_registry.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_consolidated_registry.py' -q
```

Resultados: SHA esperado; build zero avisos/erros; 776 checks .NET;
35 testes de cobertura, cinco P045 e cinco consolidação PASS (sem SKIP com
artefatos configurados). Geração real em /tmp/p025-consolidated-registry;
reprodução exata em docs/coverage/consolidation.md. Teste de integração gera
duas vezes e confere bytes, inventário bruto e cartas não promovidas.
Log .NET: /tmp/p025-dotnet-tests.log. Testes de hooks/Python legado não executados:
sem alterações nesses componentes. Nenhum hook, captura ou execução nativa.

Limites: testes de registry provam consistência/proveniência; não paridade.
Deltas preexistentes de outras cartas não são autorização nem implementação de
seu ciclo de vida. Próximo passo autorizado: investigação documental P025,
em commit separado. Parar antes de implementar cartas.
