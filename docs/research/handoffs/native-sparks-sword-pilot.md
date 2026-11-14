# Handoff — piloto mínimo Sparks / Legendary Sword (aguarda captura Windows)

Branch `feature/sparks-sword-differential-pilot`, partida2523483. Branches e
anotações P045/P025 anteriores preservadas. Nenhum push, execução do Joey,
attach, injeção, hook, captura, alteração de EXE/DAT/save/DLL nesta sessão.

Contrato/matriz de campos: ../sparks-sword-differential-pilot.md.
Template: ../sparks-sword-native.template.json (nenhuma observação nativa).
Comparador: Joey.Lab effect-pilot, chama os mecanismos C# existentes; não gera
estado de cobertura. Fontes pinadas por SHA256 e seletor linha/pointer; hipóteses
Assumed nunca produzem Match. Dossiê exige auditoria humana da correlação,
proveniência e ponto de amostragem; o leitor não autentica semântica de extratos.

## Windows — preparação e coleta externa

Requer Python3.10+ e .NET10 já disponíveis. Transferir os commits desta branch
para a máquina Windows; trabalhar da raiz do repositório. Os comandos abaixo
são instruções para o operador, **não foram executados pelo agente**.
Primeiro repetir testes offline e conferir fingerprint, sem iniciar instrumentação:

```powershell
git switch feature/sparks-sword-differential-pilot
dotnet build YugiRE.sln --no-restore -m:1
py -3 -m unittest discover -s tests -p 'test_joeytrace*.py' -v
dotnet run --project src/Joey.Lab --no-build -- effect-pilot src/tests/Joey.Tests/Fixtures/effect-pilot-synthetic.recipe.json
dotnet run --project src/Joey.Lab --no-build -- effect-pilot docs/research/sparks-sword-native.template.json
# Sintético: exit0; template nativo incompleto: exit3.
```

Se os assets .NET não existirem, `dotnet restore YugiRE.sln` antes do build.
Abrir Joey original **manualmente**, uma única instância. No PowerShell:

```powershell
$joeyProcesses = @(Get-Process joey_pc)
if ($joeyProcesses.Count -ne 1) { throw 'Escolha explicitamente uma unica instancia do Joey' }
$joeyProcessId = $joeyProcesses[0].Id
$joeyExe = $joeyProcesses[0].Path
$joeyExeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $joeyExe).Hash.ToLowerInvariant()
if ($joeyExeHash -ne 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de') { throw 'EXE diferente do alvo' }
$joeyProcessId
$joeyExeHash
py -3 scripts/joeytrace.py capture --pid $joeyProcessId --card-events --poll-ms 50 --duration 180 --no-replay-prompt --verbose
```

Esse coletor lê RAM externamente, não injeta DLL nem instala hooks. Ele valida
o EXE e produz pasta `scratch/traces/duel-...`. Iniciar a coleta **com o estado
inicial pronto**, para não perder a janela no limite180s. Seguir os dois cenários
exatos do contrato em sessões separadas. Ctrl+Shift+J cria marca, depois digitar
texto no console (`S-before`, `S-after`, `E-before`, `E-linked`, `E-removed`).
Não usar comandos draw/sparks/summon/set da ponte. Encerrar com `stop`.
A gravação de vídeo ou imagens da UI deve incluir LP, stats reais e cemitério
nos estágios indicados, com referências temporais anotadas e sem informação privada.
Não importar/salvar YDR para este primeiro piloto: relógio do replay não prova
sincronização com RAM. Não alterar deck/save por script para forçar cenário.

Após cada sessão (substituir caminho pelo exibido):

```powershell
$pilotSession = 'scratch/traces/duel-AAAAMMDD-HHMMSS-pidNNN'
Get-Content "$pilotSession/manifest.json"
Get-FileHash -Algorithm SHA256 "$pilotSession/manifest.json","$pilotSession/snapshots.jsonl","$pilotSession/state-changes.jsonl","$pilotSession/markers.jsonl","$pilotSession/marker-annotations.jsonl"
py -3 scripts/joeytrace.py effect-windows --session $pilotSession --before-ms 5000 --after-ms 10000
```

