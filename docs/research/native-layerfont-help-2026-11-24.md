# V27/V28/V29 — Layerfont e ajuda

SQLite joey-knowledge-visual-decoded.sqlite, read-only, quick_check=ok.
SHA256 EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Layerfont Draws: decompilações transitórias do bundle
e589a5263234a28d0e93034d2d54abde536a784698fd592b6001ee7eef6414f7,
retry-012-00455FF0.txt e retry-013-00456360.txt.
Não há novo assembly nesta fatia. Cruzamento com construtores
00455D90/00456100 e chamador 00404580, todos persistentes.

## V27 e V28

005B9470 case 10h → 00404580 cria os objetos de Layerfont
no estágio zero, depois de 004516A0. 0044B990 retorna true
para DAT_005F3294=5 ou 7 (A); 0044B980 para modo=3 (B).
Não confundir o botão StartDemo do sandbox com modo nativo 5/7.
0044BEE0 recebe o modo na inicialização do duelo. Ambos carregam
layerfont.txt, usam Update vazio 004481F0; Draw altera estado.

A: contador inicial 0, incremento 4 módulo 256, antes de desenhar.
Recurso lookup 0. Intensidade C(2t-128)>>8 para t<64,
256 para 64<=t<=192, C(2t+128)>>8 para t>192.
Âncora (788,588) menos largura/altura; virtual +48.
B: dois campos inicialmente zero; Draw avalia 005BDAD0 !=0
e 0044BA10==0, soma 4 se verdadeiro e subtrai 4 senão,
saturando 0..256. Desenha apenas se valor>224, lookup 1,
virtual +4C. O decompilador perde EBX/EBP e pilha:
X=525-largura/2 e Y=((altura+16)*C(4*(v-224)-128)>>16)-altura
são a leitura provisória dos operandos de dimensão, não
assembly confirmado. Centro 525 é comprovado pelo bundle 01.
005BDAD0: gate 4000 desligado e soma do campo assinado
de quatro bits 21..24 de A58260 com predicado de turno
(A577F8 byte2, bit4 e lado relativo) >0. 0044BA10 testa
virtual +4 dos objetos em 005F3280+132C/1330.
O modelo aceita predicates/fatos do chamador; não implementa
IPresentationGate nem lê regras/estado Core.

A branch analise-visual (30e63ac, resource-inventory.csv)
confirma layerfont.txt com 29 bytes e prefixo demo; registra
demo_font_e.bmp 233x30 e wait_e.bmp 469x34, variantes f/g/i/s.
Não prova ordem ou texto integral do manifesto. O parser
NativeLayerFontResources exige exatamente dois BMPs existentes
em ordem do manifesto, resolve '?' para inglês como política
do cliente. Não adivinha nomes ou fallback quando faltam dados.

Modo isolado --review-layerfont=a ou b requer --native-cosine=
capturado. F7 executa uma chamada Draw lógica; F5 altera o
predicado B; F8 reinicia; F12 PNG/JSON. Redesenho da janela
e consulta de Sample são puros. Usa recursos originais do DAT,
não validado neste ambiente; compositores +48/+4C, mapeamento
dos registros de B e cor-chave ainda não provados.
Nenhuma integração com modo de duelo ou scheduler. Não aumentar
badge até haver uso no fluxo normal com contrato suficiente.

## V29 ajuda

0044C3E0 cria 0045E250 com outros objetos persistentes do duelo.
Construtor carrega help/help.txt, vtable 005DB630, camada 0xF00,
obj+10=-1, +14=0, global 005EBEE4=-1. As rotinas 0045E3D0 e
0045E4A0 não existem nas duas tabelas de decompilação do SQLite.
Não confundir com 0041E3D0 (hit-test do campo). O índice
histórico DAT lista help.txt 1119 bytes lógicos, 551 armazenados;
nenhuma chave ou geometria deduzida do tamanho. Ainda não
sabemos se o componente responde a um hover, evento de ajuda
ou outro gatilho. Roteiro 19-layerfont-help-ghidra.sh pede apenas estas duas
rotinas e assembly dos dois Layerfonts com slots das vtables.
Usar --inspect-visual-dat para os manifests, não versionar assets.

## A03

Draw-driven != Update-driven. A03 continua dona da ponte,
não avançar fase visual em CanComplete. Os objetos de Layerfont
são criados pelo opcode 10h (não se trata de um comando de texto
separado) e podem exigir vida útil independente do gate.
Não criar FIFO visual nem mapear modo nativo para modo de
duelo do sandbox por nome. Cobertura ainda 20/34, 0 Verified.
