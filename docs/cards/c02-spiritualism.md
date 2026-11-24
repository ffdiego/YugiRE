# C02.4 — Spiritualism (1539/P262)

SQLite cumulativo v2 read-only, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check ok, executable_sha256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
card_identities = (1539,864,Spiritualism,22);
card_callbacks = (1539,P262,427).
card_callback_slots: candidate 0057C250, primary 005912B0,
target_preparation 00597570. None of these addresses appears in
decompilations, visual_transient_decompilations, visual_evidence_functions
or visual_evidence_instructions (window_address). Not a recovered native effect.
Pack index [1539,4,2].

Official card text: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5246&ope=2&request_locale=en
One opposing Spell/Trap returns to hand. Activation and effect cannot
be negated. The sandbox does not have general chain/negation resolution;
preserve this distinction for later ACTIONS integration. Do not bypass
unrelated legal activation gates.

Queue-only: keep legacy Supports unchanged. Existing targeted admission
and read-only candidates accept only opponent's S/T (including FIELD,
face-down or face-up), tracked occurrence, legal Main Phase, hand/free
slot or Set (normal spell, same-turn Set allowed). A targeted producer
spends source, emits ReturnToHandCommand for the primary, then existing
reserved DestroyCommands for any modeled active Destiny Board companions.
Dependents only execute if the primary returned successfully. Reuse
ReturnQueuedSpellTrap; do not return cascade companions or destroy
the primary. Maintain the same existing sandbox ownership/Board assumptions
and gate policy, not native timing or full control-change semantics.

Tests: both sides, hand/Set, each of equip/Set/FIELD, own-only rejection,
source spend before movement, exact identity, gate, no legacy bypass,
unrelated survivors, Board and message individually, stale primary
cancels dependent cleanup. No general chain/negation, UI/CPU or native
differential validation. CI required for bounded Done; not Verified.
