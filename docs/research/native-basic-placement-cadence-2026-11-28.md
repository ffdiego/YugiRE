# V18.5 — cadência das ações comuns

Fonte privada: joey-placement-normal-20261128-113525.json, SHA256
84b2efb18e4bd5703565dbb82c9eb2030868eaf19f3be3ad17a88449a3d4117b.
EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
2700 frames, 1249 eventos, dropped=0, errors=[]. Modo normal.
SQLite v14 e documentação V18.2/3: 0044EF40 raw 7 -> Internal 0F,
raw 6 -> 10; descritor 0D em 005EFE18 aponta a 005F08F4
(magic_active.wav). Não versionar trace bruto.

| Caso | Frame início movimento | Frame commit descritor | Primeiro Update efeito | Último Update |
|---|---:|---:|---:|---:|
| CE Set monstro local | 238 | 246 | 247 | 268 |
| CF Set magia local | 303 | 311 | 312 | 333 |
| CE Summon CPU | 648 | 656 | 659 | 691 |
| CF Set CPU | 1284 | 1292 | 1293 | 1314 |
| CE Summon local | 1760 | 1768 | 1771 | 1803 |
| 91 revelação CPU -> 72 | 22 | 30 | 37 | 53 |
| 91 revelação local -> 72 | 1834 | 1842 | 1881 | 1897 |

CE/CF pedem raw 7 (caller 0040D434), som 1000000F (0044EF77),
e Update 00418B60 no mesmo frame. 91 pede raw 6 (0040D00C),
10000010 (0044EF87); x/y constantes, face 0->256 em 8 updates.
No nono Update, counter permanece 256 e sourceDescriptor recebe
destinationDescriptor. Set +12 (22 Updates, 21 Draws); summon +8
(33 Updates, 32 Draws); glow +16 (17 Updates, 16 Draws). Glow
pede 1000000D no primeiro Update (004505A6). Não há pedido
adicional de chegada de CE/CF no trace. Na revelação local, voz
10000057 no frame 1844 antes do glow; não impor 47 ticks de espera
nem universalizar o intervalo 15 da CPU.

App: placement flight inicia som na primeira amostra efetiva (inclusive
após tributos diferidos), retém amostra terminal até offset observado
9/11. Este hold é uma adaptação da sequência observada, não a recuperação
completa dos gates internos de CE/CF. Não altera CardTravel.Updates,
regras, fila ou pitch. O timing de glow permanece intacto; revelação
no campo e ativação diretamente da mão precisam de recorte específico.
Sem build local neste ambiente; CI e escuta/compare no cliente pendentes.
Sem promoção de cobertura. Snapshot v15 não disponível nesta sessão;
v14 usado para confirmar descritores. Trace privado referenciado, não
importado neste marco.

## V18.6 — Change of Heart da mão e Tribute Summon

Fonte privada joey-spell-from-hand.json, SHA256
 d354efbca04ef1e5750c21d9391dfd1b5685a40f1a5009adc241f723cfc208a5.
Mesmo EXE; 1721 frames, 295 eventos, zero perdas/erros. Mantenedor
relatou Change of Heart da mão e uso do monstro roubado em Tribute Summon.

| Frame | Opcode | Evidência |
|---:|---|---|
|117–125|CF|raw 7 / 1000000F; 8 updates até 256, commit no nono|
|127|CF|voz 10000057, caller 00417982|
|165–181|72|1000000D no primeiro Update; 17 Updates/16 Draws|
|235–250|08|kindRaw 1, tail 256->0 em passos de 16, sem movimento|
|430–438|83|raw 7/0F; transferência (681,248)->(525,352), ângulo 256->0 em 8 passos de 32, commit no nono|
|442–450|8F|raw 7/0F; movimento da magia para GY (369,460)->(760,366)|
|632|E6|voz 10000054|
|681/702|95|dois pedidos 1000000C, caller 004870A4|
|699/720|95|commits de descritores associados; não capturam todos os Updates do tributo|
|724–732|CE|raw 7/0F; viagem da nova carta, commit no nono|
|735–767|6F|33 Updates/32 Draws, offset 11 confirmado também após tributo|

