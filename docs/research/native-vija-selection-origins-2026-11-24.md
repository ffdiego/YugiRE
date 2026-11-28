# V30 — Vija e lacuna de origem V11

Fonte: joey-knowledge-visual-decoded.sqlite, read-only, quick_check=ok.
EXE SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta real a 004580F0, 00458480, 004584A0, 00409940, 00409AB0,
00458D00, 0045C140 e 0045C280. O Draw 004584C0 não consta em
decompilations nem visual_transient_decompilations do snapshot atual.
A pesquisa histórica bust-up-cinematics-and-destiny-board.md
menciona seu contador +4 por Draw; não recuperamos o ZIP histórico,
portanto não portamos uma sequência de pixels a partir dessa nota.

## V30

| Campo | Modo zero | Modo não zero |
|---|---|---|
| +14 (índice) | nativeIndex % 5 | nativeIndex % 5 |
| +10 (contador inicial) | -4 | -4 - 72*índice |
| 00458480 (limite) | 320 | 640 - 72*índice |
| 004584A0 (virtual +4) | contador <=384 | contador <=704 - 72*índice |
| Som solicitado | 10000022 | 10000023 |

A ABI do construtor tem variáveis de pilha sobrepostas na
decompilação. Os argumentos de índice/modo são sustentados
também pelos dois chamadores. Centro via 0045C140, descritor
fornecido pelo chamador; camada 0x120, ou 0x201+16*slot para
região 11. 00409940 cria um objeto modo 0 e espera virtual +4.
00409AB0 primeiro cria 00458D00 (vtable 005DB424, camada -256,
configura global 005F3400 com modo 0 e contador 32 se disponível).
No estágio 1, percorre a tabela de IDs de 005DA410 até 005DA423
(cinco registros de 4 bytes; cada ushort de ID seguido de um ushort auxiliar), procura
cada índice interno nas cinco zonas de magia/armadilha daquele
lado e cria modo 1 quando encontra. Depois espera os predicados
virtuais. A tabela precisa de leitura bruta para identificar
ordem/conjunto. Não reproduzir mutação de regra a partir dela.

O manifesto histórico tem 5 imagens conhecidas. A branch
analise-visual em 30e63ac registra vija_smoke.yga 400x80
e vija_moji_smo.yga 256x37. animation-formats.md documenta
parâmetros 5004 e 8031, /1000 para padrões, %1000 para
deslocamento Y; correspondem a recortes 80x80 e 32x37.
NativeVijaResources preserva ordem real, parâmetros e ausência;
NativeVijaAtlas valida divisibilidade. Nenhuma sequência de
sprites, máscara, origem absoluta ou fase de letra foi
inventada. NativeVijaContract expõe o predicado sem controlar
scheduler ou Draw. NativeCinematicRouting.ImplementedUpdates
continua null para DestinyBoard. A03 não deve reservar tempo
invisível com base no predicado isolado.

## V11 — Por que a transição ainda é pulada

0045C140 encaminha 0xC/0xE/0xF com slot=0 para 0045C280;
este retorna pares de dwords em 005DB5D0, 005DB5C0, 005DB5F0,
com índice relativo ao lado. Para 0xD, slot=0, usa
005DB5B0 e deslocamento X de 005F32D8[side] (soma lado
próprio, subtrai adversário); o decompilador perde Y do
retorno EDX. Precisamos da tabela bruta e da origem do
deslocamento vivo; não deduzir pelo desenho do deck/pilha.
005DB5B0:80 cobre os cinco pares duplos da região.
A coleta pede apenas dados de origem ausentes e novos
operandos Vija; sem repetir as funções dos quatro bundles
de seleção/fusão, status, atributos e chain-disabled.

Roteiro 20-vija-selection-origins-ghidra.sh exporta 004584C0, janelas de instrução
dos dois construtores/chamadores que ainda têm ABI ambígua,
vtables Vija/objeto auxiliar e tabelas. Importar o ZIP
privadamente com o importador cumulativo de evidências.
Depois, priorizar a animação de entrada/saída de seleção
com centros confirmados e a composição real Vija.
Sem nova integração de Draw: badge 20/34, zero Verified.


## Bundle 20 recebido — contrato de composição V30

Fonte SQLite cumulativo v2, quick_check=ok, SHA256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
Consulta de 004584C0 em visual_evidence_instructions, batch-000.txt,
e pseudocódigo retry-000-004584C0.txt. A mesma VA ocorre em dois
bundles; este contrato usa instruções do bundle com a janela completa.
A leitura de 005DA410 retornou F805000005060000060600000706000008060000:
cinco resource IDs 1528,1541,1542,1543,1544 a stride 4, todos os
ushorts auxiliares zero. Conferidos em card_identities: Destiny Board
e Spirit Message I/N/A/L. A vtable 005DB414 contém 00458330,
004584A0,004481F0,004584C0; helper 005DB424 contém 004030D0,
0048D9A0,004481F0,00458D50. Não repetir roteiro 20.

### Ordem e geometria

* 004584C6..004584CF: t += 4 no Draw, retorno sem imagem se t<0.
  Em modo grupo 004584FA..00458597 interpola de origin até
  ((letter+2)*100,300) por p=clamp(t,64,256)-64, /192;
  Y recebe ainda (C(8*t-128)<<3)>>16, mesmo fora da janela.
