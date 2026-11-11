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

-602 checks .NET passaram antes da reorganização.
-81 testes Python do backend: um erro anterior à migração, em expectativa
  antiga da ordem de cleanup (`test_capture_rotation`).
-49 testes yugilab: uma falha anterior, pois o teste esperava lançar quatro
  workers mesmo após o segundo falhar durante startup; o código já para antes.

As falhas serão tratadas separadamente de alterações de caminhos. Nenhum
Joey foi iniciado e nenhuma DLL carregada para executar os testes.
