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
