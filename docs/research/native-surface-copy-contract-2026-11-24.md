# V17 / V30 — infraestrutura de superfície e blit opaco

Fonte privada: joey-knowledge-visual-cumulative-2026-11-24-v2.zip,
SQLite SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
Consultadas decompilations, pointers, refs e visual_evidence_memory.
As fórmulas abaixo são **decompilação e cruzamento de rotinas**,
não assembly novo: nenhum byte de código novo do EXE foi extraído.
Não afirmar equivalência de pixels sem comparação.

## Vtable da superfície scratch de Vija

004580F0 chama 004A9CC0; este construtor grava vtable 005DC6EC
(ref 004A9D07, também 004A9DB0 em 004A9DD1). O índice esparso
pointers tem 005DC6EC -> 004A9D90, 005DC734 (+48) -> 0041AB90,
005DC740 (+54) -> 004AA4B0, 005DC748 (+5C) -> 004A9E80,
005DC754 (+68) -> 004ABDD0. O último chama virtual +E0
com dimensões e flag, seguido de 004AC110 / 004AC1B0.
Vija cria a scratch com flag 1. Ainda não deduzir formato
de pixels ativo a partir dessa flag.

O alvo de +98 (005DC784) NÃO está em pointers/refs nem
coberto pelos 24 chunks de visual_evidence_memory. Não
é seguro escolher um dos muitos blits por similaridade
do pseudocódigo. 004584C0 já foi coletada; não repetir.
Roteiro 24 lê 005DC6EC:228 (até +E0 inclusive) e confirma
00547180 / 004ABDD0 e entrada de 004AD9C0. Depois resolver
os VAs de +98, +4C, +48, +E0 pelo resultado; se a função
já constar no banco, não exportar novamente.
A composição da máscara e as opções de cor/canal de
004589C8 continuam sem implementação. O roteiro 24 não
promete recuperar automaticamente código de alvos desconhecidos.

## Cópia opaca — 004AD9C0 / 00547180

004584C0 usa 004AD9C0 diretamente para as duas cópias
do fundo (key4) na scratch, não +98. Essa rotina já é
decompilada. Despacha por formato em surface+74h, usando
dados de pixels +10h, pitch +14h e clipping 00547180.

| Código | Layout lógico recuperado |
| --- | --- |
| 3 | RGB565 little-endian |
| 4 | RGB555 little-endian |
| 5 | B,G,R (3 bytes) |
| 7 | B,G,R,X (4 bytes) |

004C7600/004C7740 convertem 565/555; 004C7640/004C7780
expandem canais com shifts/máscaras, **sem replicação de
bits** (branco 565 -> 248,252,248; branco 555 ->
248,248,248). 004B50C0/004B5500 confirmam quantização
BGR24 para 565/555. 004B5FE0 coloca X=0 no destino de
32 bits; 004B89F0 copia apenas BGR de 32 para 24.
004BA020 copia dword inteiro quando formatos compatíveis;
o byte X não deve ser interpretado automaticamente
como alfa. 004BFD00/004C0130 confirmam 32 -> 565/555.
Os demais formatos (6,8,10..13 e variantes) não foram
implementados. Não confundir o tipo 7 com YGA source alpha.

00547180 tem caminho direto se largura E altura coincidem
e outro se alguma dimensão difere. Para cada eixo escalado:
S = extensão de origem, D = extensão destino, k = clipping
no início do destino, A=-D+k*S. Se A>0, deslocar cursor
por q=A/D+1 e A-=q*D; depois A-=1. Passo base S/D,
resto S%D. Antes de copiar cada pixel, somar resto;
se A>=0, avançar cursor e subtrair D. Copiar pixel,
avançar cursor por passo base. Este detalhe difere de
floor(i*S/D) e do filtro de ponto padrão da GPU.
Exemplos 5->3: [0,2,3], 3->5: [0,1,1,2,2].
No caminho misto, ambos eixos seguem recorrência,
inclusive o eixo de dimensão igual e início cortado;
preservar a expressão recuperada, confirmar assembly
do caso de fronteira antes de rotular pixel-parity.

NativeSurfaceCopy é implementação de CPU independente
de Core/raylib, com pitch positivo, retângulos fonte
válidos, clipping destino e conversões de quatro formatos.
Usa intermediários de 64 bits e valida memória, sem
reproduzir overflow/endereços inválidos do x86. Não
implementa flip, sobreposição in-place, alpha, color key,
+98 ou alocação nativa. Os testes exercitam conversões,
clipping, padding, caminho direto/misto e recorrência.
A enumeração independente em Python conferiu limites de
amostras para S,D=1..64 e todos os skips. Não é captura
nativa. Aplicação de Vija permanece plano de composição
até resolver máscara/remap/format; não ligar novo gate.

## Reuso

