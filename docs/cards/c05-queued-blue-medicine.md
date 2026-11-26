# C05.1 — Blue Medicine (1006/P041)

SQLite cumulativo v2, quick_check ok, leitura somente:
card_identities (1006,820,Blue Medicine,22); card_callback_slots
primary 00584D00, target_preparation 00593CA0. Ambas ausentes
em decompilations e visual_transient_decompilations. Pack-index
[1006,4,2] confirma magia jogável no pack Joey. Texto público
oficial confirma cura 400 LP:
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4840&ope=2&request_locale=en
Não alegar callback nativo recuperado nem paridade.

ResolveQueued/SupportsQueued adicionam Blue Medicine à família
de LP mantendo ResolveDemo/Supports legados inalterados.
TryQueueActivation delega ao produtor existente, que emite
SendToGraveyardCommand e HealCommand através de EffectContext.
Set usa a mesma guarda de gasto. Não criar mutação ou produtor
paralelo; não duplicar efeito em Play/ActivateSetSpell.
Testes ambos lados, mão/Set, gate, identidade, caminho genérico,
negação de bypass legado, e side inválido. Done no escopo
headless somente após CI. UI/CPU e negation pendentes.

## Próximos recortes
Tremendous Fire (346/P053) entrou na fila em C05.2, com duas
perdas separadas e adjudicação conjunta após os gates.
[Comportamento e limites](../../CARDS.md#magias-lp-e-compra-na-fila-c12).
Mooyan Curry (337,P040) consta do catálogo nativo, mas não dos
771 packs jogáveis; não implementar para aumentar a badge.
Gift of The Mystical Elf (1092/P156) é Trap, não LP Spell;
depende das janelas/guarda de Trap. Nenhuma promoção desses IDs.
