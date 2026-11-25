# C02.8 — Offerings to the Doomed (1451/P105)

SQLite cumulativo v2 read-only, quick_check ok, SHA-256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
card_identities (1451,362,Offerings to the Doomed,22);
card_callbacks (1451,P105,399); pack [1451,4,2].
Primary 00587950, candidate 0057AE20: no rows in the three decompilation
tables or exact instruction windows. Target preparation 005944D0
present in transient and visual functions and 1536 instruction rows.
No native phase-replacement implementation inferred from absent bodies.
Script 30 amended with just these two missing addresses; no native assets committed.

Official card text: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5214&ope=2&request_locale=en
Historical rulings: https://www.goatformat.com/rulings5.html
Target one face-up monster on either side; destroy and independently skip
the activating player's next Draw Phase. Multiple resolved copies in the
same turn skip one phase. If the target becomes face-down after activation,
destruction fails but skip still applies.

Bounded queue: own Main Phase only, from Hand with free S/T slot or
prior-turn unlinked Set (Quick-Play timing); no legacy activation.
Reuse conditional spell producer, shared source spend and resolution.
After successful spend, resolution registers the owner's next due turn
even if the target is no longer eligible. DestroyCommand if still valid;
no retarget. No skip if activation rejected or source unspent. A
side-aware nullable due turn coalesces copies; only shared TakeTurnDraw
consumes it before empty-deck loss, sets drawn without transferring
a card, and retains the legacy/queued Standby hook. DrawCommand and
other card draws never consult skip state. AdvanceTurn expires an unused
due turn because the sandbox permits manual phase bypass. Do not treat
this as a full phase engine. General Quick-Play windows, chains, UI/CPU
and native skip presentation remain ACTIONS dependencies.

Tests: both sides, hand/prior-turn Set, own/adversary, face-down
rejection, current-turn Set rejection, other-player draw, effect draw,
one skip per next Draw Phase, repeated copies, target hidden after
spend, empty deck, subsequent normal draw, unspent source, skipped
manual opportunity expiry, and paid Revival Jam Standby on a skipped
Draw Phase. Done bounded to headless own Main after CI, not Verified.