O mesmo primitivo pode servir às cópias de bancos
de outros efeitos e à composição de Vija quando o
formato estiver resolvido. O pipeline do jogo permanece
inalterado. A03 continua responsável por scheduler,
snapshots e tempo de vida. Cobertura 20/34.


## V17 complemento — mistura por intensidade, tabelas de cor

SQLite v2 reaberto read-only, SHA acima confirmado e quick_check=ok.
00546D70 (chamador 004A8370 em 004A857C) inicializa quatro
famílias de lookup; nenhuma instrução do inicializador está
no snapshot. Cruzamento de decompilação do produtor e dos
consumidores 00431750 (formato 3), 00432450 (4), 00433150 (5)
e 00434B40 (7), despachados por 00422E30 quando formatos
coincidem e intensidade signed <=255. Não é confirmação de
assembly, nem identificação da vtable +98.

- 00A2EE04[256*c+i] = floor(i*c/255), 005F6E04[256*c+i] =
  floor(i*(255-c)/255); aqui c e i são índices 0..255.
- 0060EF03[512*i+d] para -255<=d<=255: d*i/255 truncado
  em direção a zero, armazenado como byte modular. O
  inicializador preenche positivos, depois nega os bytes
  positivos para os negativos. BGR24 usa dst+lookup(src-dst),
  com chave comparada nos 24 bits baixos (00433150).
  Não é floor(src*i/255)+floor(dst*(255-i)/255), nem
  aritmética em sRGB linearizado.
- 0062EE04 (16*65536 words): RGB565, escala cada canal
  packed separadamente por q/15 com truncamento. 0082EE04
  (16*65536 words): RGB555, mesma escala de 5 bits por
  canal, duplicação para bit 15, bit extra removido no
  valor escalado. Consumidores usam q=(i&0xf0)>>4 e
  somam scale(src,q)+scale(dst,15-q), em ushort.
  Pulam se src==key ou src==dst; este último evita
  escurecer um pixel igual devido a dois truncamentos.
- 00434B40 (7, BGRX32) compara dword inteiro com chave;
  aplica o mesmo delta BGR e escreve quarto byte zero
  até com intensidade 0 se houver store. Não é RGBA.
  A variante formato 6 em 00433E50 aparece inconsistente
  na decompilação (perde intensidade/stride); não portá-la.
- 00422E30 em intensidade >=256 delega a 0051EBD0,
  que tem contratos próprios; não tratar 256 como
  saturação do kernel de intensidade. Modelar só 0..255.

NativeIntensityPixel porta esses kernels de mesma cor
em CPU, sem alocar tabelas de vários MB e sem aplicar
gamma ou alpha do raylib. Produto/inverso, delta
signed e escala 16-bit verificados em testes sintéticos
(tabelas completas, chaves, limites e byte de storage).
Não altera NativeSurfaceCopy nem assume equivalência
entre blit retangular e raster quadrilateral. Não porta
cross-format, source-alpha, máscaras, clipping/edges
do polígono, scratch, nem liga ao App. Roteiro 25
inclui agora 00546D70 para confirmação de assembly
do inicializador; não pedir dump das tabelas reconstruídas.
Roteiro 24 continua dono da vtable desconhecida.
Cobertura permanece 20/34.


## V17 complemento — source alpha nos formatos 12/13

Consulta direta read-only do SQLite v2 (SHA256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok nesta sessão). 00422E30, 00438100, 00438E30,
0051EBD0 e 00530220 têm decompilação; não há instruções
dos dois kernels de intensidade no snapshot. Não rotular
como confirmado em assembly.

O dispatcher 00422E30, quando formatos de origem/destino
coincidem, escolhe 00438100 para 12 e 00438E30 para 13.
Em ambos, o loop de pixels compara apenas os 24 bits baixos
da cor-chave; o quarto byte da origem é um multiplicador.
As rotinas são equivalentes em matemática, embora usem
ponteiros diferentes para os canais. Cada store:
  effective = floor(source[3]*globalIntensity/255)
  destination[c] += trunc((source[c]-destination[c])*effective/255)
  destination[3] = 255
A primeira operação usa 00A2EE04, a segunda 0060EF03.
GlobalIntensity está no intervalo 0..255 do adapter.
Não há skip separado para alpha zero ou intensidade zero:
em ambos, RGB fica igual mas o quarto byte vira 255.
Cor-chave é a única condição de skip de pixel nesse ramo.

Para raw intensity >=256, 00422E30 delega 0051EBD0,
que para formatos iguais 12/13 usa 00530220. Seu loop
compara a chave RGB e copia o dword completo; não usa
a tabela de blend nem testa alpha zero. Assim 255 e 256
são semanticamente distintos, não valores equivalentes.
A distinção dos formatos 12 e 13 no carregamento e na
alocação de superfícies não foi recuperada. O código não
adiciona estes formatos ao NativeCpuSurface, nem confunde
o quarto byte do formato 7 (BGRX32) com alpha.

