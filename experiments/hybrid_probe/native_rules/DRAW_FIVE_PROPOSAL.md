# Proposed next experiment: draw 5 after activating Sparks or Raimei

This is a **design proposal, not part of the current two-card callback
replacement**. The first experiment should keep the known damage effects
unchanged (Sparks 300 LP, Raimei 400 LP).

## Already reverse-engineered engine paths

- `0x00578B60(side, count)` delegates to `0x00578860(side, count, 0)`.
- The latter emits `0x61` draw commands, processed asynchronously by the
  original queue and consumer `0x00571E30`. The processor ultimately
  removes cards from the deck and inserts them in the hand.
- `0x005887C0`, used by Pot of Greed / Jar of Greed / Skelengel, reads a
  side from request offset +6 and calls `0x00578B60`.
- The existing Sparks/Raimei target-preparation slot `0x00593CC0`
  prepares the *opponent* for damage. Its request+6 value must **not**
  blindly be reused as the recipient of a draw effect.

See `docs/21-native-card-draw.md` for exact static trace.

## Suggested safe phased test

1. Confirm the unchanged two-card DLL works with damage-only.
2. Trace which request field identifies the *controller of the activated
   spell*. Prior evidence suggests `request[2] & 1` is the side of the
   requester in the current path, but verify with a live trace and the
   actual player hand before relying on it.
3. Create a separate optional `--mode draw-five` for just ONE test card
   (Raimei initially), instead of replacing the existing damage mode.
4. Follow the callback/queue ABI and verify that the draw producer can be
   invoked safely during a card callback. Do not mutate the hand or deck
   arrays directly.
5. Initially test on a deck with at least 5 cards remaining, record both
   sides' hand/deck counts, and check that the original command queue
   resolves without interrupting gameplay.
6. Only later test deck exhaustion, anti-draw effects, chains, and multiple
   activations. Empty deck can trigger a terminal condition, so it is not
   a trivial failure mode.

The objective is to demonstrate **cross-mechanism reuse**: a card previously
resolved by a damage callback invokes the existing native drawing producer.
Exact behavior must be verified in-game; the static findings are insufficient
to assert all draw and presentation side effects will be correct.
