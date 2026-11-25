# P051/P052 — banishment cost and 800 LP FLIP damage

## Sources and confidence

- Joey resource IDs **1435** (Lady Assailant of Flames; internal DAT
  index **855**, profile P051) and **1436** (Fire Sorcerer; index
  **767**, P052). Both are in Joey's pack (mask 4).
- SQLite original `00584E60` is decompiled and branches
  `0x59B` / `0x59C` to call `005784E0(opponent,800)`, only if
  `request.flags & 4 == 0`.
- Availability `00582E60` and `00582EA0`, preparations
  `0057DDD0` and `0057DDF0`, and the hand-mode consumer are
  **not decompiled in the supplied SQLite**. The existing static
  sweep `docs/research/card-static-sweep/01-lp-destruicao-respostas.jsonl`
  reports deck≥3, hand≥2 and banishment paths
  `00578B80` → command `0x62`, `005BCEA0` → mode 4.
- Historical DAT texts and official card descriptions distinguish:
  top three cards of own Deck (Lady Assailant); two random cards
  from own Hand (Fire Sorcerer), each followed by 800 damage.
- Static evidence is **not** runtime confirmation of RNG, action
  order, chain/cancel semantics, or enemy-side selection.

## Bounded Core model

`FlipBanishBurnRules` separates **cost selection** from shared
**damage**, with two source-zone policies and an atomic
precondition: if the required number of cards is unavailable,
the sandbox does not banish anything or inflict damage.

`DemoDuel.Banished(player)` retains original Card occurrences
in a distinct out-of-play zone. A banish is not a draw, discard,
mill, or Graveyard transfer. The deterministic `effectRandom`
provides stable synthetic tests, **not native RNG parity**.

`DemoDuel.TriggerFlip` uses the existing FLIP dispatcher and
Royal Command gate. When a valid cost exists, it resolves the
banishment then applies 800 damage through the existing LP path.
The effect is synchronous within sandbox; request chains,
response timing, and native cost cancellation remain outside
this implementation.

`CpuDuelController.FlipUseful` checks that the correct source
zone can pay the full cost. The existing FLIP reveal, Flip Summon,
and deferred battle continuations are reused.

## Regressions

`src/tests/Joey.Tests/FlipBanishBurnChecks.cs` covers resource-ID
vs DAT-index confusion, deck order, hand-only RNG, insufficient
costs, duplicate hand occurrences, player ownership, no Graveyard
movement, reveal once, no face-up position trigger, Royal Command
suppression, battle resolution, and lethal LP damage.

Status **Done** for bounded sandbox after .NET 10 build and **3,311 synthetic
checks passed**, including 16 new P051/P052 checks, on PR #58. Even if marked Done
after the sandbox suite passes, neither card is Verified without
a native capture under controlled decks and matching initial
states, action sequence, and RNG observation.

## Native follow-up

The cumulative SQLite includes the bundle from
`scripts/completed/07-flip-banish-burn-ghidra.sh`, which requested
`00582E60`, `00582EA0`, `0057DDD0`, `0057DDF0`,
`005BCEA0`, `005BCC70`, `00578B80` and the command
`0x62` consumer. Confirm whether insufficient cost skips
damage, and the exact hand RNG selection/removal order.
