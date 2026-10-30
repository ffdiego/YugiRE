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

## Windows e Visual Studio

Abra `YugiRE.sln` na raiz do repositório e defina **Joey.App** como projeto
de inicialização. A solução inclui o leitor, a aplicação e o executável de
verificações. A partir da raiz, também é possível usar PowerShell:

```powershell
dotnet restore .\YugiRE.sln
dotnet build .\YugiRE.sln --no-restore
dotnet run --project .\src\Joey.App -- "C:\Jogos\Joey\data.dat"
```

Ajuste o caminho de `data.dat` para sua instalação. Ele não acompanha o clone.
Para corrigir NETSDK1004 antes de obter a solução, restaure diretamente o
projeto: `dotnet restore .\src\Joey.App\Joey.App.csproj`. Isso restaura
Joey.Core por sua referência. `project.assets.json` é gerado pelo NuGet em
`obj/`; essa pasta fica fora do Git e deve ser recriada em cada máquina.
Se a restauração falhar, resolva o erro apresentado pelo próprio restore
antes de executar build com `--no-restore`.

No Visual Studio, a ação **Restaurar Pacotes NuGet** no menu da solução
executa essa etapa caso a restauração automática esteja desabilitada.

## Telas e controles

- Menu: Single Duel Mode, Card List e Quit Game funcionais. Deck Construction
  e Options permanecem desabilitados.
- Card List: digite para pesquisar por nome; Backspace apaga. Selecione uma
  linha para ver atributos e descrição; Previous/Next mudam a página.
  Não há restrição de desbloqueio. Variantes mantêm seus IDs e índices próprios;
  registros sem nome são identificados, sem inferir jogabilidade.
- Duelo local: a mão exibida pertence ao jogador ativo, alternado por End turn.
  Draw compra uma carta por turno; selecione um monstro e uma zona própria vazia
  para invocar uma vez por turno. Não há custos de tributo nesta demonstração.
- Use card ativa Sparks ou Ookazi da mão. Para Legendary Sword, selecione a carta e clique em um Warrior no campo
  de qualquer jogador. Ela ocupa uma zona de magia própria e concede +300/+300.
  Os demais efeitos são recusados.
- Battle inicia combate após o primeiro turno. Clique em um monstro próprio,
  depois em um monstro inimigo; Direct attack funciona se o adversário não
  tiver monstros. Cada monstro ataca uma vez. Em Main, Cancel e clique num monstro próprio para alternar ataque/defesa
  aberta uma vez por turno. Monstros em defesa não atacam.
- Hand </> percorrem mãos grandes. Cancel limpa seleção; Reset reinicia.
- Escape ou Menu retornam ao menu principal. A janela pode ser redimensionada;
  a área de interação acompanha a escala da imagem.

## Procedência e limites

| Categoria | Uso nesta versão |
|---|---|
| Comprovado estaticamente por RE | Índice DAT, troca de nibbles, LZSS, IDs/índices, nomes, descrições e campos de propriedades; referências em `../docs/02-file-formats.md`. |
| Observado no jogo original | Organização geral de telas fornecida pelo usuário; fidelidade visual ainda não conferida neste ambiente. |
| Hipótese | Códigos de classe são preservados numericamente; não atribuímos nomes a classes ainda não confirmadas. |
| Demonstração | `Joey.Core/Demo.cs`: mãos/decks fixos, LP 8000, dois jogadores locais, turnos manuais, uma compra e invocação por turno, invocações sem tributos e combate simplificado por ATK/DEF. A política de aceitar classes 0–20 como monstros é uma simplificação local. |

Legendary Sword usa a regra de seleção e bônus de `../docs/cards/legendary-sword.md`
e limpeza comum de `../docs/cards/equipment-cleanup.md`. O tipo estático 15 é
usado como aproximação do tipo dinâmico, pois não há modificadores de tipo
neste sandbox. Ao destruir o alvo, cada equipamento vai ao cemitério de seu
dono; reutilizar a zona não mantém o bônus. Exceções de efeitos, Banisher,
fichas e negação não são reproduzidas.

Os nomes e atributos são reais; os desenhos são representações temporárias,
sem ilustrações originais. Sparks/Ookazi causam 200/800 ao oponente (RE: `../docs/cards/sparks-ookazi.md`).
Ativação imediata sem correntes, cemitério, combate, bloqueio de batalha no
primeiro turno, derrota por deck vazio e vitória por LP são políticas do demo,
ainda não atribuídas ao motor original. Não há IA, cartas viradas para baixo, efeitos de monstros,
armadilhas, compra automática ou multiplayer em rede. Textos de cartas exibidos
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
código diferente de zero. `dotnet test` não substitui esse comando. São 48
verificações sintéticas e mais 7 contra o arquivo original opcional.

Nesta sessão: compilação concluída; 55 verificações C# e 14 testes Python
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

Conferência posterior por RE: a comparação nativa só destrói ambos em empate
de ATK **positivo** ([evidência](../docs/22-battle-core.md)). O protótipo foi corrigido para manter ambos em 0 contra 0,
com teste que falhava antes da correção. Os demais limites do modo de
demonstração continuam válidos; não foi feita validação visual nesta sessão.
