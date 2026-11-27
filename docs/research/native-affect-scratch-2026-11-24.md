# V24 — scratch e amostragem de affect.txt

O roteiro 17 confirma o recorte do fundo em `0040E040` e o gatilho de
recomposição em `0040E1F0`. O manifesto e as dimensões foram cruzados
com `visual_dat_manifests` e `visual_dat_images` no snapshot privado v3.
O roteamento de foco e vínculos está no [contrato V23/V24](native-chain-affect-contract-2026-11-24.md).

`0040DBF8` consulta a entrada 1 de `affect.txt` e `0040DC4C` cria
o scratch no tamanho do ícone consultado, 50×50. A entrada 0 é
`loop_blue.bmp`, 100×69. As cinco entradas de ícone preservam a ordem
do manifesto. O virtual de lookup `0043A5D0` mantém o índice recebido.

`0040E0AB–0040E0E6` calcula a origem vertical do recorte como
`(contador/8) % alturaDoFundo`. O fim é
`(origem + alturaDoScratch) % alturaDoFundo`. Se o fim for menor que a
origem, copia primeiro [0,fim) na linha `alturaDoFundo−origem` do
scratch (`0040E145`) e depois [origem,alturaDoFundo) na linha zero
(`0040E191`). Sem wrap, só [origem,fim) vai para a linha zero.
As duas cópias partem de X=0 com largura igual à do scratch.

`0040E1F8–0040E209` recompõe apenas quando o seletor difere do último
desenhado. A troca de foco reseta o contador, mas o scratch segue
reutilizado se o seletor não mudar. Se o mesmo Draw solicita dois
seletores distintos, há duas recomposições. O passo final conserva o
centro e a intensidade assinada em `0040E252`.

`0040E1DF` passa o ícone selecionado ao slot +94 do scratch com
argumentos 0,255,0,255,0. O roteiro 24 confirma a vtable `005DC6EC`
e permite seguir `0041AC90` para `004CCD40`. O caso fonte tipo 3
e destino tipo 10 usa `004ED5E0` para derivar a máscara a partir
dos cinco bits baixos da fonte e da tabela de remapeamento. A conversão
dos outros formatos e o uso dessa máscara na composição final seguem
pendentes. O plano C# representa a ordem dos passes e os retângulos,
mantendo a rasterização e a seleção completa de vínculos fora da entrega.
Nenhuma Draw foi acrescentada à cobertura.

**Correção V24 (27/11):** a instrução 0040E288 no início de Draw
zera +128 após resolver o objeto em foco. A reutilização descrita acima
vale apenas entre passes do mesmo Draw, não entre frames. NativeAffectDraw.BeginDraw
modela esta fronteira. O banco de ícones não é a animação transitória de
EquipCommand; [V32](native-equipment-travel-2026-11-27.md) documenta a rota B2.

## V24.3 — auditoria estática de seletores (27/11)

SQLite cumulativo v7, consulta read-only: instruções de 0040E260..0040E913,
card_identities, card_callback_slots, visual_dat_manifests. O manifesto
Shift-JIS confirma: key1 装備 (equipamento), key2 永続 (contínuo),
key3 フィールド (campo), key4 拘束 (restrição), key5 パワーアップ系
(aumento de poder). Nomes não são suficiente para atribuir carta.

- **Dragon Capture Jar, resource 328 / 0x148:** foco monstro (região 0),
  bit reveal e raça efetiva 1 via 0056F6D0; varre slots 5..10 dos dois
  lados. 0040E435 compara resource 148, 0040E442 exige reveal,
  0040E446..5B exige flags & 22h == 0. 0040E475 PUSH 2 para a posição
  da armadilha. No sentido inverso, 0040E81E/832 exige carta revelada e
  raça efetiva 1, 0040E852/858 confirma Jar revelado no foco,
  0040E861 exige somente flags & 20h == 0; 0040E88A PUSH 2 para a
  ocorrência varrida. Não impor simetria dos bits; a segunda varredura
  percorre zonas 0..10, não apenas 0..4. Não recalcular raça pelo catálogo.
- **Thousand-Eyes Restrict, 0x536:** 0040E4B2..D2 consulta 005699C0(536h)
  menos 1 se o próprio foco for TER. Resultado > 0 desenha key4 SOBRE
  o monstro em foco (0040E4F4), não sobre o TER que gera a restrição.
  A contagem deve vir do predicado original, não de qualquer ocorrência.
- **Vínculos armazenados:** 0040E565 lê kind (low nibble) e par side/zone
  da lista da carta em foco; exige ocupação da carta relacionada em
  0040E5D1. Para foco monstro, 0040E602..61F usa resource da carta
  RELACIONADA: 0x52 Castle of Dark Illusions -> 5; 0x2D9 Dark-Eyes
  Illusionist e 0x534 Invitation to a Dark Sleep -> 4; caso comum -> 2.
  No sentido inverso 0040E773..790 consulta resource do próprio foco.
  Decompilação da tabela inbound 0040E7A6 confirma kinds 1/4/5 e 2/6,
  com seletor 1 ou 2 para foco S/T. Não aplicar esse mapa indiscriminadamente
  ao sentido outbound; Ghidra avisa sobre a segunda jump table.
- **Campo:** 0040E33D/365 e 0040E50D/536 usam resultado de 0056F430
  (lado efetivo ou -1). Foco FIELD varre monstros revelados dos dois
  lados; o teste não compara explicitamente o lado do FIELD em foco
  com o resultado. Não substituir por regra genérica de buff.
- **Poder:** 0040E395/3B7 e 0040E8A5/8C9 consultam 0056F5C0 nas
  duas direções, com raça/atributo efetivos e tabela de efeitos de
  monstros. Key5 não é equivalente a todo aumento numérico de ATK.

Spellbinding Circle é resource 348 / 0x15C, callback primário 00585200,
candidate 00579C40, target_preparation 00593970. Nenhuma instrução ou
 decompilação desse primário no v7; a falta de referência não prova
inexistência de um vínculo. Jump tables de 24 bytes em 0040E918,
0040E930, 0040E948 não estão em visual_evidence_memory nem pointers.
Roteiro 58 exporta exatamente as três tabelas (72 bytes contíguos),
os callbacks relevantes e helpers da relação. A informação faltante
para decidir Spellbinding Circle é estática: kind registrado e destino
no switch, não uma captura por carta. Renderer/compositor continuam
pendentes; nenhum novo Draw contabilizado.
