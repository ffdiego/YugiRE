# P188 — Dimensional Warrior: banish both combatants after damage calculation

## Basis and limits

Original Joey pack includes **Dimensional Warrior**, resource ID `255`,
DAT index `46`, profile `P188`. `card_callback_slots` identifies
native primary `0058CC00`, but **this address is absent from
the Ghidra SQLite decompilations**.

The original 2004 card list describes removing both the attacking
and defending monsters from play when a Dimensional Warrior attacks
or is attacked. Later D.D. Warrior rulings place the effect **after
damage calculation**, including when a combatant would otherwise
be destroyed by battle. These are references for the sandbox
contract, **not a dynamic test of Joey the Passion**.

## Design

`DimensionalWarriorRules` identifies the effect by resource ID,
not DAT index. The existing `DemoDuel.FinishDeferredBattle`
is the single post-damage point across ordinary and face-down
defender battles. It first verifies **both original Card objects**
are still present in their respective slots.

A qualifying pair is transferred to the already shared
`DemoFusion.banished` out-of-play zone, with `ClearMonsterSlot`
cleanup (including attached equipments). Neither is placed in
Graveyard, so Sangan/Witch, Giant Germ/Nimble Momonga, Spear
Cretin and The Immortal of Thunder do **not** trigger Graveyard
effects merely because they battled this monster.

Normal damage calculation is not skipped, and each side still
takes the usual applicable battle damage. A direct attack has
no opposing monster and cannot trigger a pair removal.

Mirror Force and other attack-declaration negations run before
damage calculation and stop this post-damage resolution.

If either original combatant leaves before the deferred
continuation, the sandbox does **not** banish an unrelated
replacement card. Full native effect/chain resolution,
situations where only one original combatant remains, control
changes and replacement effects are pending.

## Regression coverage

`DimensionalWarriorChecks.cs` exercises attacking/defending,
face-down DEF, reflected damage, both P188 together, damage
vs banish, suppression-independent effect, Sangan, Giant Germ,
Spear Cretin and Immortal Thunder negative Graveyard triggers,
direct attack, ordinary combat and Mirror Force cancellation.

Status **Done no sandbox** após build .NET 10 e **3.340 verificações C#**
aprovadas no PR #60, incluindo 15 casos de P188. Passing CI certifies
only this bounded sandbox behavior. **Verified** still requires
an observed original-executable duel and suitable Ghidra
callback/dispatch analysis.

## Pending Ghidra

The queued ninth script exports `0058CC00` and surrounding
battle dispatcher/command consumers for source/target ordering,
post-damage timing and interaction with flips or other effects.
No Ghidra extraction was executed in this session.
