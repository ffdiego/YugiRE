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
Na lista, título, Back, fundo, cartas, moldura, setas e números de página usam
os recursos originais. Busca, tipografia da descrição e rolagem são locais.
No duelo, fundo e ilustrações são
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

## Grade e ordem da Card List

O usuário confirmou no original 10 colunas × 5 linhas e ordem alfabética.
Relatou também as 350 cartas inicialmente disponíveis e a integração com saves
das outras versões; o filtro de disponibilidade continua fora desta etapa.

A análise estática confirmou em `0x0047DE80` os passos de **55×87 pixels**,
os limites de 10 colunas/5 linhas e o índice `coluna + linha * 10`.
`0x0047E750` desenha a grade e a moldura deslocada -4 pixels em cada eixo.
Usamos miniaturas de 50×72, inferidas da moldura de 58×80 com essa margem.
O início (225,110), as setas (408,565)/(549,565) e os centros dos números
(476,576)/(530,576) vêm de `list_scene.txt`. `0x00480690` centraliza os números.
O verde RGB(0,255,0) desses sprites é tratado como transparência; a remoção
foi conferida pelo decodificador em memória, sem janela.

`0x0047DBB0` ordena com o comparador `0x0047DCB0`, que subtrai os valores
retornados por `0x00402540` para os índices das cartas. A aplicação usa os
uint16 de `bin#\card_sorteng.bin` por índice, preservando IDs e variantes.
Registros sem nome são mantidos **no fim**, uma política do demo sem desbloqueios.
Hover mostra a carta imediatamente; o atraso nativo de quatro atualizações,
as transições de página e o efeito de destaque das setas não foram reproduzidos.

Evidência: [consulta Ghidra](evidence/card-list-ui-ghidra.txt). Reprodução:

```bash
bash scripts/run-archive-query.sh card-list-ui-ghidra.txt tempfunc:0047de80 tempfunc:0047e750 tempfunc:0047ed50 tempfunc:0047dcb0 00402540 tempfunc:00480690
```

Validação da grade: build sem avisos/erros, **83 verificações C#**, 25 testes
Python e **789 BMPs** decodificados. Inclui limites e espaços da grade,
paginação vazia/exata/parcial, ordenação por índice e posições dos sprites.
O teste visual desta alteração permanece pendente no desktop do usuário.

## Cartas de cada versão e registros fora do catálogo

`0x004024E0` carrega `CARD_Pack.bin` e retorna o uint16 pelo índice interno.
`0x0047DBB0` só inclui a carta se esse valor tiver bits em comum com a máscara
da versão. `0x005BEA40` retorna um bit de versão; `0x005BEA60` retorna todos os
bits até a versão atual. [Evidência direta](evidence/card-list-pools-ghidra.txt).

No DAT analisado, os bits 1/2/4 selecionam 155/315/350 registros. A união é
**771**, pois os grupos se sobrepõem: há 4 repetições entre os dois primeiros
e 45 cartas do terceiro já presentes nos anteriores. A soma 820 conta essas
49 ocorrências novamente. Restam 343 registros sem associação a esses grupos:
340 apontam para `token_sl.bmp` e três para outros `token_*.bmp`.
Isso os exclui do catálogo colecionável; não prova que sejam todos inválidos
para usos internos do motor, como fichas.

A lista agora aplica a máscara 7 por padrão, preservando as 771 cartas sem
restrição de desbloqueio. `--include-unused` mantém a inspeção dos 1114
registros. A filtragem é por tabela, sem depender de nome ou imagem.
O registro do Windows e a disponibilidade do save continuam sem implementação.

Validação adicional: **89 verificações C#**, incluindo contagens individuais,
união sem duplicatas e exclusão dos placeholders pelo critério do original.
Os modos normal e `--include-unused` também passaram em `--check-data`.

## Filtros da Card List

All cards e Joey usam os sprites `e_allcards1/2.bmp` (120×69) e
`joey_only1/2.bmp` (127×70), nas posições (214,531) e (664,530) do manifesto.
O recurso `2` é o estado indisponível descrito em `list_scene.txt`; a interface
o usa para o filtro já selecionado. A máscara 7 mostra 771 cartas e a máscara
4 mostra as 350 do Joey, incluindo cartas compartilhadas com outras versões.
A busca é mantida ao trocar de grupo; página e seleção retornam ao início.
O destaque de hover ainda é uma tintura local. O modo sem desbloqueios e o
grupo inicial All cards são escolhas do protótipo.

Validação desta etapa: 111 verificações C# e 793 BMPs decodificados, incluindo
as quatro imagens dos filtros. A confirmação visual permanece pendente.
