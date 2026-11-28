# Joey.Tools

Utilitários de linha de comando para o YugiRE (.NET 10, Windows/Linux).
Não precisam do jogo em execução, Raylib, Ghidra ou Frida.
Execute a partir da raiz do repositório.

## DAT

```sh
dotnet run --project src/tools/Joey.Tools -- --help
# Listar todos os recursos (tamanho descomprimido, tamanho armazenado, caminho):
dotnet run --project src/tools/Joey.Tools -- dat list ../passion/data.dat
# Listar somente BMPs em uma pasta recursivamente:
dotnet run --project src/tools/Joey.Tools -- dat list ../passion/data.dat --folder j/duel --ext bmp
# Extrair pelo nome completo:
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/data.dat --name j/duel/field/fie_normal.bmp
# Pelo nome do arquivo (todos os caminhos que correspondem):
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/data.dat --name title_1_e.bmp
# Toda uma pasta (inclui subpastas):
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/data.dat --folder j/title
# Todos os BMPs do duelo:
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/data.dat --folder j/duel --ext bmp
# Busca por trecho do caminho:
dotnet run --project src/tools/Joey.Tools -- dat list ../passion/data.dat --match fie_
# Todo o arquivo (incluindo binários, áudio e YGA):
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/data.dat --all
# Voice.dat: escolha um destino próprio para outro lote:
dotnet run --project src/tools/Joey.Tools -- dat extract ../passion/Voice.dat --ext wav --output scratch/voices
```

Os filtros são combinados por AND, sem distinção de maiúsculas/minúsculas.
Aceitam barras normais ou invertidas; prefira barras normais no shell.
`--name` é exato: se for só o nome base, seleciona todos os recursos com
esse nome; se tiver diretórios, seleciona o caminho interno completo.
`--folder` respeita fronteiras de pasta. `--match` procura uma substring.
`list` sem filtros lista tudo. `extract` exige filtros ou `--all` explícito.

Por padrão os resultados ficam em `scratch/dat-extracted/<nome-do-DAT-sem-extensão>/`,
por exemplo `scratch/dat-extracted/data/j/duel/field/fie_normal.bmp`.
O caminho interno e os bytes dos recursos são preservados após a descompressão
LZSS do arquivo. Não modifica o DAT, não converte YGA em PNG nem aplica máscaras
ou color keys de renderização. Limite de 256 MiB por recurso armazenado sem
compressão; recursos comprimidos conservam o limite de 16 MiB do Archive.
A ferramenta rejeita caminhos perigosos, links/reparse points existentes no
destino e colisões; nunca sobrescreve arquivos. Se quiser repetir uma extração,
use outro `--output` ou remova os resultados antigos manualmente.
Se ocorrer erro de leitura/escrita durante a extração, arquivos anteriores do
lote podem permanecer; não é uma operação transacional. Use diretório local
privado, não modificado simultaneamente por outro processo.

Para testes HD, comece por `j/title/title_1_e.bmp`,
`j/list/list_bg.bmp`, `j/duel/field/fie_normal.bmp` e
`j/duel/detail/detail.bmp`. Não versione recursos originais ou processados;
`scratch/` é ignorado pelo Git.

Leitura do DAT compartilhada com [Joey.Core/Archive.cs](../../Joey.Core/Archive.cs).
Testes sintéticos no runner Joey.Tests, sem precisar de um DAT proprietário.
