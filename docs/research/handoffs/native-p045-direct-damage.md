# Handoff — Fase 2, piloto P045

Branch nova feature/p045-direct-damage, partida 441dda8. A branch
feature/card-coverage-registry e research/esi-crash não foram modificadas.
Commits: 420bdb9 investigação; 0964723 implementação; 0a9b443 testes;
6d4a15a baseline explícito do registry; a812d63 anotações e testes do registry.

## Conclusões e escopo

EXE SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de
confirmado pelo REA open_binary e pelo verificador local. REA analyze_function
não tinha provider configurado; sessão encerrada sem persistir projeto.
Fallback: dumps existentes + GNU objdump e script read-only inspect_p045.py.
Sem hooks, captura, execução do EXE, alteração de DAT/EXE/save/DLL ou análise
de outra família. Fragmentos novos mínimos em p045-static-checks.json.

Ramos conferidos individualmente: Sparks 584f1b/200, Hinotama 584f42/500,
Final Flame 584f5e/600, Ookazi 584fa7/800, Raimei 585076/300. Todos chamam
5784e0 com byte[pedido+6]&1; guarda +4&4 evita emissão. Preparação 593cc0
calcula 1-(sourceByte&1) e acrescenta alvo ao pedido; contador inicial não é
assumido pela API escalar. Comando LP 43/8043 com amount, 0, 1. Apresentação
e outros efeitos do helper não são reproduzidos. AX=0 não prova retorno32=0.

DirectDamageRules implementa uma whitelist explícita de cinco IDs e plano
escalar de comando; desconhecidos/null retornam Unsupported. O bit4 conhecido
produz SkippedByRequestFlag, sem nomeá-lo negação. Demo usa flags0 e preparação
usual explicitamente como política. Quantidades antigas Sparks/Ookazi e
fixture inicial preservados; Hinotama, Final Flame e Raimei passam a funcionar
na mão e em ativação de set. Sem engine genérica ou ampliação a outros IDs.

Baseline atestado da implementação/testes: 0a9b4434dd7d185b5c44c20680b11258f7f6e164.
CLI --baseline-commit explicita essa seleção; continua pinando extração/helper
com verificação byte a byte de Archive.cs e NativeAiAttributes.cs. Padrão Fase 1
inalterado. p045.annotations.json é separado do piloto antigo e não pode ser
combinado com baseline diferente. Cartas históricas fora de P045 não foram
reavaliadas: Unknown no snapshot novo não afirma perda de implementação.

Ganho verificável: demo 2 -> 5 IDs de dano (três novos); API escalar contempla
cinco IDs e guarda conhecida. Registry P045: 1114 cartas, 443 callbacks,
771 na união packs, 5 cartas com compreensão/implementação Partial e 1109
Unknown; validação integral 1114 Unknown. Três mecânicas/15 vínculos:
comando e preparação escalares ImplementedInScope/SyntheticOnly; integração
Partial/SyntheticOnly depende de ambas. Nenhuma promoção por ponteiro/Pxxx.

## Verificação executada

```bash
dotnet restore YugiRE.sln --disable-parallel
dotnet build src/tests/Joey.Tests --no-restore -m:1
dotnet build YugiRE.sln --no-restore -m:1
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
dotnet run --project src/tests/Joey.Tests --no-build
dotnet build src/Joey.Coverage --no-restore
python3 scripts/inspect_p045.py --exe ../passion/joey_pc.exe
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p045_registry.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/p045-registry --baseline-commit 0a9b4434dd7d185b5c44c20680b11258f7f6e164 --annotations docs/coverage/p045.annotations.json --coverage-report --investigation-priorities
git diff --check
```

Resultados: builds de teste/solução/ponte sem avisos ou erros; 776 checks .NET
com DAT/deck e 578 offline. Incluem 94 checks P045 e 327680 combinações sintéticas
de flags/target (não execuções nativas). Python: 35 testes de cobertura,
5 P045 e 8 callbacks passaram. Metadata estática reproduzida e comparada byte
a byte com o JSON versionado. Integrações comparam gerações determinísticas,
preservação dos bytes brutos e falha de merge com baseline Fase 1.
Build inicial --no-restore falhou por assets ausentes; resolvido com restore.
Não se atribui PASS à tentativa inicial. Logs completos em /tmp/p045-*.log;
saídas reais em /tmp/p045-registry, não versionadas. Nenhum push realizado.

## Lacunas e ponto de parada

Sem validação diferencial independente. Captura futura precisaria atestar EXE
 e captura; observar ator, índice/recurso, contador, alvo preparado e flags
no instante de consumo; correlacionar comando/LP antes-depois/limpeza e ordem,
sem perda; testar ambos os lados, guarda e interações. Nome da flag, legalidade,
cadeias, redirecionamento/modificadores e apresentação permanecem desconhecidos.
Os sete critérios do contrato continuam necessários para ValidatedInScope.

Detalhes e tabela por carta: docs/cards/p045-direct-damage.md. Reprodução do
registry: docs/coverage/generator.md, seção Fase 2. Ponto de parada: piloto
P045 concluído; não iniciar outras famílias ou novas capturas automaticamente.
