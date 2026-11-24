# C02.2 — Fusion Sage (1040/P134)

SQLite cumulativo v2 (SHA-256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4),
quick_check ok, executable_sha256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
card_identities: 1040, index 1029, Fusion Sage, class 22.
P134 availability 00581040 (transient and visual evidence decompilation: return 1);
primary 005892D0 absent in decompilations, transient and visual evidence functions.
Polymerization has IDs 1003/index 1026 and 1034/index 1027, both class 22.
Pack-index [1040,4,2] and [1003,7,2]; 1034 absent from pack union but supported by FusionRules.
Official effect: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4872&ope=2&request_locale=en
No native callback resolution or exact native timing claimed.

Activation: legal Main Phase, tracked Hand/Set, free Spell slot if Hand,
at least one eligible Polymerization in own Deck. New typed producer reserves
source, then SendToGraveyardCommand, then OpenDeckSearchSpellCommand after
spend gate. Open verifies successful spend and rebuilds candidate view.
Mandatory PendingCardSelection with new Polymerization filter using
FusionRules.Supports (not a fuzzy name match). ResolveDeckSearchCommand
already checks exact request, current occurrence and owner; existing movement
and shuffle reservation executes MoveToHandCommand, then ShuffleDeckCommand
only on success, then FinishDeckSearchCommand. No legacy ResolveCardSelection
bypass; no second scheduler or duplicate deck movement.

Tests cover both sides, Hand/Set, duplicates, alternate ID, wrong class,
Fusion Gate, mandatory selection, gate, no draw event, stale spend, no
eligible card, exact occurrence and no repeat. UI/CPU and response windows
not implemented here; reveal to opponent and native shuffle/availability
policy remain unverified. Done only for bounded headless sandbox after CI.
