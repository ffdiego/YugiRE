# C02.3 — Giant Trunade (1080/P152)

SQLite cumulativo v2 read-only: SHA-256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check ok; executable_sha256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
card_identities and card_callbacks confirm 1080 Giant Trunade, class 22, P152.
Candidate 0057B530 present in visual_transient_decompilations and visual_evidence_functions:
accepts occupied native slots 5..10, excludes source side/slot. Primary
00589E60 absent in all three decompilation tables queried. Candidate does
not establish return order or complete effect. Official card text:
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4910&ope=2&request_locale=en

Queue-only mass return; leave legacy Supports/HitsOwnSide untouched.
Existing TryPlanSpellTrapWipe/ActivateSpellTrapWipeCommand reserves tracked
occurrences on both sides (opponent then own; S/T then FIELD, sandbox
ordering only). Spend source first; cancellation on failed spend is
inherited. Emit one ReturnToHandCommand per reserved target, not Destroy.
Reuse ReturnQueuedSpellTrap and ReturnSpellTrapCardToOwnerHand from Tornado
Bird; unlink modeled equips and clear FIELD flags. Existing FLIP guard is
retained. For a full mass return, do not apply Tornado Bird's selective
Destiny Board cascade (all pieces are reserved for return). No new queue.
Tests cover both sides, Hand/Set, equipment link, FIELD, hidden S/T, stale
source/target, one movement per occurrence and presentation gate.
No SDK/DAT in chat; require CI before merge. Done limited to current
headless modeled zones; control changes, other continuous/link effects,
responses and native return sequence not verified.

## Search triage

Fusion Sage's simple mandatory single search does not cover Last Will
(P138, conditional Special Summon), Painful Choice (P153, multi-card
opponent choice and discard/send), or Thunder Dragon (P064, discard self
and up to two same-name copies). Keep those independent recortes; do
not broaden the mandatory Spell search without modeling their costs/gates.
