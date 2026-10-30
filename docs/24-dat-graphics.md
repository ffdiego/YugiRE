# Gráficos lidos diretamente do DAT

Alvo: `data.dat`, SHA256 `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
O leitor usa o índice e a descompressão já confirmados em [02-file-formats.md](02-file-formats.md).
Não modifica nem distribui os recursos originais.

## Evidência nos recursos

- `j\title\title_1_e.bmp`: BMP de 800×600, 24 bits, BI_RGB.
- `j\title\menu_e_ani0.bmp` até `ani6`: sete BMPs de 400×250,
  cada qual contendo os cinco botões em faixas de 400×50.
- `j\title\title.txt`: declara `menu_?_ani0.bmp 200320`;
  a leitura como coordenadas (200,320) encaixa a imagem no fundo de 800×600.
- `menu_e_d_ani*.bmp` contém o submenu de duelo, **não** botões desabilitados.

Esses fatos são verificáveis lendo os arquivos do arquivo DAT. A dimensão do
fundo sustenta usar 800×600 como tela lógica; ainda não demonstramos que o
executável só admite essa resolução. Não houve observação dinâmica nesta sessão.

## Implementação e limites

`Joey.Core/Graphics.cs` valida cabeçalhos BMP e interpreta apenas coordenadas
explícitas de seis dígitos. Raylib decodifica os bytes em memória. Texturas são
carregadas sob demanda e liberadas antes de fechar o contexto gráfico.
Nenhum BMP é extraído permanentemente.

O menu usa o fundo e as dimensões originais. Usar `ani0` em repouso e `ani6`
no hover é uma escolha temporária: o temporizador e a sequência de animação
original ainda não foram reconstruídos. Deck Construction e Options recebem
um escurecimento local e ficam desabilitados. A região clicável inclui a faixa
inteira de cada botão, inclusive seus pequenos espaços decorativos.

Teste sem janela: `dotnet run --project src/Joey.App -- --check-graphics`.
Esse modo também exercita o decodificador BMP nativo do Raylib, sem criar GPU
ou janela. O teste de interação e apresentação no Windows segue pendente.
