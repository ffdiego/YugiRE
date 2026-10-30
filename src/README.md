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

- Menu: fundo e botões originais lidos do DAT em 800×600. Single Duel Mode,
  Card List e Quit Game funcionais. Deck Construction abre o inspetor de deck
  descrito abaixo; edição ainda não implementada. Options permanece desabilitado.
- Card List: grade de 10 colunas × 5 linhas, 50 cartas por página, em ordem
  alfabética pela tabela inglesa do DAT. Passe o mouse sobre a miniatura para
  ver atributos e descrição. As setas originais ou PageUp/PageDown mudam a página.
  Digite para pesquisar por nome; Backspace apaga. Registros sem nome ficam no fim.
  O fundo, o título, o botão Back e as imagens das cartas vêm do DAT.
  Use a roda do mouse sobre a descrição para percorrer textos longos.
  Os botões originais All cards e Joey alternam entre as 771 cartas dos três
  jogos e as 350 do Joey, sem restrição de desbloqueio. A busca é preservada
  entre filtros e a página retorna ao início. O filtro
  usa `card_pack.bin`, não a imagem da carta. Para inspecionar os 1114 registros
  internos, use `dotnet run -- --include-unused` (junto ao caminho DAT, se necessário).
  Esse modo mantém variantes e registros sem nome, sem inferir jogabilidade.
- Duelo local: a mão exibida pertence ao jogador ativo, alternado por End turn.
  Draw compra uma carta por turno; selecione um monstro e uma zona própria vazia
  para invocar uma vez por turno. Níveis 5–6 exigem um tributo; 7+ exigem dois.
  Selecione os monstros próprios, depois uma zona vazia ou selecionada. Cancel
  abandona a seleção. É possível liberar uma zona de um campo cheio.
  Fusões, Rituais e monstros especiais reconhecidos pelo gate nativo são
  recusados para invocação normal; seus procedimentos especiais não estão implementados.
  Evidência e limites: [invocação normal](../docs/26-normal-summon-tributes.md).
- Use card ativa Sparks ou Ookazi da mão. Para Legendary Sword, selecione a carta e clique em um Warrior no campo
  de qualquer jogador. Ela ocupa uma zona de magia própria e concede +300/+300.
  Os demais efeitos são recusados.
- Battle inicia combate após o primeiro turno. Clique em um monstro próprio,
  depois em um monstro inimigo; Direct attack funciona se o adversário não
  tiver monstros. Cada monstro ataca uma vez. Em Main, Cancel e clique num monstro próprio para alternar ataque/defesa
  aberta uma vez por turno. Monstros em defesa não atacam.
- Hand </> percorrem mãos grandes. Cancel limpa seleção; Reset reinicia.
- End turn com mais de seis cartas exige descarte. Selecione uma carta e use
  **Discard** até ficar com seis; só então o jogador muda. A regra básica foi
  confirmada por RE em [limite da mão](../docs/25-end-turn-hand-limit.md).
  A exceção de Infinite Cards e outros efeitos de fim de turno ainda não são simulados.
- Escape ou Menu retornam ao menu principal. A janela pode ser redimensionada;
  a área de interação acompanha a escala da imagem.

## Importar e comparar um deck original

O inspetor procura `deck.ydc` ao lado de `data.dat`. Para outra localização,
execute da raiz do repositório no Windows:

```powershell
dotnet run --project .\src\Joey.App -- "C:\Jogos\Joey\data.dat" "--deck=C:\Jogos\Joey\deck.ydc"
```

Abra **Deck Construction**. As abas mostram a lista principal e as duas listas
adicionais, preservando ordem e cópias repetidas. Os papéis das listas 1 e 2
continuam sem confirmação completa; não atribuímos nomes modernos por analogia.
**Reload** relê o mesmo arquivo após uma alteração feita no original.
Para comparar nomes e contagens sem janela, acrescente `--check-deck` ao comando.
Esse modo verifica estrutura e resolução de IDs, não legalidade nem o cabeçalho
de compatibilidade ainda desconhecido. O arquivo é aberto somente para leitura.