NativeSourceAlphaPixel porta somente essa fatia de pixel;
os testes percorrem 65536 combinações alpha/global e
testam store/skip, truncamento, 255/256, zero alpha,
byte de storage e rejeições. Nenhuma promessa de composição
Porter-Duff, gamma ou equivalência com GPU/raylib. Nenhum
renderer, clipping, cross-format, associação de YGA a um
desses códigos ou máscara de Vija foi introduzido.
004365C0/00437360 (10/11) permanecem pendentes: o
decompilador apresenta expressões de indexação packed
e stride que exigem assembly. 00439B60 é outro helper
de 565 com quantização; não presumir o mesmo algoritmo.

Roteiro 25 ampliado com janelas 00438CD4, 00439A04,
00530DFE (loops), sem pedir dumps de tabelas já
reconstruídas nem repetir outro Draw. Se roteiro 25
já foi executado, coletar somente esses três alvos/janelas.
Cobertura Draw inalterada: 20/34.


## V17 complemento — ordem de canais, carga alpha e cor-chave (24/11)

SQLite privado v2 extraído e aberto; SHA256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4
e PRAGMA quick_check=ok conferidos. Consultadas decompilações
004A6120, 004AA660, 004AA8D0, 004AB460, 004AB950, 004AD0E0,
004BA020, 004AD580, 004ABA00 e 004AC000. Evidência de
decompilação, não assembly novo. O upload de superfície
está separado da interpretação dos arquivos YGA.

004AC000 grava em surface+74h o resultado de 004A6120,
que lê os masks de uma superfície DirectDraw. 8 bits ->2;
16 com F800/07E0/001F ->3 (565), 7C00/03E0/001F ->4
(555); 24 bits BGR/RGB ->5/6; 32 bits BGRX/RGBX ->7/8.
Outros layouts ->1 (falha de ponteiro ->FFFFFFFF).
O adapter recebe masks válidos, não tenta simular COM.
Esta rotina não escolhe formatos alpha 10..13.

004AB460 carrega a imagem para objeto intermediário e chama
virtual +E0 para alocar, depois examina +74h. As entradas
dos conversores são pixels de quatro bytes B,G,R,A
(confirmados pela expressão packed de 004AB950).
Os destinos têm stride de quatro bytes por pixel:
- 10: 004AB950 escreve RGB565 little-endian nos bytes 0/1,
  alpha>>4 no byte 2. Não escreve byte 3.
- 11: 004AD0E0 escreve RGB555 little-endian nos bytes 0/1,
  alpha>>4 no byte 2. Não escreve byte 3.
- 12: 004BA020 copia dword, BGRA.
- 13: 004AD580 inverte canais 0 e 2, preserva verde/alpha,
  RGBA.
004ABA00 aloca formatos 10..13 com pitch W*4 e flag
+78h=true. Não adivinhar byte 3 de 10/11: o helper
preserva armazenamento fornecido. O inicializador pode
preencher outros valores, e a política real do allocator
ainda não está confirmada. Os kernels same-format do PR
127 são independentes da ordem de canais: os nomes das
variáveis eram BGR, mas os índices em 12 e 13 permanecem
corretos. Não chamar 13 de BGRA.

004AA8D0 recebe COLORREF 00BBGGRR e converte a chave:
3/10 packed565, 4/11 packed555, 5/7/12 troca R/B,
6/8/13 preserva low24, 2 retorna raw. FFFFFFFF é
sentinela de erro/ausência, formatos desconhecidos idem.
004AC1B0 grava raw +6Ch e chave convertida +70h.
Não confundir COLORREF com 0xRRGGBB nem com o dword
armazenado na imagem. Verde puro não distingue os
dois layouts; testes usam chave assimétrica.

NativeSurfaceColorLayout porta classificação, chave e
conversões de upload por pixel; testes sintéticos de
máscaras, 256 níveis de alpha por formato, quarto byte,
e integração com NativeSourceAlphaPixel. Não muda
NativeCpuSurface, o decoder YGA existente, nem o App.
O caminho 004AB460 é de carga de imagem intermediária;
a associação exata de arquivos YGA e o seletor +E0
ainda não estão provados. 00496760 escolhe vtables
por capacidades de CPU (00496560 CPUID/MMX), não por
extensão. Não interpretar essa seleção como formato.
Roteiro 24 ampliado com 004AB460/004AB950/004AD580/
004AA8D0 para confirmar as conversões e a ABI. Após
resolver +E0 pelo dump existente, consultar SQLite antes
de pedir código adicional. Roteiro 25 continua dono
dos kernels raster, não repetir. Nenhum novo Draw.
