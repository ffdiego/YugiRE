# Migração gradual para Joey.Lab

## Dependências antes da reorganização

| Componente | Dependência atual | Destino |
| --- | --- | --- |
| Joey.Core | Sem Python/Frida; tipos e algoritmos independentes. | Permanece engine. |
| Joey.App | Referência direta a Core e raylib-cs. | Permanece interface. |
| Joey.Lab | Nova console net10.0 com referência direta a Core. | Importação e análise offline primeiro. |
| yugilab | Acrescenta o diretório do backend ao sys.path e importa native_rules.py; orquestra subprocessos, comparação, RNG e fixtures. | Preservar durante migração. |
| native_rules | Launcher Python, agente Frida, preflight, captura e DLL C++ x86. Caminhos internos partem de __file__. | Renomear diretório para native_hooks sem mudar módulos, ABI ou hooks. |
| Joey.Tests | Copia decks JSON do backend para fixtures. | Atualizar caminho após renomeação. |

O nome do diretório e o nome do módulo Python não precisam coincidir:
`native_hooks/native_rules.py` continuará sendo o launcher legado.
Não mudar o nome da DLL `yugire_rules.dll`, exports, headers ou protocolo JSONL.
Não implementar gerenciamento de processos ou Frida em C# nesta etapa.

## Baseline no Fedora

- 602 checks .NET passaram antes da reorganização.
- 81 testes Python do backend: um erro anterior à migração, em expectativa
  antiga da ordem de cleanup (`test_capture_rotation`).
- 49 testes yugilab: uma falha anterior, pois o teste esperava lançar quatro
  workers mesmo após o segundo falhar durante startup; o código já para antes.

As falhas serão tratadas separadamente de alterações de caminhos. Nenhum
Joey foi iniciado e nenhuma DLL carregada para executar os testes.


## CLI inicial e formatos

```bash
dotnet run --project src/Joey.Lab -- summarize capture.jsonl
dotnet run --project src/Joey.Lab -- summarize capture.jsonl --json \
  --expected-sha256 HASH
```

Sem argumentos ou com `--help`, mostra ajuda. Exit0: importação sem erros de
integridade detectados; exit2: entrada inválida, hash divergente ou erros de
integridade. Avisos (hash do EXE ausente, captura live sem footer) não se
transformam em campos fictícios nem em validação do estado do duelo.
O hash do arquivo é calculado sempre, mesmo quando não é fornecido um hash
esperado. JSONL é lido sem escrever na captura.

`CaptureReader` preserva campos desconhecidos como JsonElement. Os tipos do
laboratório referenciam `NativeAiStats`/`NativeAiCombatEvent` de Core, sem
adicionar dependência reversa. `CaptureAnalyzer` conta kinds, comandos por
opcode mascarado, outputs, seletores e stats pareados por thread/callId.
Suporta cabeçalhos de ring buffer e `live_duels` com `capture_summary`.
A soma de comandos terminais não informa vencedor nem legalidade.

Posições, flags e efeitos não são resolvidos. O relatório não executa uma
simulação, não calcula score ausente e não converte stats pareados em um
replay completo. Eventos de instrumentação mais novos permanecem acessíveis
no Raw, sem decoder especulativo. A primeira versão mantém os eventos em
memória; ZIP/múltiplos workers/streaming incremental ficam no legado.

### Comparação independente

Captura local real `commands-20261111-022940-355228-pid36348.jsonl`, SHA256
`dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`:
1120 eventos (649command,470ai_probe,1marker),402 stats,22 outputs de combate,
14 pares de stats,13 seletores normais (7 negativos) e10 prioritários.
Esses valores coincidem com `scripts/ai_decision_trace_audit.py` e o corpus
`ai-combat-differential.json`, independentemente da nova implementação C#.
O EXE SHA está null na captura: o relatório preserva isso e emite aviso.

O fixture portátil `lab-capture-prefix.jsonl` conserva eventos1..30 idênticos
à captura, ajustando apenas contadores do cabeçalho para30. Proveniência e
transformação estão explícitas no próprio fixture; ele não é a captura
completa. Inclui stats Gemini Elf/Axe Raider e output0808C800 no evento25,
dentro do seletor que retorna550 no evento26.

## Renomeação isolada

