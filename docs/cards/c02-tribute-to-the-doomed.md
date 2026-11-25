# C02.5 — Tribute to The Doomed (1023/P074)

SQLite cumulativo v2, read-only, quick_check ok, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
card_identities (1023,363,Tribute to The Doomed,22); card_callbacks
(1023,P074,187); pack [1023,6,2]. P074 singleton.
Official rule: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4856&ope=2&request_locale=en

Native preparation 0057D7C0: switch on resource ID 0x3ff selects count 1,
calls 005BCE00(controller,1,0,0). 005BCE00 calls 005BCC70 with
operation 2 and flags; consistent with a one-card payment, not proof
of precise native discard/target sequencing. Primary 00585C80 checks
flag 4, obtains selected occurrence via 00583470 (slot and generation),
tests nonempty slot and calls 00583510 for 0x3ff/0x4bb, then 00577880
(single target mask passed to 00576940). 00583510 has a conditional
target-specific branch for resource 0x4b1; no full protection semantics
claimed here. Target preparation 005944D0 recovered in transient and
visual functions: selects human/CPU path through 00593440 or 005B3FA0.
Availability 00580D20 and candidate 0057A9A0 absent in the three
decompilation tables queried. No native trace.

## Bounded queue contract

Discard one other card in own hand, then destroy one monster on either
side, face-up or face-down. Activation from Hand requires a free Spell
slot, or from an unlinked Set. Read-only independent candidate views;
never expose hidden enemy identities in UI. Admission validates all
three distinct occurrences and does not mutate. Producer reserves
activation; DiscardCommand pays cost (rechecking source/cost/target);
SendToGraveyardCommand spends spell only after applied discard;
ResolveTargetedMonsterSpellCommand verifies both results and live target.
Then existing standalone DestroyCommand executes with link cleanup
and deferred field-to-graveyard triggers. No new destruction primitive.
If source/target changed before payment, no cost; after payment,
a vanished target is not replaced and cost is not refunded. No legacy
activation or generic activation without selections.

Tests cover both controllers, Hand/Set, own/enemy target, hidden
candidate, duplicate spell copies, exact costs, invalid cost, gate,
equipment cleanup, stale source and target. Done only for headless
bounded scope after CI. Interactive choice order and chain semantics
(including target protection, discard-trigger/replacement effects)
remain pending, no native parity claim. The API submits both choices
before payment; original selection UI order is not asserted.
