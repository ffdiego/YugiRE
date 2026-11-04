# YugiRE Native Rules — dual card experiment

This replaces **two individual card-table primary pointers** for the exact
`joey_pc.exe` fingerprint documented in `pe_preflight.py`. It does **not**
modify the shared original callback function or the EXE on disk.

- `include/yugire/card_request.hpp` — deliberately limited 20-byte request view;
  the only interpreted bytes are offsets +4 and +6.
- `include/yugire/engine.hpp`, `src/engine.cpp` — link to Joey's native LP
  command generator by RVA, and per-card counters.
- `src/cards/sparks.cpp` — Sparks C++ implementation.
- `src/cards/ookazi.cpp` — Ookazi C++ implementation.
- `src/exports.cpp`, `yugire_rules.def` — stable `__cdecl` exports without
  decorated names; the loader calls the exports with Frida `mscdecl` ABI.
- `../yugire_dual_loader.js` — Frida-only memory table adapter (two pointers).
- `../patch_dual_rules.py` — Python runner, EXE hash check, DLL PE32 check,
  status display, and cleanup.

## Build on Visual Studio 2026 Developer PowerShell

From `experiments\hybrid_probe`:

```powershell
cmake -S .\native_rules -B .\native_rules\build-vs2026 -G "Visual Studio 18 2026" -A Win32
cmake --build .\native_rules\build-vs2026 --config Release
```

The output should be `native_rules\build-vs2026\Release\yugire_rules.dll`.
Verify with `dumpbin /headers`, `dumpbin /imports`, `dumpbin /exports`.

## Test on a **new Joey process**

Back up save data; do not run with any other Frida callback patcher attached.
Play a duel normally rather than playing a saved YDR.

```powershell
$joeyPid = (Get-Process joey_pc).Id
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --pid $joeyPid --sparks 300 --ookazi 900
```

If you use another build folder:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --pid $joeyPid --sparks 300 --ookazi 900 --dll .\native_rules\build\Release\yugire_rules.dll
```

The two cards should cause 300 and 900 LP, respectively, when the effect is
allowed. Press **Enter** to inspect counters for each card, then type **stop**.
The original primary pointers should both reappear in the final status.

**Safer initial control:** test default values `--sparks 200 --ookazi 800`
first, then `300/900`. The 200/300 Sparks behavior has previously been tested
with an earlier DLL. **This new two-card DLL has not yet been run inside Joey.**

The DLL remains loaded until Joey closes, even after pointers are restored,
so in-flight callbacks never jump into unloaded code. If the Python/Frida
process exits abnormally, **close Joey before any further patch test**.

## Scope and known limitations

- Only primary callbacks `+4` of table rows 66 and 69 are redirected.
- The `+20` target-preparation callbacks remain original.
- Both original entries share `0x00584E60`; it is not modified directly.
- The engine helper `0x005784E0` emits the LP command; it and its consumers
  remain original.
- The test does not prove behavior under negation, all effect flags, replays,
  multiple simultaneous copies, or interaction with other callbacks.
- Do not invoke `YugiRE_Initialize` after installing pointers, or reset
  counters during an in-flight callback.
- Restore is not a thread-suspending atomic operation; avoid patch/unpatch
  during effect resolution. Stop after the effect finishes, not mid-animation.

## Expected outputs

```text
[dual-rules] Both primary callback pointers installed.
[dual-rules] Sparks: calls=0 commands=0 damage=300 ...
[dual-rules] Ookazi: calls=0 commands=0 damage=900 ...
...normal duel...
[dual-rules] Sparks: calls=1 commands=1 damage=300 ...
[dual-rules] Ookazi: calls=1 commands=1 damage=900 ...
[dual-rules] Restore completed; table pointers:
[dual-rules] Sparks: ... currentPointer=0x584e60
[dual-rules] Ookazi: ... currentPointer=0x584e60
```

Counts may be greater than one if the game invokes callbacks more than once;
they count actual calls and commands, not completed damage effects.
