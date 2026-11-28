# V31.12 — busto de resultado (review)

SQLite cumulativo v14, PRAGMA quick_check=ok. Manifestos
j\bust_up\win.txt, lose.txt, draw.txt e dimensões inglesas em
visual_dat_manifests/images. Assembly e decompilação de 00415C90,
00416050 e 00416260 em visual_evidence_functions/instructions.

| código nativo | banco | variantes | base voz | personagem (x,y) |
|---|---|---:|---|---|
| 2 | lose | 3 | 10000059 | 401,43 (399×557) |
| 3 | draw | 1 | 1000005C | 0,40 (366×560) |
| outro | win | 3 | 1000005D | 0,40 (355×560) |

Primeiro parâmetro: lose=1003, draw=1, win=3. Resto por 1000
determina variantes; quociente & 1 determina sentido de entrada.
00416316..004163D7: Draw >64; deslocamento min(t-64,32)*largura SAR 5.
00416050: compõe fundo no input 0 e sombra/balão no input 96;
solicita base de voz + variante. Primeiro Draw recomposto=100.
00416550..004166B7: letreiro quando t>96; parâmetro 1..4 escolhe
canto, deslocamento por cosseno [(min(t-96,64)*2-128)&511],
produto SAR 16. Lose inglês 341×280 canto inferior esquerdo;
win 352×292 e draw 457×174 canto inferior direito.
00416219..00416259: poll em t>480 e t<0x10000; sucesso grava
0x10000 e soma 4 no mesmo Update. Draw terminal 0x10080.
Proton roteiro 68 observou código 2, voz 1000005A, 153 pares sem som
(salto 484) e 408 com som (salto 1504). Testes exercitam ambos.

00416419 e 004164BD leem 005DA7E0[phase], somam 4/6, verificam
parâmetro não zero. phase=(t>>3)&3; zero não desenha. Olhos exigem
((t>>5)&7)==0; boca exige t<0x10000. Tabela ausente nos chunks v14;
omitimos sobreposições, não usamos tabela da abertura. Os descritores
59..5F têm ponteiros 005F0528, 005F051C, 005F0510, 005F0504,
005F04F8, 005F04EC, 005F04E0; roteiro 72 coleta só as lacunas.

Review: --review-reaction=result:lose:1 --no-audio
ou result:win:0 / result:draw:0. F5/F6 normal/speedy, F7 passo,
F8 reset, F12 PNG/JSON. Opcional --reaction-wait-audio + F9.
Não toca voz real, nem altera duração de WAV. Composição em camadas
aproximada; sombra não refinada por orientação do mantenedor.
O original dispara após a recusa de salvamento do replay; não
inventar gatilho por LP zero. Sem integração ao duelo neste recorte.
CI e DAT real pendentes; cobertura inalterada.
