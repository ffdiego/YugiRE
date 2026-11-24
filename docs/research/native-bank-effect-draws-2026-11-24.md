# V20–V22 — contrato Draw recuperado no bundle 16

Snapshot privado v2, SHA256 SQLite
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok, 21 bundles. Evidência bundle
9e0a8a6c7fe67b2aa7a5f5d0dcdb2c2f4e025f81831a461ad6de837e257cc75f
(retry-001-00450FD0, retry-003-00456B90, retry-005-00457520),
e janelas dos construtores/handlers. Consultadas instruções
visual_evidence_instructions por endereço, não só pseudocódigo.
Não repetir roteiro 16.

## V21 — dado: relógio, centro e passes

004569DA inicia -256, 00456B70 soma 8, ativo enquanto t<768
(128 Updates). 0040A6AB lê word B e 00456A12 grava +14:
índice fornecido pela lógica, não RNG visual. Suporte restrito 1..6.
00456B9B..00456C2D calcula upper=Y<300 em cada Draw, bounce
B=0 fora de [0,384), C((t&255)-128)>>7 em [0,256),
C(((2*t)&255)-128)>>9 em [256,384). Escala 256+B/2,
ou 1024+4t quando t<-192. Divisões inteiras truncam para zero.
00456C35..00456C96 para 0<t<=384 modifica +18/+1C:
n=(384-t)/8, novo X=525+(X-525)*n/(n+1); novo Y
=Yalvo+(Y-Yalvo)*n/(n+1), Yalvo 236 se upper, senão 364.
Entre 384 e 448 (inclusive 448), X=525 e Y=t-148 ou 748-t.
Esse é estado mutado no Draw; repetir Draw não equivale a
repetir amostra. Não avançar em CanComplete.

00456CCC..00456D63: antes de 448, key do dado=0 se t<=256,
depois =4,4,2,2,2,4 (índices 1..6). Para índice 6 lê ESP+54,
onde está 4, mesmo que o decompilador apresente tabela incompleta.
Sombra key+1, offsets de fase 8,4,8,0,4,0. Fase upper
=t/8+256+offset; caso contrário 255-t/8+offset. IDIV
pelo divisor de cada recurso fornece recorte (resto).
00456DDB..00456E62: sombra usa min(escala,256) para dimensões,
offsets X=B*25/256+escala*7/256, Y=B*25/256+escala*9/256,
cada divisão separada. 00456F91..00457059: dado usa escala
integral e Y=-B*50/256, subtrai metade da largura escalada
de X e Y (não metade da altura). Ordem sombra, dado.
0045705E..00457193: t>=448, key sombra 6, face índice+6.
Intensidade 256 até t=704, depois 4*(768-t).
Os dois passes usam a mesma origem = centro - largura da
face/2 em ambos os eixos, conforme pushes 0045710F..00457130
e 00457160..00457193; não centralizar cada textura separadamente.
Som 1000001C em Draw t=256 ou 384 (00457196..004571B5).
Não transformar a solicitação em áudio efetivamente tocado.

NativeDiceTimeline amostra dois passes e possui Draw explícito.
A revisão --review-dice=INDEX,X,Y, com --native-cosine=,
avança F7 Update+Draw, F9 Draw isolado, F8 reset, F12 PNG/JSON.
O adapter NativeDiceResources tenta a ordem do manifesto local
e exige divisores positivos, sombra decide e seis faces nos keys
esperados. Se a remap virtual ou o formato real for diferente,
falha explicitamente. Não temos o texto de dice.txt nesta
sessão; parser, cor-chave verde e blend Raylib diagnósticos,
não comparados ao DAT/Joey original. O modelo aceita lookup
fornecido pelo consumidor para não impor essa ordem no Core.
Não contar review como integração do fluxo normal.

## V20 — moeda

004572E2 inicia -128, 00457309..00457321 grava origem,
bits +18, count +14, outro parâmetro +1C. 0040A838 / 0040A976
passam count 1/3, 00457500 soma 8 e desativa em (count+4)*128.
96 ou 128 Updates. 004575BC..00457625 por moeda:
local=t-128*i, bit=(+18>>i)&1; bounce C((local&255)-128)>>7
só em 0<=local<256, escala 256+bounce/2. Solicita som
1000001D se local=0 no Draw. 00457639..0045770F:
origem antes de zero; interpolação linear inteira em 0..256;
depois destino (525+(100-100*count)/2+100*i,300).
Loop 004580B4..004580D3 soma 128 na defasagem e 100 em X.
NativeCoinTimeline porta este contrato. O recorte de moeda,
reflexo/máscara e brilho ainda exigem rastrear chamadas
virtuais específicas; não mapear Bit a cara/coroa só pelo nome.

## V22 — break

00450FB0: 0 até 256, +16 (16 Updates), ativo t<256.
00450FDF chave 0. 00451039..004510D9: cinco passagens;
radius=t/8, angle=floor(512*i/5), offsets
(C(angle)*radius)>>16 e (C(angle-128)*radius)>>16,
subtrai metade das dimensões do recurso; intensidade byte
255-t (não 256-t). Depois seleciona rotação
90*(bit14+2*(side XOR A54E5C&1)), chama 0041F130 para região 11
ou 0041F2E0 nas demais, dimensões próprias da imagem orientada.
0045116E..004511BB chama 00558FB0 com
256-(C(t/2-128)>>8), flag 0, valor 255, flag 0.
Essa função não é simples fade: divide a carta em blocos
4x2 e usa bytes da tabela apontada por 00A52EB4, inicializada
sob demanda por 00558BC0/0055C020. Falta a ABI/assembly da
distorção e seus dados vivos para renderização fiel. O modelo
porta partículas e parâmetros de orientação/distorção,
não substitui todos os efeitos Destroy nem oculta a carta.
Ações de banimento/movimentação devem fornecer snapshot
adequado; A03 continua dono do gate e da vida útil do efeito.

Testes sintéticos verificam limites, todos os índices e lados,
recortes, fade, repetição de Draw, âncoras, bits/defasagem e
partículas. Coverage 20/34, nenhum novo Verified; o dado
tem renderer de revisão, ainda não disparado no duelo.
