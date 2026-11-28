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
usa F5/F6 para rodar ou pausar, F7 para avançar um Update/Draw,
F8 para reiniciar e F12 para gravar PNG/JSON.
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

## V20 complemento — passes primários da moeda

Reconsulta direta do SQLite v2 (SHA e quick_check acima, confirmados nesta sessão), instruções 00457520..00457D01. Não repetir bundle 16. Não usar ordem de arquivo como remap.

- 00457727..00457774: em 1<=local<256, divisor P do banco key0, frame=(6+trunc(local*(bit?5P:4P+trunc(P/2))/256))%P. A tabela literal de 14 entradas 00457754..004577E0 é 1,2,3,3,2,1,0,1,2,3,3,2,1,0 para sombra key1; usar dimensão/divisor próprio. Adapter aceita P=14 e sombra >=4 por segurança (não afirmar manifesto observado).
- 00457824..004578F5: sombra usa recorte, sem escala, offset trunc(bounce*25/256) em ambos os eixos; metade largura/altura próprias. Virtual +4C. 004578F8..004579E1: key0 frame selecionado, dimensões escaladas por S/256, origem X=centerX-H/2, Y=centerY-H/2+trunc(-50*bounce/256). Ambas usam metade ALTURA escalada, não largura. Virtual +4C. Ordem sombra e moeda.
- 004579ED..00457A22: fora do giro, nada quando local<=-128; -128<local<-64 usa 512+4local, caso restante usa 256, exceto global counter-end>-64: 4*(end-counter). Virtual +48. 00457AD6..00457B05 sombra key1 frame0 usa metade largura do frame em X/Y e +1 nos dois. 00457B08..00457BB3 key0 frame13 se local>0 e bit0, senão frame6. Âncora pela metade largura, sem +1. Não interpretar bit como cara/coroa.
- 00457BD2..00457CDB: halo key2 inteiro entre 256<=local<320, dimensões *(2local-256)/256 e argumento 4*(320-local). Origem central por largura e altura próprias; slot +48. Este argumento não é alpha Raylib confirmado.
- 00457CDE..00457D01: compara bit com objeto+1C e diferença global counter-end; se -320<=diff<-192 entra ramo scratch keys3/4; fora desse intervalo salta ao ramo key5, cujo loop ainda tem gates de visibilidade. NativeCoinPassLayout expõe apenas gates, não simula os passes omitidos. Resta mapear parâmetros/compositor +94/+A0 e partículas. O campo +1C não é resultado sorteado nem um flag booleano confirmado; adapter restringe ao domínio 0/1 recuperável. Não ligar ao duelo, não criar gate A03, não contar novo Draw.

Testes sintéticos usam dimensões assimétricas para detectar trocas de eixos, verificam recortes por bit, halo/fade, ausência pré-lançamento e defasagem. Sem DAT real nem comparação de frames. Coverage continua 20/34.


## V22 complemento — dispersão determinística da carta

SQLite v2 SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check ok nesta sessão. 00450FD0 tem assembly no bundle 16; os
callees 00558FB0, 00558BC0, 0055C020, 00558BF0, 005C3BF5 e 005C3C02
têm decompilação, não assembly indexado. Não repetir a coleta Draw.

**Tabela 00A52EB4:** 00558BC0 inicializa sob demanda, 0055C020 aloca
0x100 bytes e chama 00558BF0. Esta última chama 005C3BF5(0) (srand,
estado TLS+14h) e 256 vezes _rand (005C3C02). A implementação do CRT
usa state = (state*0x343FD+0x269EC3) mod 2^32 e retorna
(state>>16)&0x7FFF. Cada entrada armazena somente o byte baixo.
A tabela candidata deterministicamente reconstruída inicia
26 27 F6 85 97 15 AD 1D D2 94 DD C4 76 19 39 31 (hex),
SHA256 dos 256 bytes
62ad1265fa2adde0c900daee2cee11a9405a49573388877efbb410318968c854.
Não consumir RNG de cartas, UI/deck MT ou global C#; isto é
um gerador local para reproduzir a tabela, não uma alegação de que
o srand original não tenha efeitos colaterais na thread nativa.

**00558FB0:** parâmetro <1 retorna 0, >=256 usa blit direto para
modos conhecidos 0..3 (+0C,+08,+20,+4C). No domínio 1..255,
percorre linhas de 2 e colunas de 4 pixels, limitando retângulos
à imagem. Para bloco em (x,y), linha r=y/2, usa tabela
[(26*r+x)&255] e [(1+38*r+x)&255] separadamente; direção
(x-W/2,y-H/2), quadrado da distância. Há uma expressão x87
seguida de __ftol perdida pelo decompilador; se divisor !=0,
multiplica direção por 256 e divide com truncamento. Extensão
E=W+H-((W+H)*p>>8). Deslocamentos candidatos (noise*dir*E)>>16
com shift aritmético, preservando sinais e truncamentos.
A ordem de argumentos e o divisor precisam da janela assembly
para fechar a ABI; não assumir sqrt nem renderizar via alpha.

NativeBreakScatter porta a tabela, retângulos, fases e classificação
de domínio, e expõe projeção inteira com divisor **injetado**.
Testes sintéticos de hash, cortes de borda, orientação, índices,
shift negativo e centro. Uma miniatura 50x72 produz 468 blocos
(13 colunas x 36 linhas, última coluna de 2px); uma 72x50 produz 450.
Modelo não desenha partículas/carta e não substitui o gate de A03.
Roteiro 29 coleta callee 00558FB0 e gerador específico, sem pedir
a tabela runtime já reconstruível. Roteiro 24 continua dono da
vtable de superfície. Sem mudança da cobertura 20/34.


