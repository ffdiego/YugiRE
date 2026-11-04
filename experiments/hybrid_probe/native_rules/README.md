# YugiRE C++ dual-card rules — Sparks + Raimei

Experimental selective callbacks for the *exact* Joey executable fingerprint
checked by `../pe_preflight.py`. The DLL does not patch the original executable,
and the Frida adapter redirects only the two per-card primary callback slots.

## Cards and callback ABI

| Card | Internal card index | Resource ID | Table row | Original damage |
|---|---:|---:|---:|---:|
| Sparks | 755 | 342 | 66 | 200 LP |
| Raimei | 1049 | 1007 | 172 | 300 LP |

Both rows originally point to the shared `0x00584E60` callback and use
`0x00593CC0` for opponent target preparation. The function-pointer table
remains intact except for the two primary pointers while the patch is active.

Both compiled C++ functions now support **three independent modes**:

| Mode | Meaning |
|---|---|
| `damage` | Queue the configured damage (previous behavior; default) |
| `draw` | Queue **five cards for the activating player**, no damage |
| `damage-draw` | Queue damage, **then** queue five cards for the activating player |

The damage value remains configurable from 1 to 9999 LP via
`--sparks` and `--raimei`, with defaults 200 and 300. In draw-only
mode, the corresponding damage amount is accepted but unused.

The C++ rules invoke two native functions:
- **`0x005784E0(targetSide, damage)`** — the previously tested LP-change
  command producer.
- **`0x00578B60(actorSide, 5)`** — the same draw-command producer called
  by the original Pot of Greed / Skelengel / Jar of Greed path. The
  queue then uses original `0x61` command handling; card lists are not
  modified manually by YugiRE.

### Why the draw recipient is different

The existing Sparks/Raimei target-preparation callback selects the
**opponent** in request offset +6. The original shared damage callback
derives the opponent as `1 - (request[2] & 1)`, so the draw recipient
is tentatively derived as `request[2] & 1`, the **activating player**.
This interpretation is grounded in the original damage callback but
**the new five-card behavior has not been validated in a live duel**.
Test who actually receives the cards before relying on the mapping in
unusual activation modes.

## Build on Visual Studio 2026 Developer PowerShell

Run from `experiments\hybrid_probe`:

```powershell
cmake -S .\native_rules -B .\native_rules\build-vs2026 -G "Visual Studio 18 2026" -A Win32
cmake --build .\native_rules\build-vs2026 --config Release
ctest --test-dir .\native_rules\build-vs2026 -C Release --output-on-failure
```

Output: `native_rules\build-vs2026\Release\yugire_rules.dll`.
Validate exports with `dumpbin /exports`; the DLL ABI is **version 3** and
must include `YugiRE_GetConfiguredMode` and `YugiRE_GetDrawBatchCount`.
The Frida loader rejects an older version of the DLL.

## First in-game test: Sparks draws, Raimei damages AND draws

Make a backup of your save, close any previous Joey process, relaunch it
and use a **normal live duel**, not a replay. Put both cards in your deck
and ensure there are **at least 10–15 cards still in your deck** before
each activation to avoid accidentally testing a deck-out defeat.

```powershell
$joeyPid = (Get-Process joey_pc).Id
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py `
    --pid $joeyPid `
    --sparks-mode draw `
    --raimei-mode damage-draw `
    --raimei 400
```

Expected **if the engine produces and resolves the queued draw commands**:

- **Sparks**: no damage, *five* cards drawn by the activating player.
- **Raimei**: 400 LP of damage to the opponent, then *five* cards drawn by
  the activating player.
- Playing a spell itself removes one card from your hand, so the net hand
  change after a successfully resolved five-card draw is **+4**.
- Press **Enter** in the host console to see `calls`,
  `damageCommands`, and `drawBatches` (batch count, NOT the number
  of actually drawn cards).
- Type **stop** only after the effect has completed; both pointers should
  return to their original values. DLL memory stays loaded until exit.

If the wrong player draws or the engine misbehaves, stop the experiment,
restore the pointers and restart Joey. This effect is deliberately experimental;
do not infer that five cards were drawn just because `drawBatches=1`.

## Regression mode (previous behavior)

No extra flags, or explicitly:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py `
    --pid $joeyPid `
    --sparks 300 --raimei 400 `
    --sparks-mode damage --raimei-mode damage
```

Also available: `--sparks-mode damage-draw --raimei-mode draw`, etc.
Unlike the previous version, the *mode* is now decided at installation
without needing another compilation.

## Native C++ EffectContext and interactive terminal commands

The card-specific callbacks are now deliberately tiny and use
`EffectContext::ResolveConfigured(card)`, implemented in
`include/yugire/effect_context.hpp` and `src/effect_context.cpp`.

