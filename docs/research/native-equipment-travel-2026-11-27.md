# V32 — Animação específica de equipamento (B2)

Fonte: SQLite cumulativo v7, EXE fingerprint db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta read-only a callbacks, decompilations, refs, pointers e assembly persistido
nos windows 00455FF0/00456360. Não foi executado o original.

## Produtor e efeito visual

Para equipamentos comuns (ex.: recurso 300/301/302/314/315),
primary 00584500 verifica ocorrência e 0057A470 (elegibilidade), chama
00574990 -> 005749B0(kind=1). Esta emite **B2** em 005B9170 com B=
source side/zone, C=target side/zone, D=link kind; também agenda evento
1A. Pode emitir 88/8A para casos especiais. A decompilação do dispatcher
case 73 e ref 005B970D apontam 0040A230; o encadeamento de B2 com o
índice é forte, mas a tabela comprimida precisa ser relida para confirmar
o par bruto B2/73. 0040A230 no subestado 0 cria 00456490; espera
predicados virtuais +4; chama 0056AB60 para gravar o vínculo no final.
O construtor aloca 0x20, usa 005DB3E4, camada 0x110, banco GRANDE
005F2FF8 (não mini), +10=0, +14 índice interno mascarado pela visibilidade,
+18 descritor da fonte, +1A descritor destino.
O índice esparso só confirma 005DB3E4+0=00456680. Não supor o gate +4
sem a janela da vtable.

Assembly 00456730: ADD [ECX+10],8; RET.
00456740..004568B3 está inteiro na cauda das janelas Layerfont:
resolve centro do destino (+1A), depois fonte (+18) por 0045C140.
Para t=counter-128, angle=t se counter<128, senão 2*t.
ScaleFixed16=(256+trunc(C(angle)/256))<<6.
Peso=0 se counter<128, senão 4*t. Centro = fonte + trunc((destino-fonte)*peso/256)
em cada eixo. 00422C00 recebe ângulo 256*(ladoFonte XOR bitPerspectiva),
escala nos dois eixos e intensidade bruta 128. Em counter=192, após
desenhar, pede som 1000000A e limpa ativo (00456892..A8).
Não é o efeito contextual Affect, nem fluxo de fusão.

## Cliente

NativeEquipmentTravel porta a amostra 0..192 e request terminal.
Após EquipCommand bem-sucedido, Core publica snapshot com carta, fonte
S/T e destino. A sequência visual do comando toca a ativação existente
antes da viagem semitransparente da carta grande. O terminal precisa
ser desenhado antes de liberar o comando; 24 Updates até 192 e uma
fronteira de apresentação adicional no host para preservar o Draw final.
Não há nova fila lógica. O estado do vínculo já está commitado no Core
(como antes); não se alega que os números ATK/DEF ou a visibilidade do
vínculo mudem no mesmo instante do original. No modo instantâneo, sem
visual. Rotação relativa ao lado exibido é hipótese do cliente, e
compositor/tint Raylib e áudio ainda não equivalem ao original.
Rota visual padrão dos EquipCommand migrados; variantes de kind e
equipamentos de callbacks 00584610 ainda não confirmadas.
Testes de contrato sempre ativos: geometria dos dois lados e posições
relativas, resultado de ocorrência sucesso/obsoleta.

## V24: vínculo contextual independente

0040E288 reseta [obj+128] para zero em CADA Draw com foco. O scratch
só pode ser reutilizado entre passes de seletor igual no mesmo Draw.
Corrigido modelo com BeginDraw, preservando o wrap. manifesto original:
loop_blue.bmp, ico_equip.bmp, ico_conti.bmp, ico_field.bmp,
ico_kousoku.bmp, ico_power.bmp. Ícone equip é key 1, 50x50. A tabela de
links usa kind 1 para equipamento comum; 0056AB60 guarda o descritor da
fonte no monstro. A varredura inbound 0040E7A6..0040E7E5 em foco S/T
kind 1 aponta o ícone equip ao monstro, e o ramo outbound de foco
monstro aponta para fonte S/T. Não implementar scratch por alpha
simples antes de recuperar remap 004ED5E0 e compositor +50.

## Pendência

Roteiro 57 pede somente vtable 005DB3E4, tabela comprimida para B2 e
rotinas de producer/handler já identificadas. Não repetir a coleta de
assembly conhecida como condição de progresso. Rodar validação visual
com equipamento comum da mão/Set, alvo próprio/oponente, revisar tempo
do atributo e áudio. A Draw 00456740 não consta da matriz auditada de
39 entradas; não incrementar numerador/denominador até reconciliação
da vtable/inventário. 

**Precisão sobre o vínculo:** 0040A230 chama 0056AB60 com o par C
como primeiro argumento (registro dono da lista, normalmente monstro),
B como segundo (par fonte S/T), e D como terceiro (kind). 0056AB60
acrescenta a relação e metadata em arrays separados e incrementa a
contagem. Assim, foco sobre monstro percorre o vínculo armazenado;
foco sobre equip precisa da varredura inversa. Não confundir o sentido
com a origem da animação B2.
