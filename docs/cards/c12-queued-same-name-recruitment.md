# C12 — Recrutamento de mesmo nome

P168: Nimble Momonga (1115), Giant Germ (1114). Bubonic Vermin (1307)
usa FLIP, não o gatilho de destruição por batalha. Reusar filtros e
solicitações de CardSelectionRules; nenhum ID novo ou promoção de cobertura.

A resposta opt-in TryQueueSameNameRecruitment recebe o request exato,
zero a MaxSelections ocorrências distintas; MaxSelections é limitado
pela quantidade de candidatos e slots no produtor legado (até dois P168,
um Bubonic). A resposta revalida antes de consumir o pedido. Múltiplas
cópias usam slots livres da esquerda para direita na ordem escolhida;
request com máximo um admite slot explícito. Não misturar com seleção
parcial legada. Royal Command suprime Bubonic antes de resolver, não P168.

SpecialSummonCommand agora tem FaceDownDefense opcional, default false.
Nimble Momonga e Bubonic Vermin invocam em defesa face-down; Giant Germ
em ataque face-up. Um comando por ocorrência e um shuffle depois de
todo o lote, só se alguma movimentação aplicada. Recusa ou falha total
não consome RNG. O executor revalida cada movimento; alteração externa
entre comandos pode rejeitar uma movimentação sem desfazer as anteriores.
O deck restante preserva a ordem relativa antes do shuffle; mesma seed
e mesmo estado final do sandbox em resolução normal. Não afirmar ordem
de apresentação nativa; legado embaralha antes das invocações.

A batalha e efeitos de LP P168 permanecem legados (não repetir cura/dano
durante seleção). Bubonic atacado com Damage Step diferida continua
legado; apenas resposta sem deferredBattle entra na fila. UI/CPU e
janelas de resposta ainda pendentes. Testes ambos donos, 0/1/2 cópias,
posições, seleção obsoleta, gates, seed, Royal Command e limite de batalha.
CI pendente.
