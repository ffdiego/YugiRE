# C02.7 — Fissure (1001/P114)

SQLite cumulativo v2 read-only, quick_check ok, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
card_identities (1001,419,Fissure,22); card_callbacks (1001,P114,166);
pack [1001,1,2] (Yugi only). P114 singleton.
Candidate 0057B180 and primary 005883C0: no rows in decompilations,
visual_transient_decompilations, visual_evidence_functions, or exact
visual_evidence_instructions window_address in v2. No native ordering
or tie rule recovered.

Official rule: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4835&ope=2&request_locale=en
Destroy one face-up opposing monster with the lowest ATK; player's
choice if tied. Compare live AttackValue, including modeled equipment,
field and aura contributions, regardless of face-up battle position.
Exclude hidden and own monsters; rank all eligible visible slots before
filtering trackable occurrences so an invalid minimum cannot promote a
higher-ATK candidate.

Use the existing queue-only no-discard activation and targeted destruction
reservation. Generic TryQueueActivation selects a unique minimum
automatically. On a tie require explicit choice through
TryQueueTargetedMonsterSpell; never choose by slot order. Selection
submitted before spend in bounded no-chain sandbox, not a claim that
Fissure targets at activation in TCG/native. Recheck criterion before
spend and at resolution; stale occurrence/criterion after spend skips
rather than silently retargeting. The actual non-targeting selection at
chain resolution is deferred to ACTIONS, as is UI/CPU.
Reuse DestroyCommand for equipment and field-to-graveyard consequences.

Tests: both sides, Hand/Set, equipped printed minimum no longer lowest,
hidden lower printed ATK, face-up defense eligible, own/higher/hidden
rejection, gate, both tie choices, no candidates and stale face state
before/after spend. No native Verified. Script 30 requests only missing
P114/P074/P077 callback bodies, not the complete export again.
