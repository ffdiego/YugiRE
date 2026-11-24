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

## V29 ajuda — evidência cumulativa v2

0044C3E0 cria 0045E250 com outros objetos persistentes do duelo.
Construtor carrega help/help.txt, vtable 005DB630, camada 0xF00,
obj+10=-1, +14=0, global 005EBEE4=-1. O snapshot v2 (SHA256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
integrity_check=ok) contém Update/Draw no bundle
87ff09fe3b5b864c91373efc4c7dea874cab9c547ab47eb12dafded9401ee888,
retry-002-0045E3D0.txt, retry-003-0045E4A0.txt, assembly indexado.
Não repetir roteiro 19.

### Produtor e Update (assembly confirmado)

0045E240 grava uma única chave em 005EBEE4 (não é FIFO).
Xrefs em 0056B5A0, 0056B7D0, 0057E490, 0057E650 e outros.
Decompilação dos produtores indica feedback contextual de
disponibilidade de ações: 0056B5A0 solicita 3, 0x10 ou 0x12
em caminhos que retornam falso; 005929D0 solicita 9. Não
atribuir texto aos IDs antes de resolver o manifesto.

0045E3D7–0045E3E5 incrementa +14 em 4 enquanto <256.
005F2FDC deve ser não zero e 005F2FE0 zero; senão fecha.
00418B50 copia a palavra contextual. Quando já visível, mantém
somente se chave e bits 0..13 da palavra coincidirem; a troca
fecha imediatamente, sem reabrir no mesmo Update. Quando oculto,
aceita chave não negativa do lado local, exceto 9 que aceita ambos,
e virtual +2C de 005F33E0 OU 005664A0(6). Ao aceitar, guarda a
palavra e zera o contador. Global de pedido volta a -1 em todas
as saídas. Portanto o produtor precisa renovar o pedido a cada
Update. Os predicados permanecem fatos fornecidos pelo chamador.

### Draw e geometria (assembly confirmado)

0045E4B4–0045E4D2 passa três argumentos a 0045C140:
lado=word&1, zona=(word>>1)&31, índice=(word>>6)&255.
O retorno EAX/EDX fornece X/Y. A decompilação perde o terceiro
argumento e vários registradores. A altura-base H vem de
005F3000/2 se zona=11, senão 005F3008; Y0=Y-H/2.
0045E508–0045E528: escala 8.8 = C(8*t-128)>>8 se t<16,
256 senão. Após quatro Updates desde a abertura, altura cheia.

0045E538–0045E574 resolve a chave no manifesto e consulta dimensões
via virtual +38 (largura w, altura h). 0045E577–0045E5B7:
X0=min(X,800-w/2), Y1=max(Y0,h), h2=trunc(h*escala/256).
0045E5BB–0045E62D resolve novamente a chave e chama +4C
com X0-w/2, Y1-h/2-h2/2, par (w,h2), outros argumentos zero.
Divisões inteiras truncam para zero. Não há clamp esquerdo
neste trecho. O tamanho original é independente da abertura.
NativeHelpTimeline modela esse recorte com amostra pura e
Update explícito. Testes sintéticos cobrem ambos os lados,
chave especial 9, identidade, renovação, gates, abertura e
clamps; não são comparação com o original.

### Lacunas

Manifesto help.txt (1119 bytes lógicos no inventário histórico),
textos/variantes, remap das chaves, dimensões reais, origem dos
predicados, compositor +4C e fornecimento do contexto no cliente.
Não criar mensagens por aproximação nem ligar ao Core por nome.
A Draw segue Pending no inventário; V29 é Partial (modelo isolado).
Sem fila paralela, sem gate de conclusão.

## A03

Draw-driven != Update-driven. A03 continua dona da ponte,
não avançar fase visual em CanComplete. Os objetos de Layerfont
são criados pelo opcode 10h (não se trata de um comando de texto
separado) e podem exigir vida útil independente do gate.
Não criar FIFO visual nem mapear modo nativo para modo de
duelo do sandbox por nome. Cobertura ainda 20/34, 0 Verified.
