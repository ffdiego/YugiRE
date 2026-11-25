# V17/V07 — geometria de quadriláteros e fronteira de rasterização

Fonte privada SQLite cumulativo v2, read-only, quick_check=ok;
SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
Consultadas decompilations, refs, visual_evidence_functions e
visual_evidence_instructions (inclusive por intervalo de endereço).
00480C60 possui assembly recebido (bundle 23), chama 004223F0.
Os callee 004223F0, 00422540, 00422840, 00422E30 e 0051EBD0
estão em decompilations, mas não há assembly indexado nessas VAs.
Não confundir o assembly do chamador com confirmação do callee.

## Geometria

004223F0 consulta dimensões por virtual +38, monta quatro
vértices fonte inclusivos (0,0),(w-1,0),(w-1,h-1),(0,h-1)
e destina 4 pontos a 0051EBD0 (xref 00422532).
O decompilador perde a origem dos registradores EBX/EBP e
argumentos. Leitura candidata, consistente com a geometria:
w=largura, h=altura, C(i)=tabela 00A52EE8[i&511].
Para os ângulos assinados -128..128 de V07:
W=trunc(w*C(a)/65536), S=C(a-128);
HR=MulDiv(h,65536,65536+trunc(S/2));
HL=MulDiv(h,65536,65536-trunc(S/2)).
MulDiv arredonda ao inteiro mais próximo (operandos positivos).
Xl=cx-trunc(W/2), Xr=cx+W-trunc(W/2)-1;
Ylt=cy-trunc(HL/2), Ylb=Ylt+HL-1;
Yrt=cy-trunc(HR/2), Yrb=Yrt+HR-1.
Ângulo zero: largura e altura originais; sinais opostos
trocam alturas dos lados. A 128, W=0 se C(128)=0:
o modelo conserva endpoints e sinaliza degeneração, sem
inventar largura mínima nem decidir política do rasterizador.

NativeNoticeProjection é modelo isolado, não usado no Draw
normal; a origem EBX/EBP das dimensões ainda exige assembly.
Testes sintéticos com dimensões 395x65 e tabela cardinal/oblíqua
testam endpoints, simetria, MulDiv, degeneração e ambos os lados.
Não são validação de pixels nem da tabela viva V01.

## Família compartilhada

Xrefs confirmados para 0051EBD0:
004223F0 (00422532), 00422540 (004226A6),
00422840 (00422A02), 00422E30 (00422E80).
00422540 tem ramos 90/180/270 e escala MulDiv; 00422840
usa trigonometria fixa e recorte fonte. Não confundir
essas transformações com a deformação trapezoidal do aviso.

0051EBD0 bloqueia duas superfícies via 004AC200; despacha
por formato em +74h de ambas. O caminho de formatos iguais
consulta virtual +74 da fonte e chama, entre outros,
0052C230 (3/4), 0052CF10 (5), 0052DBD0 (6),
0052E890 (7/8), 0052F570 (10/11), 00530220 (12/13).
Não atribuir significado de alpha aos bytes ainda não mapeados.
00422E30 delega diretamente a 0051EBD0 se intensidade>255;
0..255 segue família própria, por exemplo 00433150 para 5
e 00434B40 para 7. NativeQuadRasterDispatch porta apenas essa
fronteira para intensidades não negativas. Intensidade zero
não é automaticamente no-op e 256 não significa ignorar chave.
A composição, clipping, regra de cobertura das arestas e
interpolação não foram portadas.

Roteiro 25: assembly da geometria, dispatch e amostra BGR24;
não repete Draw 00480C60 nem substitui roteiro 24 (+98 de scratch).
Quando recebido, consultar os kernels em função do formato
real da superfície antes de expandir a coleta. Nenhuma alteração
ao Core, scheduler ou badge (20/34).


## V17.3 — scanline em ponto fixo e raster CPU BGR24 (candidato)

Nesta sessão: ZIP v2 extraído em diretório temporário, SHA256 SQLite
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
PRAGMA quick_check=ok. Releitura direta de 0052CF10 (16377 caracteres)
e 00433150 (16534), e dispatch 0051EBD0. Consulta de instructions
por intervalo 0052CF10..0052DBD0 e 00433150..00433E50 retorna zero;
as fórmulas são decompilação, não novo assembly. Não repetir roteiro 25:
já contém ambos os kernels; confirmar seus loops com a coleta existente.

A rotina escolhe topo pelo menor Y e, em empate, menor X; fundo
pelo maior Y e maior X. Produto cruzado determina sentido das
duas cadeias; pula arestas horizontais de topo/fundo; duplica
último segmento para a última linha. Para quad estritamente
convexo, a implementação equivalente percorre duas listas de
arestas monotônicas, troca segmento no Y do vértice intermediário.
Não generaliza polígonos côncavos, colineares ou quads degenerados.

F=0x100000, H=0x80000. Para aresta A->B de dy>0 e linha y:
 X = Ax*F+H+trunc((Bx-Ax)*F/dy)*(y-Ay)
 U = Au*F+H+trunc((Bu-Au)*F/(dy+flag))*(y-Ay)
 V análogo. flag=(param_9 !=0) no kernel base; não afirmar
 semântica do chamador. A geometria não recebe flag. Top/bottom
 inclusive, respeitando clip half-open. Vertical clip inicializa
 acumuladores em clipTop-1, depois soma passo; para domínio sem
 overflow, avaliar diretamente no Y atual produz mesmo valor.

Por linha: L=Xl>>20, R=Xr>>20, U0=Ul>>20, V0=Vl>>20.
Se R==L, dU=dV=0. Caso contrário
 dU=trunc(((Ur>>20)-U0)*F/(R-L+flag))
 dV=trunc(((Vr>>20)-V0)*F/(R-L+flag)).
O loop usa U0+(offsetU>>20), V0+(offsetV>>20), offsets começam
 em zero, depois somam passo. Não aplicar meia unidade no passo
 horizontal; valores negativos usam shift aritmético.
Clip Lf=left*F+H, Rf=right*F-H; rejeita se Xl>Rf ou Xr<Lf.
Se Xl<Lf, cursor destino=left, skip=(Lf-Xl)>>20. Este skip pode
 diferir de left-(Xl>>20); preservar. Se Xr>Rf, R=right-1.

NativeQuadScanlines usa somente quads convexos cíclicos de quatro
pontos e domínio limitado de coordenadas, intermediários long para
não reproduzir endereços/overflow do x86. NativeBgr24QuadRaster
combina o sampler com o kernel de intensidade BGR24 e caminho
base keyed copy. Prevalida todos os samples para impedir leitura
fora da textura e store parcial. Testes sintéticos cobrem ambos
sentidos, flag 0/1, clip subpixel, troca de aresta, trapezoide,
pitch, cor-chave, intensidade e rejeição. Não integrar ao App,
não afirmar equivalência de pixels até assembly + comparação.
Sem suporte a outros formatos, vtables, máscaras, alpha por
pixel neste raster, perspectiva com divisão por profundidade,
ou integração de A03. Sem mudança na cobertura Draw.