SQLite v14 (quick_check ok), 00418B60 confirma posição por cosseno,
escala linear para mão/campo, ângulo por descriptor e commit quando
contador de entrada é 256. 00487060..004870A7: incrementa 8 se liberado
ou abaixo de 0x80; em 0048708E compara contador pós-update com 0x88;
0048709F pede 1000000C. Descriptor 005EFE08 -> 005F0908 = ikenie.wav.
No cliente, o estágio Travel de ativação agora usa 9 updates, o mesmo
renderer de pose do placement, e toca card_move na primeira amostra;
Glow toca magic_active na primeira amostra, sem atravessar etapas no
catch-up. Tributo liberado toca ikenie ao cruzar 17 updates (0x88),
não na entrada; mantém o renderer e duração existentes.
Não introduz voz local, espera fixa de 38 ticks, animação de controle
ou GY, nem corrige a ordem da reação da CPU nesta fatia. A voz local
precisa do produtor/predicado e rota visual; o trace não identifica
cada objeto de reação, nem a liberação da interface. A fase de prévia
em 0x80 do tributo não está integrada, então o instante absoluto do
som ainda não é equivalente. O timing de 11 ticks é observado, não
universal. CI e comparação local pendentes, sem mudança de badge.

## V18.7 — revelação de carta baixada e seletor de reação local

Reabertas ambas as capturas Windows privadas (hashes acima). Em 91,
frame 22 e 1834: descriptor 000A/000B -> 800A/800B, posição e ângulo
constantes; face 0..256 (+32), nono Update confirma descritor. raw 6
(0040D00C), 10000010 (0044EF87) = card_open.wav. Assembly 00418B60
confirma a interpolação de face independente de geometria. Cliente:
Snapshot da face ANTES de mutação em AppActionPresentationGate;
quando fonte baixada face-down, Reveal (9 updates) precede Glow;
mascara a carta estática no campo durante a revelação. Não revela
novamente uma magia/armadilha já face-up. Legacy CPU sem snapshot
não é promovido neste marco. Sem mudança no Core, fila ou pitch.

SQLite cumulativo v14: 004030F0 ramo lado diferente de (~00A54E5C)&1
seleciona 00402E00(4,10000057) para classe 0x16; 00403BF0 chama o
seletor indireto se os gates 0044B9B0/0044B980 permitem. CF 00405CB0
case 0 chama 00403BC0 para colocação ativada; case 1 espera os objetos
(+4), move a carta; case 2 chama 00403BF0 quando uVar11!=0 e uVar4==0;
case 3 espera os objetos (+4) antes de liberar. Assim a reação anterior
à viagem da CPU (00402BE0) permanece correta; não transpor essa ordem
para o jogador. As duas amostras locais pedem 57 em 00417982 dois ticks
após commit; glow em 1881/165. 00416BC0 epílogo confirma poll 005C0490
quando contador>=256, pedido primário no Draw de contador 8.

DAT v14 contém reaction04.txt: base 666x454 em (50,50), header 1
(rows 6 eyes1, 7 kuti1), header 12 (rows 9..11 te1..3, 477180).
O caso 12 em 00416BC0 usa contador próprio /24 e contagem do banco,
não os bits globais de cena. Faltam disassembly preciso do caso 12,
composição do grupo e confirmação do nome apontado pelo descriptor
005F02B8 -> 005F0540 (bytes da string ausentes no v14). Não inventar
nome, duração ou compositor. Rota estática localizada; implementação
reaction04 e gate variável ainda pendentes, assim como controle/GY.
Sem promoção de cobertura, validação CI/cliente pendente.

## V18.7 — descritor e término de reaction04

A coleta 76 confirma `005F02B8 → 005F0540 → Joey_51.wav`. O Joey.Tools
encontrou o recurso inglês em `Voice.dat`. A tabela de saltos em
`00417A10` resolve o header 12 para `00417681`. O resto de
`(contador / 24) % (quantidade_do_grupo + 1)` seleciona a camada.
Resto zero omite o grupo. Restos 1, 2 e 3 selecionam as mãos
`te1`, `te2` e `te3` na ordem DAT. O blit soma o X/Y codificado
no campo do manifesto à base que o Draw calculou, com quociente/resto
por 1000. O grupo de expressão passa por outro ramo do mesmo Draw.
O compositor escolhe a base antes de percorrer os grupos. A comparação
de olhos, boca e mãos nos quadros de entrada e saída fica pendente.

`00417971` pede a fala primária no Draw de contador 8. Se +18
contiver fala secundária, `00417999–004179BB` espera
`005C0490(primária)` antes de pedir a secundária e promovê-la
ao campo +14. A saída em `004179D0–004179FD` exige contador
ao menos `0x100` e `005C0490(+14)` verdadeiro. O primeiro gate
derruba +4; com flag +1C, também limpa o byte +5 do objeto de
apresentação externo. `00416BB0` expõe atividade quando +1C está
armado. O caminho de áudio desabilitado de `005C0490` retorna 1.
A voz depende do predicado, e o intervalo do trace é só uma amostra.
As ordens das rotas local e CPU observadas acima são específicas
dos produtores investigados.