Conferir status final/finished_utc, PID/hash, read_error_count0, segmento único
na janela e associações com count válido. Não presumir que polling sem erros
capturou todos os estados intermediários. Guardar os brutos e hashes localmente,
sem editá-los. Compartilhar só extratos mínimos revisados e suas referências.

## Observação de comandos — somente após NOVA autorização explícita

A etapa externa não coleta o pedido de Sparks nem comandos ao vivo. Para reutilizar
o trace existente, uma autorização posterior precisa permitir attach Frida e
`Interceptor.attach` **no processo manual especificado**, sem DLL de regras,
substituição de callbacks ou escrita de estado. Este handoff não é autorização.
Sem ela, não executar o bloco abaixo. Não usar `experiment`, yugilab, spawn,
seed/deck/speed override, trace prep/demo ou modo de IA para este piloto.

Depois dessa autorização, usando a venv Frida **já existente**, em terminal2:

```powershell
New-Item -ItemType Directory -Force scratch/pilot | Out-Null
$joeyProcessId = [int](Read-Host 'PID exato da instancia manual usada na captura externa')
$observeProcess = Get-Process -Id $joeyProcessId
if ($observeProcess.ProcessName -ne 'joey_pc') { throw 'PID nao corresponde ao Joey' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $observeProcess.Path).Hash.ToLowerInvariant() -ne 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de') { throw 'EXE diferente' }
Start-Transcript -Path scratch/pilot/observe-console.txt -NoClobber
.\native_hooks\.venv-frida\Scripts\python.exe .\native_hooks\native_rules.py --observe-only --pid $joeyProcessId
```

No console da ponte, para **cada cenário separadamente**:

```text
trace on 20000
trace mark sparks-before
# Fazer a unica ativacao pela interface normal; nao executar "sparks" no console.
trace mark sparks-after
trace off
trace status
trace dump
stop
```

As linhas iniciadas com `#` acima são instruções humanas, não comandos do console.
Para Sword, usar marcas sword-before / sword-linked / sword-removed em torno das
ações naturais. Ao retornar ao PowerShell: `Stop-Transcript`. Exportar/hash o
JSONL commands-... exibido, log de preflight, PID e manifest joeytrace da mesma
sessão. O traceoff/dump não apaga o bruto. Não usar traceon novamente sem exportar.

Exigir seq contígua, seen=retained, overwritten0, ausência de readError e hash
do EXE ligado à captura. Fonte de tipo `commands` do comparador rejeita cabeçalho
sem exeSha256 ou contadores incompletos. Não troque para `jsonl` genérico para
contornar isso. O host atual exporta hostAttestedExeSha256 quando disponível;
se vier null, preservar a lacuna e o log de preflight para revisão separada.
LP do evento43 é **pré-enfileiramento**. A primeira mudança subsequente só pode
ser atribuída à Sparks com isolamento e correlação; não inferir alvo do resultado.
Esse trace ainda NÃO observa requestFlags/preparedTarget; essas células continuam
Missing. Novos probes dependeriam de outra tarefa/contrato e autorização.

## Preparar dossiê e comparar offline

Copiar o template para scratch/pilot/recipe.json. Acrescentar sources com paths,
format e SHA256 reais (minúsculas; PowerShell: `(Get-FileHash -Algorithm SHA256 $arquivo).Hash.ToLowerInvariant()`);
para cada input/resultado realmente observado, inserir
origin=Observed, source=id, line=linha não vazia1-based, pointer=/caminho/json.
Exemplo de snapshot joeytrace: `/players/0/life_points`; conferir a estrutura real
antes de usar. Fonte jsonl snapshots não é atestada pelo CaptureAnalyzer: revisar
manifest/read errors/segmentos explicitamente. Para dados transformados ou UI,
criar extrato auditado JSON com hashes/seq/timestamps/fórmula, método e revisor;
referenciar o valor desse extrato. Não copiar previsões C# para observações.

