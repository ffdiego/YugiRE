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
(10 words, não assumir que os dez IDs são distintos), procura
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
