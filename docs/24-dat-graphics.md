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
- `j\list\list_bg.bmp`: 800×600. `list_scene.txt` posiciona o título em
  (205,0), Back em (692,0) e o início da lista em (225,110).
  `list_detail.txt` indica a carta em (6,87) e o texto entre (16,393) e (180,592).
- `j\duel\field\fie_normal.bmp`: 570×600; a aplicação o coloca em (230,0).
  Essa posição e as zonas clicáveis atuais são decisões locais de layout.
- `card\list_card.txt` associa índices e IDs aos BMPs de 200×290. Exemplos:
  índice 816, ID 0 → `LOB001.bmp` (Blue-Eyes); índice 197, ID 762 →
  `PSV003.bmp` (Parasite). As 1114 associações conferem com `card_id.bin`.
  Vários registros usam imagens compartilhadas `token_*.bmp` no arquivo original.

Esses fatos são verificáveis lendo os arquivos do arquivo DAT. A dimensão do
fundo sustenta usar 800×600 como tela lógica; ainda não demonstramos que o
executável só admite essa resolução. Não houve observação dinâmica nesta sessão.

## Implementação e limites

`Joey.Core/Graphics.cs` valida cabeçalhos BMP e interpreta apenas coordenadas
explícitas de seis dígitos. Raylib decodifica os bytes em memória. Texturas são
carregadas sob demanda e liberadas antes de fechar o contexto gráfico.
Nenhum BMP é extraído permanentemente.
O cache de texturas é reduzido a 64 entradas entre quadros, podendo crescer
durante o quadro atual; descarregar texturas só entre quadros evita invalidar
imagens ainda usadas pelo lote de desenho. Não se carrega o catálogo inteiro na GPU.

O menu usa o fundo e as dimensões originais. Usar `ani0` em repouso e `ani6`
no hover é uma escolha temporária: o temporizador e a sequência de animação
original ainda não foram reconstruídos. Deck Construction e Options recebem
um escurecimento local e ficam desabilitados. A região clicável inclui a faixa
inteira de cada botão, inclusive seus pequenos espaços decorativos.
Na lista, título, Back, fundo e carta usam os recursos originais. Busca,
paginação, tipografia e rolagem são locais. No duelo, fundo e ilustrações são
originais; controles, posições e apresentação de ataque/defesa continuam de
demonstração. Não há reprodução das animações YGA ou de todo o renderizador.

Teste sem janela: `dotnet run --project src/Joey.App -- --check-graphics`.
Esse modo também exercita o decodificador BMP nativo do Raylib, sem criar GPU
ou janela. O teste de interação e apresentação no Windows segue pendente.

Validação desta etapa: build sem avisos/erros, 71 verificações C#, 25 testes
Python existentes e 785 BMPs decodificados pelo Raylib em memória. Os hashes
de `data.dat` e `joey_pc.exe` permaneceram iguais aos registrados. A sessão
SSH não tem DISPLAY/WAYLAND_DISPLAY; a janela, hover, redimensionamento e
interação das três telas precisam ser conferidos no desktop.
