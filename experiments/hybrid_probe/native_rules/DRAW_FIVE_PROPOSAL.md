# Five-card draw experiment: implementation status

The design in this document was implemented as a **mode-selectable C++ callback**
for Sparks and Raimei. See [native_rules/README.md](README.md) for build and
test instructions.

The new modes are `damage`, `draw` and `damage-draw` (default `damage`);
each card is configurable separately. The draw amount is fixed to five.

Native code:
- `0x005784E0(side, damage)`: produces a damage command.
- `0x00578B60(side, 5)`: produces five native draw commands.
- The activating side is inferred as `request[2] & 1` from the original
  damage routine's opponent-side derivation. Unlike the damage target at +6,
  this should be the correct recipient of the draw under ordinary activation.

The first live experiment requested by the operator is **Sparks draw-only**
and **Raimei damage then draw-five**.

## Important caveats

This implementation has not yet been validated during a real draw resolution.
Counters indicate invocation/queueing, not that five cards arrived in the
correct hand. The order of queue submission is damage then draw, but actual
visual presentation depends on the original engine.

Test while there are at least 10–15 cards in the activating player's deck.
The engine may trigger a deck-out defeat if too many cards are requested.
Do not directly modify deck or hand arrays. Preserve the earlier damage-only
mode for regression. Check side effects and hand/deck counts in the game.

Static source: `docs/21-native-card-draw.md` and original Pot of Greed
callback `docs/evidence/pot-of-greed-draw-callback.txt`.
