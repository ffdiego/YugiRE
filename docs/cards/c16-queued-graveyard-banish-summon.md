# C16 — Invocação inerente por banimento do GY

Fonte: joey-knowledge-visual-cumulative-2026-11-24-v2.zip,
SQLite quick_check ok, decompilations 0059DEF0 e 00599E30.
0059DEF0 retorna 3 para 0x5ea, 2 para 0x5eb e 1 para 0x5ec..0x5ef.
00599E30 percorre GY próprio (param1 & 1), 0x5ea exige classe 3
via 004025D0; 0x5eb..0x5ef exige classe <21 e atributo igual ao
da carta fonte via 00402460/00402650. Não inferir os efeitos seguintes.

| ID | Carta | Custo |
|---|---|---|
|1514|Dark Necrofear|3 Fiends|
|1515|Soul of Purity and Light|2 LIGHT|
|1516|Spirit of Flames|1 FIRE|
|1517|Aqua Spirit|1 WATER|
|1518|The Rock Spirit|1 EARTH|
|1519|Garuda the Wind Spirit|1 WIND|

O catálogo de identidades v2 não tem linha 1518; ID inferido da
família consecutiva, confirmar nome/atributo DAT antes de promoção.
A regra executável usa atributo real da fonte DAT, não nomes.
O manifesto registra os seis procedimentos e o escopo dos efeitos
posteriores já implementados. O tabuleiro e a CPU em modo fila, bem como o
console App/Lab, usam este produtor por ocorrência. Spirit Elimination,
os efeitos de posição de Aqua/Garuda, a mudança de controle C17 e as
janelas de resposta continuam pendentes. A API legada por ID continua
restrita a Dark Necrofear.

QueuedGraveyardBanishSummonCandidates e TryQueueGraveyardBanishSummon
validam mão, Main Phase, dono ativo, custo exato e slot livre. Executor
revalida lote antes de banir. BanishCommand aceita GY; cada ocorrência
aguarda gate, SpecialSummonCommand Hand só permitido pelo procedimento
reservado integralmente pago. FaceUpDefense adicional mantém posição
legada de P252; não usa normal summon allowance. Sem compra, shuffle,
GY arrival ou gatilhos de batalha. Alteração externa após início
pode interromper custo sem rollback; não invocar sem pagamento completo.

Testes dos seis IDs, lados, posições, duplicatas, filtros, custo/slot
inválidos, gate, origem da mão, após Normal Summon e tentativa de
burlar custo. CI do commit 3178443 aprovado. O manifesto registra o escopo Partial;
não promover a Done antes de completar os efeitos e integração.

## Entrada do tabuleiro

No modo fila, clique no monstro elegível da mão, escolha a posição aberta e
clique em cada custo. O último clique envia a seleção inteira à fila. Antes
dele, outro clique no mesmo custo desmarca a ocorrência. Backspace ou Cancel
fecha a lista. O destino é a primeira zona livre no momento da admissão.
Os controles de posição, cancelamento e seleção são adaptações provisórias
do App. A lista reutiliza o ramo estável do seletor V11 e seus assets DAT.

No EXE identificado pelo snapshot cumulativo v6, o branch `0x32` de
`0059DF40` guarda a quantidade devolvida por `0059DEF0`. No caminho sem
Spirit Elimination, o assembly em `0059F5A9–0059F5D6` lê essa quantidade,
soma 6 e chama `0040C940` com lado e resource ID. `0040CA33` chama
`0043A060`. Em `0043A235–0043A2C6`, modos acima de 4 caem no
manifesto `selectlist` (string `005EB9C4`), então C16 usa os modos
7/8/9 com `selectlist1/2.txt`. O nome do cemitério no prompt expressa
a zona dos custos. A timeline de entrada/saída, os indicadores de
seleção e os controles nativos de posição ainda precisam de comparação.