`EffectContext` provides `Controller()`, `Opponent()`,
`InflictDamage(card)`, and `DrawCards(card, count)`. The actual game's
queue producers remain concentrated in `src/engine.cpp`. Sparks and Raimei
still retain independent modes `damage`, `draw` and `damage-draw`.

### Interactive command while a live duel is running

The same Python host now supports typing the following into its existing
interactive prompt:

```text
[dual-rules] > draw 5
[console] Request 1 pending for side 1.
[dual-rules] >
```

**Live test confirmed the sides in a normal CPU duel:** side 0 = CPU and
side 1 = the human player. The default is now **side 1**, so `draw 5`
draws for the player. Use `draw 5 0` to draw for the CPU. The default
can still be overridden with `--console-side 0` or `--console-side 1`
on the host command line; other duel modes have not been tested for the
same mapping.
The explicit commands are:

- `draw 5`: request a batch of five draws for the configured default side.
- `draw 5 0` or `draw 5 1`: request a batch for that exact player side.
- `status` or Enter: current counters and console request status.
- `cancel`: cancel a request that has NOT yet been submitted to Joey.
- `stop`: cancel pending work, restore Sparks/Raimei callback pointers,
  and detach the duel-update hook.
- `help`: show available commands.

The command is **not executed on Frida's RPC thread**. RPC stages a single
request in the JS agent. On `0x005BC700` (the duel scheduler), the Frida
`Interceptor.onLeave` callback checks that the command queue is idle
(`0x00A558A8`, `0x00A558AC` bit `0x4000`, and the stage byte at
`0x00A5780E`). Then, on that original game thread, it calls the new
C++ DLL export `YugiRE_ConsoleDraw(side, 5)`, which enqueues the original
`0x61` commands through `0x00578B60`. The DLL also exposes
`YugiRE_GetConsoleDrawBatchCount`. The Frida ABI is bumped to **v3**,
so an outdated DLL is rejected.

**The external-console path was validated by the operator in a normal CPU
duel, including repeated requests while the CPU was playing.** Native
draws were reported to resolve and be queued correctly; the full trace
of hand/deck counts and all concurrency conditions has not been collected.
This remains an experimental game-thread injection boundary, not a
formal guarantee for every duel mode.

There is intentionally only **one outstanding console request** at a
time: `A console draw request is already pending` is expected backpressure,
not evidence of a crash or lost queue entries. After the pending request
is accepted by the duel scheduler, another can be sent. Invalid counts
(`draw 6`, `draw 30`) are rejected with a usage message. If a request
remains pending, `status` displays the gate values and `cancel` removes it.

Start by testing in a local duel with at least 15 remaining deck cards,
away from any active animation or card chain. Back up the save and do
not fire commands repeatedly while an earlier one is pending. Do not use
the console during menus, online duels, or replays.

The command path does not require you to play Sparks or Raimei: it is
a standalone console action while the dual patcher remains installed.

## Offline checks

```powershell
python .\native_rules\tests\test_catalog.py
python .\native_rules\tests\test_damage_limits.py
node .\native_rules\tests\test_loader.cjs
python .\native_rules\tests\test_console_commands.py
ctest --test-dir .\native_rules\build-vs2026 -C Release --output-on-failure
```

The JS fixture checks the DLL ABI, independent mode selection, rollback,
restoration and rejection of invalid values. The C++ test exercises the
inferred byte offsets. These tests do **not** execute the original game.

## Safety / limitations

- Only a specified original EXE hash, Win32 x86 process, and verified table
  rows are supported.
- The DLL remains loaded, and patch restoration is not a thread-suspending
  atomic transaction. Do not press `stop` during an animation or resolution.
- The draw producer may trigger other engine systems (e.g. deck-out, draw
  triggers, deck restrictions). These cases and chain/negation handling
  are **not** validated.
- The `request[4] & 4` suppression flag is reused for both kinds of
  effect; other modes/contexts have not been independently tested.
- A null/aborted Frida connection can leave patched pointers active until
  Joey terminates. Restart Joey before retrying a failed experiment.
- The frame/text description of a card does not change: only runtime
  primary-callback effects are replaced. The visual card still says its
  original damage amount.


The console-only draw also verifies the native **remaining deck count** at
`0x00A55D69 + side * 0xD44` before enqueueing. A pending `draw 5` is
rejected when fewer than five cards remain, to avoid triggering an accidental
deck-out. Card callback modes are unchanged and still obey the original
game's rules for deck exhaustion; this extra check applies only to the
console command.

The shared C++ engine's internal draw helper accepts 1–5 cards so additional
card effects can reuse it. The interactive console deliberately remains
restricted to `draw 5` for this initial cross-thread experiment.
