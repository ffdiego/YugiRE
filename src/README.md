# Protótipo .NET 10 + Raylib

Na pasta `src/Joey.App`, execute:

```bash
dotnet run
```

Requer SDK .NET 10, desktop gráfico e bibliotecas do sistema usadas pelo Raylib.
A dependência Raylib-cs 7.0.2 fica fixada no projeto (compatível via net8.0).
O aplicativo procura `passion/data.dat` nos diretórios ancestrais da pasta atual.
Para outra instalação:

```bash
dotnet run -- /caminho/para/data.dat
```

Os arquivos originais são abertos somente para leitura e não são distribuídos
com o código. Voice.dat e Region.dat não são necessários nesta versão.
Não há extração permanente de recursos nem dependência do Ghidra em execução.

## Telas e controles

- Menu: Single Duel Mode, Card List e Quit Game funcionais. Deck Construction
  e Options permanecem desabilitados.
- Card List: digite para pesquisar por nome; Backspace apaga. Selecione uma
  linha para ver atributos e descrição; Previous/Next mudam a página.
  Não há restrição de desbloqueio. Variantes mantêm seus IDs e índices próprios;
  registros sem nome são identificados, sem inferir jogabilidade.
- Duelo: selecione uma carta na mão e clique em uma zona de monstro própria
  vazia. Next turn alterna o jogador; Reset restaura o cenário.
- Escape ou Menu retornam ao menu principal. A janela pode ser redimensionada;
  a área de interação acompanha a escala da imagem.

## Procedência e limites

| Categoria | Uso nesta versão |
|---|---|
| Comprovado estaticamente por RE | Índice DAT, troca de nibbles, LZSS, IDs/índices, nomes, descrições e campos de propriedades; referências em `../docs/02-file-formats.md`. |
| Observado no jogo original | Organização geral de telas fornecida pelo usuário; fidelidade visual ainda não conferida neste ambiente. |
| Hipótese | Códigos de classe são preservados numericamente; não atribuímos nomes a classes ainda não confirmadas. |
| Demonstração | `Joey.Core/Demo.cs`: mãos fixas, oponente estático, LP 8000, turnos manuais e invocações sem tributos, limites por turno ou efeitos. A política de aceitar classes 0–20 como monstros é uma simplificação local. |

Os nomes e atributos são reais; os desenhos são representações temporárias,
sem ilustrações originais. LP não muda nesta etapa. Não há batalha, IA,
efeitos, compra automática, vitória ou multiplayer. Textos de cartas exibidos
não implicam que seus efeitos estejam implementados.

## Validação

A partir de `src/`:

```bash
dotnet build Joey.App
dotnet run --project tests/Joey.Tests
dotnet run --project tests/Joey.Tests -- ../../passion/data.dat
dotnet run --project Joey.App -- --check-data
```

`Joey.Tests` é um executável de verificações, sem framework externo; falha com
código diferente de zero. `dotnet test` não substitui esse comando. São 16
verificações sintéticas e mais 7 contra o arquivo original opcional.

Nesta sessão: compilação concluída; 23 verificações C# e 14 testes Python
existentes passaram. A leitura real encontrou 3568 entradas DAT e 1114 registros
sem a sentinela; isso não declara 1114 cartas jogáveis. ID/nome descomprimidos
foram comparados aos hashes existentes e Blue-Eyes aos atributos conhecidos.
Os testes PowerShell existentes requerem `pwsh`, ausente neste ambiente.

Não houve validação visual: sessão SSH sem DISPLAY/WAYLAND_DISPLAY. O modo
`--check-data` valida os dados sem abrir a janela; execução gráfica nesta
condição encerra com código 2 e mensagem explicativa. Dependências nativas
vinculadas diretamente foram encontradas por `ldd`; isso não prova que o
contexto gráfico inicializa. NuGet pode emitir NU1900 se a rede estiver
indisponível para consulta de vulnerabilidades.

Roteiro visual pendente: abrir a janela, percorrer os três destinos do menu,
pesquisar Blue-Eyes e Parasite, mudar páginas, tentar zonas inválidas,
invocar em zona livre, avançar turno, reiniciar e redimensionar a janela.