## V20.2 — revisão com recursos confirmados no SQLite v3

Nesta sessão foi aberto somente leitura o snapshot privado
joey-knowledge-visual-cumulative-2026-11-24-v3.zip, SQLite SHA256
9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed,
PRAGMA quick_check=ok e metadata executable_sha256 igual ao alvo.
Tabelas visual_dat_manifests e visual_dat_images confirmam
coin.txt e seis imagens sem erro. Não copiar o manifesto inteiro
nem arquivos gráficos para Git.

| Key por ordem do manifesto | Nome | Dimensões | Divisor |
|---|---|---|---|
| 0 | coin_rotate.bmp | 560x40 | 14 |
| 1 | coin_sha.yga | 168x42 | 4 |
| 2 | coin_ripple.yga | 64x64 | 1 |
| 3 | coin_mask.bmp | 40x40 | 1 |
| 4 | coin_ref.bmp | 69x40 | 1 |
| 5 | coin_kirari.yga | 56x14 | 4 |

O divisor 14 antes era uma hipótese restrita do adapter; agora
é dado do DAT. 00457366..00457385 em assembly divide a largura
do recurso pelo parâmetro e aloca scratch desse tamanho por
altura (40x40 no manifesto observado); flag 1 não identifica
o formato. 00457520 e NativeCoinPassLayout já recuperam 0/1/2.
A revisão usa ordem do manifesto como remap diagnóstico; a
vtable/compositor final não estão confirmados. Não preencher
reflexo e brilho por aproximação; gates constam do JSON mas
passes 3/4/5 não são desenhados. Key verde no BMP da moeda,
YGA pelo decoder existente e tint linear são diagnóstico,
não garantia de fidelidade. Nenhum gate, RNG de regras, áudio
automático ou integração do duelo.

    dotnet run --project src/Joey.App -- /path/to/data.dat --review-coin=3,5,400,100,1

COUNT 1/3; BITS decimal fornecido pelo operador (não sortear,
não nomear cara/coroa); X/Y origem lógica; HIGHLIGHT raw 0/1.
F7 Update+Sample, F8 reset, F12 PNG/JSON (inclui flags dos passes
omitidos, request sonoro e fonte do cosseno), Escape sai.
Cosseno segue loader/default capturado do Codex. Confirmar no
desktop; testes sintéticos exercitam parser, dimensões, as
timelines 96/128 e limites dos recortes. Sem alteração de
badge porque review isolado não conta como Draw do duelo.


## V20.3 — reflexo e cinco brilhos: assembly do Draw

SQLite v3 SHA256 9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed,
quick_check=ok nesta sessão. Releitura de visual_evidence_instructions
00457520..004580DA (deduplicada por VA/instrução), cotejada com
visual_evidence_functions e dimensões/manifesto. Nenhuma coleta nova.

Correção de V20: 00457CE7 salta todos os extras se bit != +1C.
A reflexão ocorre em -320<=diff<-192, diff=counter-end.
00457EAE recarrega diff e cai em 00457EB2, mesmo após refletir;
não há exclusão mútua. 00457EB2..004580A2 sempre percorre os
cinco brilhos se chegou ao ramo de extras (fora do giro e
de local<=-128). O boolean SparkleBranchReached só indica
entrada no loop, não visibilidade.

Reflexo: 00457D07..00457DA1 obtém dimensões key3/4.
00457DA9 chama 004B1160(scratch,0), cuja decompilação indica
preenchimento com chave corrente +70h e retângulo default.
00457DE2..00457E1F copia key4 opacamente via 004AD9C0, em
scratch+0B4h, x=trunc((maskW+refW)*(640+2*diff)/256)-refW,
y=0, demais parâmetros zero. DAT: scratch 40x40 (quadro
da moeda), mask 40x40, reflexo 69x40. x=-69 em diff=-320;
x=-15 em -256; x=33 em -200. 00457E4D..00457E7C chama
scratch virtual +94 com key3 e argumentos (0,255,0,255,0).
00457E82..00457EA8 compõe scratch via destino +A0 em
(cx-maskW/2,cy-maskW/2), mais três zeros. Não atribuir alpha,
blend ou máscara RGB aos argumentos sem resolver os slots.
NativeCoinReflection preserva esse plano; ainda não desenha.

Brilhos: 00457EB2..00457ED0 inicia t=diff+256, j=0; em cada
iteração t-=16, j++, fase+=3, até fase=15. Desenha apenas
0<=t<64. 00457F2B..00457FB3 tabela [3,2,1,0,0,1,2,3],
indexada por t/8. key5: 56x14, quatro quadros 14x14.
00457FE3..00458079: recuperado rastreando deslocamentos
ESP após pushes. x=cx+j*faceW/4-sparkW/2-faceW/2;
y=cy+((3*j+2*coinIndex)%8)*faceH/8-sparkH/2-faceH/2.
Virtual +4C, rect fonte, destino-size null, flag zero.
As dimensões são do quadro original, não da moeda escalada.
Todos usam clock global; posição Y difere por índice.
A revisão desenha key5 com compositor raylib diagnóstico,
não substitui a reflexão por sprite inventado. F12 schema 2
inclui extras, plano e passes. Testes de overlap, fronteiras,
seletores, índices e âncoras. Roteiro 24 já inclui vtable
005DC6EC (até +E0), portanto abrange +94/+A0 se esta for
a vtable da scratch; não solicitar coleta redundante.
Sem integração de duelo, sem novo Draw/coverage.
