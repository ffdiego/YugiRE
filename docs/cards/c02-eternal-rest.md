# C02.1 — Eternal Rest (655/P092)

Pack index [655,2,2] confirms Kaiba pack (not Joey pack), normal Spell.
Cumulative SQLite v2 read-only quick_check ok, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
EXE fingerprint db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
card_identities identifies Eternal Rest and class 22; P092 primary 00586540,
candidate 0057AAA0. Neither entrypoint has a body in decompilations,
visual_transient_decompilations or visual_evidence_functions in this snapshot.
The official card text confirms destruction of all equipped monsters:
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4662&ope=2&request_locale=en
No claim of native filter or native order.

## Implementation

Queue-only support, keeping legacy Scope/Targets unchanged. Reuse
ActivateMonsterWipeCommand, EffectContext, existing reservation and
TryPlanRemovalLinks. Select both players' face-up monsters with at least one
live equipment link (source and target reference, face-up source).
The selector emits one candidate per monster regardless of link count.
Admission and execution both recompute candidates. Existing guards require
a legal source, tracked occurrences, at least one eligible monster and
consistent cleanup plan. The same batch spends the source, destroys each
monster and destroys its linked equipment separately before finalization.
Keep existing sandbox ordering (side 0 then side 1, slots ascending);
do not infer original visual order. No new mutation path or queue.

Tests: both active sides, Hand/Set, three linked equips on two monsters,
unlinked monsters survive, each destroy command once, presentation gate,
legacy bypass rejected, no targets and stale equipment occurrence.
No .NET SDK in chat environment; rely on required GitHub CI before merge.
Done limited to the modeled headless equipment scope, not unimplemented
equip-like traps, ownership/control changes, chains or negation. No Verified.
