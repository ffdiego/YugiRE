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
  --duels 100 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

O `native_rules/` permanece como backend da DLL, hooks e ferramentas de
engenharia reversa; seus comandos existentes continuam compatíveis.
