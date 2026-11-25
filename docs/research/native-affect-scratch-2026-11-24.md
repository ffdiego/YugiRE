# V24 — scratch e amostragem de affect.txt

O roteiro 17 confirma o recorte do fundo em `0040E040` e o gatilho de
recomposição em `0040E1F0`. O manifesto e as dimensões foram cruzados
com `visual_dat_manifests` e `visual_dat_images` no snapshot privado v3.
O roteamento de foco e vínculos está no [contrato V23/V24](native-chain-affect-contract-2026-11-24.md).

`0040DBF8` consulta a entrada 1 de `affect.txt` e `0040DC4C` cria
o scratch no tamanho do ícone consultado, 50×50. A entrada 0 é
`loop_blue.bmp`, 100×69. As cinco entradas de ícone preservam a ordem
do manifesto. O virtual de lookup `0043A5D0` mantém o índice recebido.

`0040E0AB–0040E0E6` calcula a origem vertical do recorte como
`(contador/8) % alturaDoFundo`. O fim é
`(origem + alturaDoScratch) % alturaDoFundo`. Se o fim for menor que a
origem, copia primeiro [0,fim) na linha `alturaDoFundo−origem` do
scratch (`0040E145`) e depois [origem,alturaDoFundo) na linha zero
(`0040E191`). Sem wrap, só [origem,fim) vai para a linha zero.
As duas cópias partem de X=0 com largura igual à do scratch.

`0040E1F8–0040E209` recompõe apenas quando o seletor difere do último
desenhado. A troca de foco reseta o contador, mas o scratch segue
reutilizado se o seletor não mudar. Se o mesmo Draw solicita dois
seletores distintos, há duas recomposições. O passo final conserva o
centro e a intensidade assinada em `0040E252`.

`0040E1DF` passa o ícone selecionado ao slot +94 do scratch com
argumentos 0,255,0,255,0. O roteiro 24 confirma a vtable `005DC6EC`
e permite seguir `0041AC90` para `004CCD40`. O caso fonte tipo 3
e destino tipo 10 usa `004ED5E0` para derivar a máscara a partir
dos cinco bits baixos da fonte e da tabela de remapeamento. A conversão
dos outros formatos e o uso dessa máscara na composição final seguem
pendentes. O plano C# representa a ordem dos passes e os retângulos,
mantendo a rasterização e a seleção completa de vínculos fora da entrega.
Nenhuma Draw foi acrescentada à cobertura.
