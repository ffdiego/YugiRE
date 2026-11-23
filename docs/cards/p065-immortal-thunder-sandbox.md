# P065 — The Immortal of Thunder (resource ID 461)

## Native evidence and limitations

The SQLite inventories card identity `461` (`0x1CD`), DAT index
`164`, class code `19` and primary callback `00585970`.
It also catalogs availability `00581040` and preparation
`0057C5F0`. **None of these three are decompiled** in the
supplied database.

Decompiled `00567620` classifies ID `0x1CD` as FLIP.
`00576F40` and `00577CF0` distinguish the same resource
and test state bits on the occurrence, which suggests the
native engine does more than check face-up presence.
Their exact mapping to the 5000 LP penalty is **not proven**.

The historical printed effect has an explicit 'after this card
is flipped' condition on the later 5000 LP loss. The current
official Konami TCG database shows the FLIP gain and field-to-GY
loss but does not repeat that qualification. We implement the
**historical conditioned variant** to keep the bounded sandbox
aligned with the older game as a hypothesis, **not a native
behavior assertion**.

## C# design

`ImmortalThunderRules` isolates two operations:
- `Gain3000LifePoints` in `DemoDuel.TriggerFlip`, subject to
  the shared Royal Command gate;
- `LosesLifeOnFieldToGraveyard` in `DemoDuel.RemoveMonster`,
  guarded by `MonsterWasFlipped[player,zone]` for the *same*
  physical field occurrence.

Moving a monster out of the field clears that occurrence's
history, even if the same card instance returns to a slot later.
`ReturnMonsterToHand` does not invoke `RemoveMonster` and
cannot cause this loss. Fusion Gate explicitly banishes materials
instead of moving them to Graveyard; other fusions and Tribute
Summons use `RemoveMonster` and can trigger it.

The LP loss uses `ChangeLife(owner,-5000)` rather than the
damage helper: **losing LP is not damage**. The synthetic
demo determines a winner on zero LP.

## Tests and caveats

`ImmortalThunderChecks.cs` covers resource/identity, +3000 on
Flip Summon and effect reveal, field-to-GY loss after flip in
battle, Dark Hole, tribute, multiple occurrences, lethal loss,
opposing side, negative cases of never-flipped Set / face-up
summon, Hane-Hane bounce, and Royal Command FLIP negation.

No proof yet for whether the *second* LP loss is negated when
Royal Command prevented the preceding FLIP effect, nor for
owner/controller divergence, change-of-control, native chain
order, counter traps or exact timing relative to simultaneous
field removals.

**Status: Done no escopo do sandbox histórico; não Verified.** O build
.NET 10 e **3.325 verificações sintéticas** passaram no CI do PR #59,
incluindo **14 verificações P065**. Ainda não há comparação com o Joey original.

## Pending native extraction

`scripts/pending/08-immortal-thunder-graveyard-ghidra.sh`
will request assembly/decomp of `00581040`, `0057C5F0`,
`00585970`, `00576F40`, `00577CF0`, `0056AF90`,
`005784E0` and callback / card movement queue helpers.

Prioritize a controlled native duel with a never-flipped
face-up Immortal sent to the Graveyard, then one flipped by
battle, and a Flip Summon under Royal Command. Compare LP
changes and the source-occurrence state at each point.