**Play this deck** inicia o duelo local com o mesmo deck principal para ambos
os jogadores; cada jogador recebe cinco cartas e pode comprar do restante.
Requer 40–80 cartas, conforme a checagem nativa documentada. O embaralhamento
com semente fixa é política de demonstração, não o algoritmo original.
**Demo fixture** retorna às cartas de teste anteriores. Reset preserva a opção
usada ao iniciar o duelo. As listas adicionais não participam da simulação.
Os efeitos suportados continuam limitados aos já descritos acima.

O inspetor reutiliza o fundo e as miniaturas da Card List com controles
temporários; não reproduz a tela original de construção de decks.
Comparação sugerida no Windows: conferir as três contagens, duas cópias da
mesma carta, nomes/ilustrações e a segunda lista. Na amostra deste workspace,
esperamos 42/1/0 e Alligator's Sword Dragon na lista 1. Ao iniciar o demo,
a mão deve ter cinco cartas e o deck restante 37; Draw muda para seis/36.

## Procedência e limites

| Categoria | Uso nesta versão |
|---|---|
| Comprovado estaticamente por RE / recursos | BMPs e manifestos de posições/imagens, índice DAT, troca de nibbles, LZSS, IDs/índices, nomes, descrições e campos de propriedades; referências em `../docs/02-file-formats.md`. |
| Observado no jogo original | Organização geral de telas fornecida pelo usuário; fidelidade visual ainda não conferida neste ambiente. |
| Hipótese | Códigos de classe são preservados numericamente; não atribuímos nomes a classes ainda não confirmadas. |
| Demonstração | `Joey.Core/Demo.cs`: mãos/decks fixos, LP 8000, dois jogadores locais, turnos manuais, uma compra e invocação por turno, seleção local de tributos e combate simplificado por ATK/DEF. Classes 1–20 como monstros seguem o gate nativo; restrições especiais completas de invocação continuam ausentes. |

`Joey.Core/DeckFile.cs` transcreve o leitor YDC comprovado em
`../docs/02-file-formats.md`, preservando os oito bytes opacos do cabeçalho,
IDs, ordem e duplicatas. Importar esse deck para os dois jogadores do demo
e embaralhá-lo com semente fixa são escolhas locais de demonstração.

Legendary Sword usa a regra de seleção e bônus de `../docs/cards/legendary-sword.md`
e limpeza comum de `../docs/cards/equipment-cleanup.md`. O tipo estático 15 é
usado como aproximação do tipo dinâmico, pois não há modificadores de tipo
neste sandbox. Ao destruir o alvo, cada equipamento vai ao cemitério de seu
dono; reutilizar a zona não mantém o bônus. Exceções de efeitos, Banisher,
fichas e negação não são reproduzidas.

Os nomes, atributos, fundos e ilustrações vêm do DAT. O menu usa os cinco
botões originais e suas dimensões; Card List usa também título e botão Back
originais, além de moldura, setas e números de página. As três telas usam
800×600, com escala proporcional ao redimensionar. A grade usa os passos
nativos de 55×87 pixels. A busca e os controles e posições das cartas no duelo
ainda são temporários. O menu usa dois quadros estáticos em lugar da animação.
Alguns registros apontam para imagens compartilhadas de placeholder no próprio
DAT; isso não significa que tenhamos uma ilustração exclusiva para cada carta.
Evidências e limites: [gráficos DAT](../docs/24-dat-graphics.md). Sparks/Ookazi causam 200/800 ao oponente (RE: `../docs/cards/sparks-ookazi.md`).
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
dotnet run --project tests/Joey.Tests -- ../../passion/data.dat ../../passion/deck.ydc
dotnet run --project Joey.App -- --check-data
dotnet run --project Joey.App -- --check-graphics
```

`Joey.Tests` é um executável de verificações, sem framework externo; falha com
código diferente de zero. `dotnet test` não substitui esse comando. São 109
verificações sintéticas, mais 23 contra o DAT e seis contra o deck de referência
opcionais (o terceiro comando de testes acima usa a amostra de 42/1/0).

Nesta sessão: compilação concluída; 138 verificações C# e 25 testes Python
existentes passaram. A leitura real encontrou 3568 entradas DAT e 1114 registros
sem a sentinela; isso não declara 1114 cartas jogáveis. ID/nome descomprimidos
foram comparados aos hashes existentes e Blue-Eyes aos atributos conhecidos.
`--check-graphics` decodificou 793 BMPs com o Raylib sem criar janela,
e o manifesto associou os 1114 registros por índice e ID.
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
