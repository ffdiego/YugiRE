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


## Complemento V17 — projeção candidata

O SQLite v2 contém a decompilação de 004223F0, não seu assembly.
NativeNoticeProjection oferece geometria de quadrilátero isolada,
com alturas distintas por lado e MulDiv; não modifica o renderer.
O contrato e roteiro 25 estão em [rasterização](native-quad-raster-contract-2026-11-24.md).

## Integração V07 — aviso de jogador durante a virada

SQLite v3, visual_dat_manifests: phase_name.txt contém 46 registros, 26..35 nex_?_b, b1..8, r; 36..45 nex_?_r, r1..8, b. Dois endpoints repetidos; não ordenar nomes nem deduplicar o manifesto. visual_dat_images confirma 18 texturas inglesas distintas de 395x65. NativePhaseNoticeResources.ValidateTurnNoticeBank valida o recorte original; DrawAtCounter usa o seletor já confirmado em assembly. DuelPhaseScreen inicia plano no mesmo ramo da virada, amostra turnFlipElapsed, deixa aviso cessar em 48 enquanto botões seguem até 64. Sem novo atraso de fase, som ou gate. Perspectiva fixa 0 para preservar as cores do painel existente no duelo local; não altera ViewPlayer. NativeNoticeProjection (candidato de decompilação) alimenta colunas de textura com interpolação linear; não é o rasterizador nativo. Fallback de recurso ausente mantém apenas giro já funcional. Testes de ordem e endpoints; desktop pendente. Sem promoção Verified.

## V07.1 — predicado de busy do painel confirmado (25/11)

Fonte privada v4-visuals, SHA256 SQLite
2085bc65dbc79fb2ec64645d2536bf0b56dde7bfedf4d173e32acb060b43722c,
quick_check ok, fingerprint EXE esperado. Não repetir coleta 23.
O bloco 005DBB2C:16 da coleta 23 decodifica quatro VAs:
004811D0 (destrutor), 004812B0 (+4), 00481370 (Update),
00481420 (Draw). A vtable distinta 005DBB1C:16 tem
00480B20, 00480B10, 00480C20, 00480C60; +4 do aviso
consulta tipo +A0 != -1, não confundir com o +4 do painel.

A rotina 004812B0..004812D0 está completa dentro das janelas
00480690, 00480C20 e 00480C60 já importadas (bundle 23
9758990fa9f00a3935a4db6d61320cbf6edf8575c5091f5f6074e78b9d0910fe).
Retorna 1 se +150 < 512 OU +14C < 128; senão 0.
00481210 converte retorno igual a 1 em true; 00404920
(decompilação persistente) libera bit 4000 quando retorna zero.
004811A3..004811AD inicializa +14C=256 e +150=512.

00481230: lado mudou -> zera somente +150, mantém +14C;
mesmo lado -> zera +14C. 00481370..0048141F:
enquanto +150<512 avança 8, ao atingir 512 solicita
virtual +14(0) do banco do painel e inicia fase armazenada
se !=-1, zerando +14C; não avança fase nesse update.
Se fase=-1, preserva +14C, que volta a avançar no próximo
update. Fora da virada, +14C soma 8, exceto Battle
quando 64<=counter<128 soma 4. A partir de 256 o contador
continua em passos de 8 e volta a 256 ao atingir 1024.

A partir de estado inicial/idle, busy termina após 16 updates
de fase comum, 24 de Battle, ou 64+16/24 na troca de jogador.
Com fase -1 e contador anterior >=128 termina em 64; se
anterior <128 ainda espera o contador remanescente.
O aviso normal permanece ativo até Draw com t>=256 (32/40
updates) e aviso de jogador até Draw com t>=384 (48).
Portanto completar toda a animação NÃO é o predicado nativo.

NativePhasePanelState porta exclusivamente os dois contadores
e o resultado do predicado, com Begin e Advance puros e
indicações de solicitação ao banco e início da próxima fase.
Testes incluem limiares, ambos tipos de transição, todas as
fases, -1 com contador anterior incompleto, wrap de idle.
ACTIONS/A03/A07 deve consumir este contrato quando assumir
a produção de fases; não alterar PhaseBusy do App agora
(ele também protege compra, Standby, fila e outros estados).
Sem novo scheduler, gate, mudança de pixels ou badge.

O compositor de 004223F0/0051EBD0 continua dependente do
roteiro 25 (nenhuma instrução nos intervalos consultados
no snapshot v4-visuals). Os dois lados do Draw de tipo 6
mantêm a política de perspectiva do cliente. Raw SETZ
em 00480C94 e 00480BFB compara com ((~DAT_00A54E5C)&1);
não interpretar a igualdade diretamente como lado próprio
sem mapear a polaridade da perspectiva nativa. A base de
Draw e preload usa offsets opostos, conforme já documentado.
CI novo pendente; não elevar V07 a Verified.