`windowIsolated`, `sandboxApplicable`, `genericGuardsSatisfied`, `dynamicType`,
`dynamicAttribute` e `noOtherModifiers` não se tornam Observed por serem convenientes.
Ausência de medição => Missing; hipótese de sandbox => Assumed com motivo.
Mapeamento de slot físico para zona lógica deve ter evidência; não subtrair5
por suposição. Se não houver observação de cemitério, deixá-lo Missing.

```powershell
dotnet run --project src/Joey.Lab --no-build -- effect-pilot scratch/pilot/recipe.json > scratch/pilot/comparison.json
$LASTEXITCODE
```

Exit0=Match escalar, 1=Mismatch por algum campo, 2=receita inválida, 3=Inconclusive.
Saída contém status/expected/observed/motivo por campo e evidenceKind. Não é replay
completo e não promove cobertura. Entregar bruto/hash locais, dossiê, relatório e
notas sobre ações/anomalias para auditoria; não apenas um total de matches.

## Resultado e parada

Sem capturas reais dos dois cenários neste checkout. Zero casos nativos completos;
somente fixtures Synthetic e template vazio. As lacunas do contrato impedem
promover paridade. **Parar aqui e aguardar os arquivos Windows**. Pode haver
resultados parciais Match de campos observados, mas o caso permanece Inconclusive
se algum campo necessário estiver ausente. Critérios de promoção futura no contrato;
as anotações anteriores permanecem inalteradas.

Commits:

- f7c0cb6 — contrato, cenários e template nativo sem observações.
- 710d05c — comparador, integração CLI e fixtures/testes sintéticos.
- Este handoff: commit `docs: hand off Windows Sparks and Sword observation pilot`,
  localizável com `git log --oneline -- docs/research/handoffs/native-sparks-sword-pilot.md`.

Ponto de parada mantido: nenhum próximo comando de captura será executado pelo
agente sem o retorno do operador e a autorização necessária.


## Verificação offline executada

```bash
dotnet build YugiRE.sln --no-restore -m:1
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
dotnet run --project src/tests/Joey.Tests --no-build
python3 -m unittest discover -s tests -p 'test_joeytrace*.py' -q
python3 -m unittest discover -s native_hooks/tests -q
node native_hooks/tests/test_loader.cjs
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p025_registry.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p045_registry.py' -q
dotnet run --project src/Joey.Lab --no-build -- effect-pilot src/tests/Joey.Tests/Fixtures/effect-pilot-synthetic.recipe.json
dotnet run --project src/Joey.Lab --no-build -- effect-pilot docs/research/sparks-sword-native.template.json
git diff --check
```

Resultados finais: build sem avisos/erros; 1051 checks .NET com DAT/deck e853
offline, incluindo16 checks de infraestrutura do piloto (21 campos sintéticos)
e regressões Sparks/P045 e Sword/P025. Python: 41 joeytrace +87 backend +quatro
registryP025 +cinco registryP045 =137 PASS. Loader JS PASS. Os testes do backend
usam mocks; não armaram hooks. Aviso preexistente SyntaxWarning em
joeytrace_replay.py (escape de docstring), sem falha. Erros de namespace e do
tratamento de InvalidDataException em teste negativo foram corrigidos antes
destes resultados; tentativas anteriores não foram contadas como PASS.

CLI fixture Synthetic: exit0, dois casos Match, nenhuma evidência nativa.
CLI template Native vazio: exit3, dois casos Inconclusive. Logs/relatórios locais:
/tmp/effect-pilot-build.log, /tmp/effect-pilot-tests.log,
/tmp/effect-pilot-tests-offline.log, /tmp/effect-pilot-loader.log,
/tmp/effect-pilot-synthetic-report.json e /tmp/effect-pilot-pending.json.
Nenhum PASS é atribuído à coleta Windows ou ao cenário nativo completo.

O teste histórico P025 foi ajustado para conferir as fontes do baseline12f560f
pinado pelo snapshot, em vez de exigir que Program.cs atual permaneça congelado
nesse commit. As anotações e seus estados não foram alterados. Core, hooks,
joeytrace e automação mantêm o código anterior. Apenas Lab/comparador e testes
foram ampliados. Nenhuma outra carta entra no comparador.
