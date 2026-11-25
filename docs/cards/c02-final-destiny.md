# C02.9 — Final Destiny (1067/P144)

SQLite cumulativo v3 read-only, quick_check ok, SHA-256
9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed.
card_identities (1067,319,Final Destiny,22);
card_callbacks (1067,P144,224), pack [1067,2,2] (Kaiba).
Preparation 0057D7C0 is present in decompilations. Resource 0x42b
chooses count=5 then calls 005BCE00(controller,5,0,0). The same switch
chooses 1 for 0x3ff/0x405, 2 for 0x430.
Primary 00589970 and availability 005813D0 absent in all three
decompilation tables and exact instruction windows. Script 30 updated;
its previous eight addresses are also absent in v3. No native order
or full availability inferred from missing bodies.

Card rule: discard five cards as cost, destroy all field cards.
Historical reference: https://www.goatformat.com/rulings3.html
Admission: own Main, Hand/free S/T slot or unlinked Set, five distinct
other tracked hand occurrences, at least one other field occurrence
(conservative empty-field legality). No legacy or arbitrary auto-choice.
Use QueuedFinalDestinyCosts and TryQueueFinalDestiny; immutable five
occurrences survive hand index shifts. Activation reserves five
DiscardCommands, source spend and resolution. Recheck remaining cost
and source before each payment; stop on invalidity, no refund of
already applied movements. Resolution only after all five payments
and successful source spend. Snapshot at resolution, not activation.
Reuse existing monster removal planning, linked equipment cleanup,
deferred searches, and reserved S/T wipe. Monster order is sandbox
order, then remaining S/T including FIELD; each equipment destroyed
once. Source not destroyed twice, unselected hand preserved. No full
chain, cost-trigger/replacement or protection rules asserted.

Tests: both controllers, Hand/Set, five duplicates with distinct IDs,
exact count, source exclusion, payment order, destruction gate, hidden
monster, both fields/traps, linked equipment deduplication, preserved
unselected card, stale source before/during payment, insufficient cost and deferred Sangan search.
Done bounded headless after CI; no UI/CPU or Verified.
