# V07 — aviso de troca de jogador e virada dos botões

Fonte: joey-knowledge-visual-decoded.sqlite (read-only, quick_check=ok).
SHA256 EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Decompilações persistentes consultadas: 004809A0, 00481040,
00481230, 00481370, 00480BC0, 00480B40, 00404920, 004811F0,
00481210, refs e pointers. Nenhuma nova evidência de assembly.
O snapshot não possui 00480C20/00480C60/00481420 em decompilations
nem visual_transient_decompilations. Não inferir Draw do nome dos recursos.

## Ordem nativa

00481040 incorpora um objeto 004809A0 (vtable 005DBB1C,
layer 0x700, phase_name/phase_name.txt, +A0=-1, +A4=0)
ao painel (vtable 005DBB2C, layer 0xF0, phase/phase.txt).
00481230 é chamado por 00404920 e 004811F0.
Se +144 == novo lado, chama 00480B40(lado,fase) e zera +14C.
Se lado mudou, zera +150, chama 00480BC0(lado) e pede som
10000005, grava novo lado e fase em +144/+148.
Portanto o aviso de troca inicia **junto** com a rotação dos botões,
não como uma etapa serial adicional depois da virada.
00481370 soma 8 em +150 até 512; ao alcançar 512 chama virtual
+14 do banco do painel com 0, e se +148 != -1, chama 00480B40
com a fase registrada e zera +14C. Se -1 não inicia aviso de fase.
00404920, no estágio seguinte ao setup, aguarda o virtual +4
do painel 005F33F0; se falso libera 4000. O predicado exato
não está resolvido pela leitura de Update. 00481210 também
consulta esse mesmo virtual. Não implementar gate pelo valor
de contador isolado; A03 deve decidir a integração.

00480BC0 grava lado +9C, tipo 6 em +A0, zera +A4.
Preload virtual +8 (key, -1), cinco chamadas:
  lado próprio: 27,29,31,33,35
  lado oposto: 37,39,41,43,45
O lado relativo usa comparação com ((~DAT_00A54E5C)&1).
00480B40 (fase normal) escreve lado, tipo=phase e zera +A4.
Quando phase==3: preload de 13,15,17 ou 20,22,24; pede som
10000011. A chamada do preload não prova a ordem das imagens
do Draw. O histórico native-visual-contract.md documenta
tipo 6 com incremento 8 e fim 384 (48 updates), painel 64
updates. Preservamos o dado como contrato estático histórico;
a rotina que efetivamente desenha e termina ainda precisa de
assembly. Não usar a duração isolada como gate de A03.

O índice histórico resource-inventory.csv em analise-visual
(30e63ac) registra phase_name.txt (856 bytes lógicos), e
nex_e_b.yga/b1..8 e nex_e_r.yga/r1..8, todos 395x65.
Há variantes de idioma. Isso não prova posição, blend,
chave para cada frame nem conteúdo do manifesto.

## Implementação e limite

NativeTurnNoticePlan é entrada imutável para uma única
troca, amostrada pelo número de updates fornecido pelo
chamador. Não possui fila nem avança regras. A amostra
relaciona notice, panel flip e início da fase posterior;
não implementa CanComplete. NativePhaseNoticeResources
preserva ordem, parâmetros e existência no DAT; parser
testado com fixtures, não com o manifesto original. As
chaves são as dos preloads, não um sampler de quadros.

ACTIONS/A03 continua dona da ponte. Não reservar 48 updates
adicionais, nem modificar DuelPhaseScreen (fluxo legado).
Para integrar o Draw, recuperar 00480C20/00480C60, ler
vtables 005DBB1C/005DBB2C e resolver o predicado virtual +4.
Usar roteiro 23, que não repete 11 (tabelas ACTIONS A07).
Executar --inspect-visual-dat para ler phase_name.txt local;
confirmar ordem, recortes e blend antes de desenhar.
Sem aumento de cobertura: 20/34, zero Verified.
