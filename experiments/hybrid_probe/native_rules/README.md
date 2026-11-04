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
Validate exports with `dumpbin /exports`; the DLL ABI is **version 2** and
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

## Offline checks

```powershell
python .\native_rules\tests\test_catalog.py
python .\native_rules\tests\test_damage_limits.py
node .\native_rules\tests\test_loader.cjs
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