`native_rules/` foi movido para `native_hooks/`. O módulo legado continua
`native_rules.py`; o yugilab aponta seu BACKEND_DIR para o novo diretório.
DLL `yugire_rules.dll`, CMake, exports, headers, fonte C++, launcher e os dois
agentes JavaScript foram comparados byte a byte com o commit anterior:
nenhuma mudança. Apenas caminhos em documentação/testes, backend do yugilab
e mensagens de instalação foram ajustados. Os comandos reproduzíveis dos
relatórios históricos usam agora o nome novo; buffers capturados não mudaram.

A execução dos testes Python após a renomeação reproduziu exatamente as duas
falhas da baseline, sem outras falhas. Três suítes JavaScript e os entrypoints
`--help` do launcher, yugilab e comparador passaram nos novos caminhos.
Não há shim no diretório antigo: atualizar comandos para
`python native_hooks/native_rules.py --observe-only --pid PID`.

## Testes legados corrigidos separadamente

As duas falhas da baseline eram expectativas anteriores ao cleanup atual.
`test_capture_rotation` agora exige export→restore→kill ainda anexado e
proíbe unload/detach nesse caminho; o launcher já tinha esse comportamento
para evitar a race do Frida no Windows. `test_parallel` agora exige que a
falha imediata do segundo worker impeça lançar o terceiro/quarto, preservando
os dados e o status de falha. Nenhuma implementação de processo mudou.
Três novos testes verificam backend/módulo, fixtures .NET e entrypoints
iniciados por caminhos absolutos a partir de outro diretório.
Resultado:84 testes native_hooks e49 yugilab passaram no Fedora.

## Inventário do que permanece em Python

| Módulo | Responsabilidade ainda não migrada | Próxima fronteira sugerida |
| --- | --- | --- |
| yugilab/command_words.py | Classificação de diferenças dos high words de comandos. | Portar decoder puro com oráculos existentes. |
| yugilab/seed_math.py | Derivação do seed por índice global e helpers MSVC. | Reutilizar equivalentes Core quando a semântica coincidir. |
| yugilab/rng_audit.py | Fingerprints de shuffle, colisões entre workers e entrada ZIP/diretório. | Analisador offline tipado. |
| yugilab/compare.py | Comparação de execuções, checkpoints observados e diferenças raw/low/high. | Subcomando compare, sem prometer replay integral. |
| yugilab/export_summons.py | Exporta transições confirmadas e valida snapshots before/after. | Exportador offline para fixtures Core. |
| scripts/ai_decision_trace_audit.py | Auditoria completa, links temporais seleção→CE, read failures e hand snapshots. | Contagens/janelas já iniciadas em Lab; links CE ainda pendentes. |
| scripts/build_ai_combat_corpus.py | Pareamento, proveniência, tradução de IDs pelo DAT e exportação de corpus. | Pareamento iniciado; exportação e resolução de IDs ainda pendentes. |
| scripts/decode_ai_combat_event.py / decode_ai_combat_stats.py | Decoders CLI. | O leitor Lab já usa tipos equivalentes de Core; CLIs legados preservados. |
| Outros scripts de pesquisa | Validação de draws, gating, atributos e evidências dirigidas. | Migrar por necessidade, com fixtures/oráculos próprios. |
| yugilab/__init__.py / __main__.py | CLI de captura, decks, workers, seed, cancelamento e dispatch para backend. | Manter até existir substituto de processo validado no Windows. |
| yugilab/parallel.py | Coordenador de subprocessos, manifests e consolidação. | Posterior à camada de processos; não iniciado. |
| native_hooks/native_rules.py | Attach/spawn, startup, batching e cleanup supervisionado. | Suporte Python temporário, sem port de processos nesta etapa. |
| native_hooks/patch_dual_rules.py | Host Frida, console/RPC, exportação e experimentos com DLL. | Separar políticas de laboratório do transporte em etapas futuras. |
| native_hooks/duel_capture.py | Drain/ack lossless, fsync, contadores de duelos e footer. | Protocolo de captura permanece intacto. |
| native_hooks/owned_process.py | HANDLE Win32, verificação de encerramento e fallback taskkill. | Exige testes Windows; não substituir por kill não verificado. |
| native_hooks/pe_preflight.py / deck_config.py | PE/hash e validação de planos de deck. | Compartilhar tipos apenas quando contratos forem equivalentes. |

Meta: `native_hooks` conter somente transporte/agentes, DLL, preflight e
serviços necessários para instrumentar o EXE; `Joey.Lab` concentrar análise,
políticas e experimentos. Nesta primeira etapa não movemos lógica Python
entre diretórios além da renomeação, para evitar quebrar imports e fluxos.
Não há referência de Core a Lab, à DLL ou aos scripts. Joey.Tests referencia
Lab para testar seu importador; App continua referenciando somente Core.

