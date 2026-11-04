# YugiRE Native Rules — Sparks + Raimei

Two-card proof of concept for the exact original `joey_pc.exe` fingerprint validated
by `experiments/hybrid_probe/pe_preflight.py`.

**This DLL version has NOT yet been validated inside Joey.** Prior tests established
Sparks (200/300 LP) from an earlier single-card C++ DLL.
The operator subsequently confirmed activating BOTH cards in the dual-C++ test,
but did not supply exact observed damage/counters for that run.

| Card | Internal index | Resource ID | Callback row | Original damage | Experiment damage |
| --- | ---: | ---: | ---: | ---: | ---: |
| Sparks | 755 | 342 | 66 | 200 LP | 300 LP |
| Raimei | 1049 | 1007 | 172 | 300 LP | 400 LP |

Both cards share the original main callback `0x00584E60` and target-preparation
`0x00593CC0`. Only the primary pointer (+4) of each individual table record
is redirected. The shared original callback is not patched.

## Project layout

- `include/yugire/card_request.hpp`: 20-byte request, only fields +4 and +6 identified here.
- `include/yugire/engine.hpp`, `src/engine.cpp`: engine adapter, per-card call
  and emission counters, native damage-command producer `0x005784E0`.
- `src/cards/sparks.cpp` and `src/cards/raimei.cpp`: independent rules.
- `src/exports.cpp`, `yugire_rules.def`: undecorated C ABI exports,
  called by the Frida adapter with `mscdecl` on x86.
- `../yugire_dual_loader.js`: installs/restores both pointers with rollback.
- `../patch_dual_rules.py`: loaded-EXE hash validation and Python host.

## Compile (Visual Studio 2026 Developer PowerShell)

In `experiments\hybrid_probe`:

```powershell
cmake -S .\native_rules -B .\native_rules\build-vs2026 -G "Visual Studio 18 2026" -A Win32
cmake --build .\native_rules\build-vs2026 --config Release
```

Output: `native_rules\build-vs2026\Release\yugire_rules.dll`.

To validate exports: `dumpbin /exports .\native_rules\build-vs2026\Release\yugire_rules.dll`.
You should find `YugiRE_Sparks` and `YugiRE_Raimei`.

## Test in a NEW Joey process

Before launching the game, back up the save; do not attach another Frida
callback patcher simultaneously. From `experiments\hybrid_probe`:

```powershell
$joeyPid = (Get-Process joey_pc).Id
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --pid $joeyPid --sparks 300 --raimei 400
```

When the game lets you play Sparks and Raimei, observe **300 LP** and **400 LP**
damage respectively. Both activations were confirmed in the previous dual-card
test; the new configurable damage range is not yet validated inside Joey. During the duel, press Enter in the
host to inspect the counters. Type `stop` AFTER the effects have finished
to restore both callback pointers. Expected restored pointer for both:
`0x00584E60` (in the original image base).

For original-rule comparison, choose `--sparks 200 --raimei 300`.

**Important:** catalogued in the game files does not imply unlocked in your
current save, or visible under every collection filter. If Raimei is missing,
check whether its card-list filters/collection availability permit adding it
to the deck; do not patch the save during the callback test.

Other build folder? Pass `--dll .\native_rules\build\Release\yugire_rules.dll`.

## Limitations and safety

The DLL is not unloaded while Joey is running. The loader checks table IDs,
other slots, original pointers, executable hash, x86 architecture, and required
exports; it attempts to restore both slots on exit and after partial installation.
An abnormal Frida exit can leave code redirected until Joey terminates.
Always close Joey after a failed or interrupted patch before retrying.

The tests cover neither all card interactions nor effect suppression, chaining,
multiple copies, YDR replay execution, or simultaneous callback execution. The
engine's queue/consumer stays original.

## Offline checks

```powershell
python .\native_rules\tests\test_catalog.py
node .\native_rules\tests\test_loader.cjs
```

The catalogue check verifies both indices/IDs/profile P045. The JavaScript
fixture checks two distinct pointers, configuration, restoration and rollback.
These are structural tests; they do not substitute for the Windows test.


## High damage / one-hit-KO experiment

The Python host, Frida loader and C++ DLL now accept **any integer from 1 to
9999 LP**, independently for Sparks and Raimei. Values outside that range,
fractions and non-numeric strings are rejected. Default values stay 200/300 LP.

For a standard duel starting at **8000 LP**, the first high-damage experiment
can be 8000 LP for either or both cards:

```powershell
# Restart Joey before testing a freshly rebuilt DLL.
$joeyPid = (Get-Process joey_pc).Id
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --pid $joeyPid --sparks 8000 --raimei 8000
```

This should be enough to reduce a full-8000-LP opponent to zero **if** the
effect is permitted to resolve and the game's damage consumer handles this
magnitude as expected. This is NOT yet experimentally confirmed.

**Do not test values above 9999** until the LP command encoding, saturation,
and terminal-condition handling have been validated. The helper accepts
32-bit values, but its consumers may narrow them or apply other rules.
In particular, a larger numeric argument does not guarantee a valid
victory result or stable graphics. Keep backed-up saves, wait until effect
resolution finishes, inspect both LP displays and the final game result, and
type `stop` to restore the original callbacks. The same loader can still be
run with 200/300 to test the original damages.

Offline tests:
```powershell
python .\native_rules\tests\test_damage_limits.py
node .\native_rules\tests\test_loader.cjs
```
