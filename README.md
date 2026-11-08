# YugiRE

Engenharia reversa e protótipo de Yu-Gi-Oh! Power of Chaos: Joey the Passion.

Abra `YugiRE.sln` no Visual Studio e escolha `Joey.App` como projeto de
inicialização. Na raiz do repositório:

```sh
dotnet restore YugiRE.sln
dotnet build YugiRE.sln --no-restore
dotnet run --project src/Joey.App -- /caminho/para/data.dat
```

Requer SDK .NET 10 e uma cópia local dos dados do jogo.
[Execução no Windows, controles e limitações](src/README.md).
[Documentação da análise](docs/00-overview.md).

## Módulo Native Rules (C++ / DLL x86)

Os efeitos nativos de Sparks e Raimei e o console interativo de duelo estão em
[native_rules](native_rules/README.md). Esse módulo é independente da aplicação
.NET e pode ser compilado como DLL Win32 pelo CMake/Visual Studio. O host em
Python usa Frida para carregar a DLL no Joey e instalar os callbacks.

## YugiLab — laboratório automatizado

Para gravar uma quantidade definida de duelos entre IAs com os decks escolhidos,
use o [YugiLab](yugilab/README.md) no Windows. O laboratório inicia um Joey
próprio, vincula a captura ao PID recém-criado, salva o trace continuamente
e **fecha automaticamente somente esse processo** ao alcançar a meta:

```powershell
# Na raiz do repositório, com o ambiente Frida existente
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 100 --workers 10 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

O `native_rules/` permanece como backend da DLL, hooks e ferramentas de
engenharia reversa; seus comandos existentes continuam compatíveis.

Para comparar desempenho, comece com `--duels 6 --workers 2` e
depois experimente 4/10 workers. Cada instância tem PID próprio, captura
JSONL e encerramento supervisionado. O `manifest.json` de cada rodada
paralela fica em `scratch/labs/parallel-.../` e **não garante ausência
de interferências em saves, registro ou replays compartilhados**.

## Reprodutibilidade por seed e regressão

O laboratório aceita `--seed 12345` para gerar duelos controlados e
comparar duas capturas independentemente de seus PIDs:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab.compare `
  ".\primeira.zip" ".\segunda.zip" `
  --output ".\scratch\comparison.json"
```

A comparação verifica os decks completos, eventos de IA,
comandos nativos e checkpoints de estado. Diferenças nos bits
superiores de argumentos são **preservadas e reportadas**.
Veja [o relatório original](docs/evidence/joey-command-highword-drift-2026-11-08.md)
e a [documentação do comparador](yugilab/README.md).

Também existe uma primeira implementação isolada do `rand`/shuffle
nativo em [Joey.Core/NativeRng.cs](src/Joey.Core/NativeRng.cs).
Seus testes com hashes de decks capturados podem ser executados com
`dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj`.
O protótipo `DemoDuel` continua usando seu shuffle próprio.

## Primeiro teste diferencial de invocação normal (Joey → C#)

`Joey.Tests` inclui dois fixtures **parciais, obtidos de
observações do Joey original**, que inicializam `DemoDuel`,
executam `Summon` e comparam o resultado observado (quantidade
de cartas na mão, índice interno e slot do monstro, LP quando
medidos). Uma observação também contém o contexto nativo
`0xCE/0x4103` para conferir lado/índice/slot.

O executor de fixtures **não reexecuta um duelo completo**:
estados desconhecidos são deixados como aproximações explícitas;
não há equivalência de eventos de IA ou de todo o estado nativo.
Veja [os limites e evidências](docs/evidence/joey-first-differential-normal-summon-2026-11-08.md).

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```
