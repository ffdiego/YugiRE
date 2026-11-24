# V31/V18 — reações do Joey, pedidos sonoros e duração

SQLite privado v2 SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok (leitura em 24/11). Fonte: decompilations, refs, pointers,
functions, visual_evidence_memory/instructions/transient_decompilations.
As rotinas de reação abaixo não têm assembly de entrada no snapshot;
classificador e duração são portas de decompilação, não paridade confirmada.

## Objetos distintos

00402E00(reaction,sound): sound=0 não cria objeto; reaction=0 cria
objeto 0x18, vtable 005DA3FC e inicia som via 005C03D0. reaction !=0
cria 0xBC via 004166F0, carregando bust_up/reactionNN.txt.
004166F0/00416A00 instalam 005DA840; +14 recebe ID de som principal,
+18 um segundo ID ou zero, +1C flag, layer +0C=0xF01.
Com flag e global 005F3400 disponível/estado permitido, escrevem
modo 3, contador 0x20. Não chamar esse contador de duração do busto.
00416B80 consulta 005C04E0(obj+14), divide por 33 (signed),
resultado <1 ->32. O lookup 005C04E0 retorna -1 se áudio desativado,
0 se ID de família 1 inválido via 005C0A00; caso válido lê
00A53894[index]. 005C0F80 percorre RIFF fmt/data e usa __ftol para
preencher a tabela; o decompilador perde a expressão x87.
Falta assembly para confirmar unidades/arredondamento. Não assumir
que este parâmetro seja o total de Updates ou usar um timer de 32
para liberar A03 sem renderer. 00416870 é destructor, não Draw.
005DA844/848/84C não aparecem em pointers nem nos chunks de memória;
nenhum corpo Draw/Update da reação foi identificado no banco.

## Roteamento com dano (00403900)

00408470 estágio 1 chama 00403CE0 quando o flag de 00A550A4
permite, após os dois predicados de apresentação de LP. Wrapper
omite o chamado nos estados globais 005F3294=3 ou 6 e despacha
005DA3F0→00403900(side,0,delta). O dano é negativo, o lado
deve ser (~00A54E5C)&1; LP anterior vem de 00A55D64.
Delta>=0, LP=0, lado diferente: sem reação. Para perda positiva D:
- D >= LP: índice 5.
- LP <= 4000 e não letal: D<=1000 índice 3; 1001..2000 índice 2;
  D>2000 índice 4. Preservar a ordem aparentemente invertida
  até verificar assembly.
- LP >= 4001 e não letal: clamp(trunc((D-1)/500)-1,0,3).
O som é 0x10000045 + índice. O manifesto não é reaction(índice):
lê dword da tabela 005DA3C0[índice] e formata número em dois
dígitos. Os 24 bytes da tabela não estão nos chunks de memória.
NativeJoeyReaction.Damage retorna índice e som, não nome de sprite.

## Outros caminhos

005DA3D8/3DC/3E0 apontam a 00402BE0/004030F0/00403310
(ativação, outros eventos, invocação). 00403310 usa resource ID
0x51 ou 0x2E8 para 00414A20 (summon.txt/summon2.txt);
o primeiro é Red-Eyes, o segundo continua sem identidade no SQLite.
Para caso forte no lado selecionado (ATK>0x739, tipos 1/2),
usa RNG de apresentação 0055C140, reaction13 ou 08, som 28/2A
ou 29 e possível segundo som, além de 0x2000000C. Não consumir
RNG de regras ou inferir quando cada tipo ocorre.
00403700 reaction05/som 41 com intervalo 8 do campo 00A577F8;
00403770 escolhe reaction04/som58 ou reaction05/som4F/50 por lado
e argumento; 00403800 consulta ATK/nível; 00403B20 reaction02/som40;
00403B70 reaction08/som30. Estes VAs e requests são evidência
de decompilação; sem mapear opcode/evento não conectar ao App.

## Áudio

005C03D0 aceita família alta 1 (005C0730) ou 2 (005C1480, parâmetro
mínimo 5); 005C0F00 usa os 12 bits baixos para índice da família 1
e limita pelo contador 00A53880. 005C0B60 monta nomes a partir da
tabela de descritores 005EFD48 (stride 16, terminador nulo), não
da ordem física do Voice.dat. Não usar índice do JSON do DAT como
ID de som. A rotina de inicialização constrói a tabela de duração.
Roteiro 27 coleta os ponteiros/assembly e 0x70 descritores, que
cobrem os requests citados até 0x6D. Conferir se a tabela termina
depois desse prefixo antes de ampliar. Resolver strings com o
SQLite; não exportar WAVs ou pixels. A família 2 fica separada.

## Implementação e pendências

NativeJoeyReaction tem modelos puros para seleção de severidade,
tipo de objeto, duração raw e índice de descritor. Testes sintéticos
cobrem fronteiras e gates; nenhuma ligação à fila/clock/raylib.
O roteiro 27 pede vtables 005DA840/005DA3FC (4 slots cada),
tabela de dano 005DA3C0, descritores e assembly dos caminhos
acima. Depois consultar os alvos Update/Draw no banco antes
de coletar outros. Reutilizar --inspect-visual-dat para os manifests
bust_up/reaction*.txt; para posição e composição, aguardar
Draw/Update. Não criar assets, não alterar denominador de Draw
sem inventário comprovado, nem marcar Verified. V18 sem player.