* 0045859B..00458724: key 0, frame=(t/8)%patterns0,
  scale8=min(4*t,256), largura e altura por SAR8;
  top=centerY-height/2-parameter0%1000, X centralizado.
  Intensidade 4*t antes de 64, 256 até limite, depois
  256+4*(limit-t). Preservar scale e intensidade separadas.
* 00458727..004589C8: t>=64 cria a superfície scratch
  com largura medida de key3/(parameter3/1000), altura key3.
  Copia key4 inteira duas vezes em X=t%width4 e X-width4
  (00458844/0045889C), sem usar largura de frame4 como módulo.
  Copia key2 recortado em (t/8)%(parameter2/1000) * scratchWidth,
  depois aplica key3 no recorte letter*scratchWidth.
  Chamada virtual +98 recebe os argumentos 0,255,0,255,0,0,
  rect,0,0 conforme pushes (ordem ABI conforme leitura reversa).
  A composição RGB/máscara não foi substituída por alpha.
* 004589CE..00458AD7: local=t-64 <128, uma chamada +4C por
  linha de scratch. Destino width=(scratchWidth*local*2)>>8,
  left=centerX-width/2 + (C(8*local-128+16*row)*(16-local/8)>>16).
  Y=top+parameter3%1000+row. Fonte (0,row,scratchWidth,1).
  00458ADB..00458B2D: local>=128, blit +48 da superfície inteira,
  centro X, mesmo top+offset e intensidade do fade.
* 00458B34..00458CDF: t>=32, key1 por último, u=(t-32)/4.
  Se u<8, frame=u/4, deslocamento Y=8-u. Senão frame na
  tabela [2,3,4,3][u&3], deslocamento zero.
  Fonte strip por parameter1/1000, destino X centrado e
  Y=top+parameter1%1000+deslocamento. Intensidade do fade.
* 00458CE2..00458CF3: flag +4 fica t<limit+64, mas
  virtual +4 (004584A0) responde t<=limit+64. Não confundir.
  Contando inclusive o Draw inicial t=0 e terminal t=limit+64,
  modo 0: 97 Draws, modo grupo: 177 Draws por objeto
  (atraso inicial compensa limite local menor).
  O último Draw libera flag, não o predicado inclusivo. A03 deve
  observar remoção/dispatch do objeto e não bloquear em um
  predicado de instância já inativa. Não introduzir nova fila.

NativeVijaTimeline agora entrega geometria de cada pass,
comandos da superfície scratch e recortes por linha; testes
sintéticos exercitam as cinco letras, os dois modos e fronteiras.
A callback de dimensão usa **chave lógica**, não nome de arquivo.
O texto privado de vija.txt ainda não foi disponibilizado
nesta sessão. A ordem no parser não é prova do remap virtual.
A operação +98 e o formato nativo da superfície scratch ainda
precisam ser reproduzidos antes de criar renderer completo;
não anunciar animação jogável nem ligar o gate de vitória.
O contrato de key4 não exige inventar um divisor de atlas.
A referência histórica animation-formats.md confirma os
campos 5004 e 8031, mas os dados nos testes são sintéticos.

### V30: próxima lacuna específica

Resolver implementação de +98 e seus canais e a identidade
do objeto scratch (+B0, criado por 004a9cc0), aproveitando
decompilações do SQLite (004AD9C0, 004A9CC0 e xrefs/vtables).
Obter vija.txt e dimensões via --inspect-visual-dat local.
Nenhuma nova coleta ampla nem nova espera no scheduler.
Cobertura 20/34, zero Verified.

## V30.3 — banco real e alvo da máscara (27/11)

SQLite cumulativo v7, quick_check ok, consulta read-only a
visual_dat_manifests e visual_dat_images:

| Key por ordem DAT | Recurso | Dimensão | Parâmetro |
|---:|---|---|---:|
| 0 | vija_smoke.yga | 400x80 | 5004 |
| 1 | vija_skel.bmp | 210x41 | 5004 |
| 2 | vija_moji_smo.yga | 256x37 | 8031 |
| 3 | vija_moji_mask.bmp | 160x37 | 5031 |
| 4 | vija_moji_iro.bmp | 160x37 | ausente |

Key2: oito quadros 32x37 de fumaça da letra; key3: cinco
máscaras 32x37; key4: fundo inteiro 160x37 sem parâmetro.
Fundo usa módulo 160, fumaça (t/8)%8. Validação estrita opcional
NativeVijaResources.ValidateTexture; novos testes de todas as cinco
letras e ambos os modos com metadados reais. Ordem DAT não comprova
remap virtual nem pixels.

004A9CC0 grava 005DC6EC. A janela de 228 bytes da vtable no v7
confirma +4C=0041AB40, +94=0041AC90, **+98=004CCD40**,
+A0=004EEC70. 004589C8 chama +98 no scratch obj+B0 com argumentos
após ponteiro fonte: 0,255,0,255,0,0,rect,0,0. Duas cópias de fundo
via 004AD9C0, faixa de fumaça via +4C antes da máscara. 004CCD40
não tem decompilação/janela própria no v7. Não reutilizar a semântica
de +94 de Affect para +98. Coleta 59 direcionada; nenhum renderer
ou espera artificial, sem mudança na métrica.
