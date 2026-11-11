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
