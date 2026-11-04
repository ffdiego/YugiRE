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