## Windows e artefatos locais

Não foram validados no Fedora: attach a PID real, bootstrap/spawn, console
Frida real, shutdown por HANDLE, leitura de registro/perfil, build x86 da DLL,
venv Windows e cache CMake local. Os mocks JS/Python e preflight do PE foram
executados, sem carregar DLL ou chamar o Joey. Nenhum novo fluxo Windows foi
introduzido. A ABI, as assinaturas de hooks e o protocolo não mudaram.

Venv e builds são artefatos locais ignorados pelo Git; um git pull não migra
esses arquivos. Venvs podem conter caminhos absolutos do diretório antigo.
Criar um ambiente novo no destino é preferível a editar scripts de ativação:

```powershell
# Na raiz do repositório; apenas se ainda não houver ambiente válido no destino:
py -3 -m venv native_hooks/.venv-frida
.\native_hooks\.venv-frida\Scripts\python.exe -m pip install frida
.\native_hooks\.venv-frida\Scripts\python.exe .\native_hooks\native_rules.py --help
# Joey iniciado normalmente pelo usuário:
.\native_hooks\.venv-frida\Scripts\python.exe .\native_hooks\native_rules.py --observe-only --pid PID
```

Esse attach observacional não precisa da DLL. Para os experimentos que a
carregam, reconstruir pelo CMake no novo caminho conforme native_hooks/README.
Não reutilizar cache CMake apontando para a source tree antiga. O nome do
arquivo gerado e os exports permanecem os mesmos. Não remover venv/builds
antigos automaticamente: verificar primeiro o ambiente local.

## Verificação reproduzível no Fedora

```bash
export DOTNET_CLI_HOME=/tmp/joey-dotnet
export NUGET_PACKAGES=/home/diego/.nuget/packages
dotnet build YugiRE.sln --no-restore -m:1
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
python3 -m unittest discover -s native_hooks/tests
python3 -m unittest discover -s yugilab/tests
node native_hooks/tests/test_loader.cjs
node native_hooks/tests/test_deck_trace.cjs
node native_hooks/tests/test_demo_rng_seed.cjs
python3 native_hooks/native_rules.py --help
python3 -m yugilab --help
```

Para checkout novo, executar `dotnet restore YugiRE.sln` antes do build.
O teste .NET do prefixo é portátil; a comparação da captura completa é
executada adicionalmente quando o arquivo original existe em scratch/traces.
A ausência desse arquivo não impede usar o CLI com uma captura fornecida.

## Resultado e continuidade

Build completo: zero avisos/erros.629 checks .NET passaram com a captura
completa local (628 se esse arquivo opcional não estiver disponível).
84 testes Python de native_hooks e49 de yugilab passaram. As três suítes
JavaScript passaram antes e depois da mudança. Entry points --help passaram
sem Frida, inclusive launcher por caminho absoluto de outro diretório.
Os17 arquivos de agente/launcher/CMake/C++/ABI verificados ficaram idênticos.

[Comparação registrada](../evidence/joey-lab-jsonl-summary-validation-2026-11-11.json):
além das contagens, todos os23 retornos de seletores, os22 buffers de combate
e os stats pareados concordaram com os auditores/fixtures Python anteriores.
Isso verifica importação e correlação; não prova o algoritmo da IA.

Para repetir a comparação com a captura completa:

```bash
dotnet run --project src/Joey.Lab -- summarize \
  scratch/traces/commands-20261111-022940-355228-pid36348.jsonl --json \
  --expected-sha256 dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90
python3 scripts/ai_decision_trace_audit.py \
  scratch/traces/commands-20261111-022940-355228-pid36348.jsonl \
  --expected-sha256 dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90
```

Commits desta etapa:

- 3739f1b: projeto console e referência direta a Core.
- 704a87c: leitor, relatório tipado, testes e fixture histórico portátil.
- 309fdfc: renomeação separada para native_hooks e caminhos atualizados.
- cfe7b67: regressões de caminhos e correção das duas expectativas antigas.
- Commit seguinte: documentação consolidada e resultado da comparação.

Próximo incremento recomendado: comando de comparação offline entre capturas,
portando command_words/compare com os fixtures existentes. Continuar sem
reimplementar processos ou Frida em C# até que essa fronteira seja uma etapa
explícita e possa ser validada no Windows. Não remover yugilab ou Python.
