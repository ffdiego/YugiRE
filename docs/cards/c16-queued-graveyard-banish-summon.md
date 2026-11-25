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
posteriores já implementados. A CPU em modo fila e o console App/Lab
usam este produtor por ocorrência. O seletor gráfico, Spirit Elimination,
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
