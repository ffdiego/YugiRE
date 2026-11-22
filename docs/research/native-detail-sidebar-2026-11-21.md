# Painel lateral do duelo — bitmap original e texto GDI

Data: 21/11/2026. Implementação parcial, sem comparação de pixels com o executável original. Evidência: SQLite global joey-knowledge.sqlite com as funções 0041BE80, 0041B3E0, 0041C920, 0040F650, 00544B80 e 00545200; índice docs/evidence/data.dat-index.json.

## Evidência recuperada

O DAT contém quatro recursos em j\duel\detail\: detail.bmp, detail.txt, line.bmp e scroll_bar.bmp. O cliente já desenhava detail.bmp como pano de fundo, mas escondia três áreas desse sprite com retângulos Panel do raylib, dois na área de LP e um na descrição.

O construtor 0041BE80 inicializa campos [0x6f]=8, [0x70]=365, [0x71]=184 e [0x72]=526. O builder 0041B3E0 calcula a largura do filho de texto como [0x71]-[0x6f]: portanto a área recuperada é x=8, y=365, largura 176 (e NÃO 184), altura 161. 0041C920 posiciona um widget em (186,364); não significa que o BMP inteiro de scroll deva ser desenhado nessa origem sem inspecionar o manifesto.

0041B3E0 constrói uma sequência com nome da carta, atributos ATK/DEF e descrição, com tags de cor, negrito, quebra e linha horizontal (FONT, B, BR, HR). O renderer 00545200 usa CreateFontA e SetTextColor do GDI. O helper 00544B80 dispõe de Courier New (índice 0) e Arial (índice 1); a seleção do idioma não japonês em 0040F650 pode selecionar Courier New, com tamanho 14 em 00544B20. Os atlas j\duel\font\duel_font*.bmp são recursos numéricos e não substituem a fonte alfabética do painel.

A paleta observada no construtor inclui os valores COLORREF 0x101010, 0x1e49b4 e 0x0b586e; no cliente são convertidos corretamente da representação numérica BGR para RGB.

## Implementado

- Os três retângulos raylib sobrepostos ao detail.bmp foram removidos. As barras e dígitos de LP já baseados em recursos do DAT permanecem inalterados.
- NativeDetailPanel centraliza geometria da janela de texto, as três cores, quebra de parágrafos por largura medida e limites de rolagem; usa scissor para não invadir a imagem da carta e as regiões vizinhas.
- Fonte do painel exclusivamente: carrega Courier New regular e negrito a partir das fontes do próprio sistema operacional do usuário, depois da abertura da janela; no Linux de desenvolvimento utiliza alternativas Liberation Mono e DejaVu Sans Mono. Não empacota, distribui ou copia arquivos de fonte. Tema com fonte própria mantém a sua escolha. Sem fonte compatível, há fallback legado com aviso no console.
- 14px e 11 linhas por página na região recuperada. Nome, atributos e descrição são tratados como regiões de estilo distintas. A rolagem por mouse acontece apenas dentro do texto.
- Os BMPs line.bmp e scroll_bar.bmp passam a ser validados, mas não foram posicionados sem acesso à ordem e ao recorte do manifesto detail.txt.
- A captura F12 registra fonte, viewport, carta e scroll para comparação posterior.

## O que ainda não está confirmado

Rasterização GDI pixel a pixel contra a fonte carregada pelo raylib; posição e recorte exatos de line.bmp e scroll_bar.bmp; métrica do thumb do scrollbar; formatação HTML rica completa; geometria da ilustração ampliada (hoje 14,53,186,265); seleção de fontes em outros idiomas.

Este trabalho é uma melhoria parcial em uma família Draw já inventariada, não uma nova função verificada. Cobertura visual permanece 19/31 (61,29%), 0 Verified.

## Testes

LookAndFeelChecks verifica coordenadas nativas, largura 184−8=176, origem do widget em 186,364, 3 cores COLORREF, quebra por medição e limites de rolagem. CI .NET 10 e testes offline Python/Node. Revisão gráfica em Windows: iniciar a Alpha, comparar card detail vazio, monstro e magia com descrição longa, observar contornos do painel, LP, texturas e scroll.