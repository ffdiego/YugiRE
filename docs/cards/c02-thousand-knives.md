# C02.6 — Thousand Knives (1211/P077)

SQLite cumulativo v2, read-only, quick_check ok, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
card_identities (1211,329,Thousand Knives,22); card_callbacks (1211,P077,305);
pack [1211,6,2]. P077 candidate 0057B680 and availability 00581F40 absent
from all three decompilation tables and exact instruction window addresses.
Primary 00585C80 is the same function as Tribute to The Doomed;
it checks flag 4, resolves exact selected slot and generation via 00583470,
checks occupancy, calls 00583510 for resource IDs 0x3ff and 0x4bb,
then destroys through 00577880 / 00576940. Target preparation 005944D0
is shared too. 00583510 has special branch for target ID 0x4b1; full
target protection remains unmodeled. No native comparison.

DAT inventory already versioned in
docs/research/card-static-sweep/14-monstros-normais.jsonl confirms all three
Dark Magician resource IDs 34,1210,2034 (1210 has pack_mask=0).
Official effect: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5166&ope=2&request_locale=en
Own face-up Dark Magician is an activation condition, not a tribute or
discard cost. Select one opponent monster, including hidden. Exact
activation legality rechecked before source spend; after spend do not
retarget or require Dark Magician to remain. Hand needs free S/T slot,
Set can activate in Main Phase if unlinked. No legacy activation.
New ActivateConditionalMonsterSpellCommand shares reservation and
ResolveTargetedMonsterSpellCommand with Tribute, which retains its
explicit DiscardCommand. Existing standalone DestroyCommand handles
linked equipment and deferred searches. No duplicate destruction path.

Tests: three identities x both sides x Hand/Set, hidden opposing target,
no discard, wrong side, absent/hidden/opponent-only magician, stale
condition before spend, stale target after spend and presentation gate.
Prior Tribute regression retained. Done in bounded headless scope after
CI, no UI/CPU, general chains or native special target protection.
