# YugiLab: opt-in deterministic per-duel seed, supported by Ghidra evidence

**Status:** IMPLEMENTED in `experiment/hybrid-native-probe`, validation on Joey original under Windows still required.
Original executable SHA256: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Evidence: `rng-seed-core.txt`, `rng-seed-callers.txt`, `rng-seed-init.txt` provided by the operator on 2026-11-08.

## Verified native behavior

- `FUN_005C3BF5(seed)` calls `FUN_005C6DB3()` and writes `seed` (uint32) to offset `+0x14` of the returned CRT state structure. It is the equivalent of `srand(seed)`.
- `_rand` at `0x005C3C02` uses the same CRT `+0x14` state: `next = (state * 0x343FD + 0x269EC3) mod 2**32`; return `(next >> 16) & 0x7FFF`. Ghidra detects matching MSVC runtime.
- In `FUN_005BFEB0` (duel entry), call at `0x005BFEB8` invokes `FUN_005C3BF5(param_4)`, returning to `0x005BFEBD`. The same function then calls `FUN_005B9E50` (duel reset), which prepares both decks and shuffles them via `FUN_00568D80`.
- OTHER callsites also reset RNG: `FUN_00408B90`, `FUN_00406060`, `FUN_0040AFC0`, and `FUN_00558BF0(0)`. We **do not** replace those arguments. Overriding them would change card-effect logic and behavior beyond the initial duel state.
- `FUN_0044BAA0` simply reads `DAT_005F3284`, so the two shuffle implementations are selected by a mode/version configuration value, not by a direct RNG call.

## Controlled mode, deliberately opt-in

Pass `--seed N` to `python -m yugilab`; zero is valid, max is `0xFFFFFFFF`. Without `--seed`, original calls and output remain unchanged.

In `native_hooks/yugire_dual_loader.js`, `setdemoseed(base,startIndex)` is an RPC invoked **before process resume**. The attached `FUN_005C3BF5` observer changes `args[0]` ONLY when the caller's verified return address is `0x005BFEBD` AND the scene mode is 7. All other `FUN_005C3BF5` callers remain untouched.

For each new duel, `globalIndex = startIndex + localDuelOrdinal`. The effective seed is `mix32-v1((base + globalIndex) mod 2**32)`, a bijective avalanche mix using unsigned shifts and two odd multipliers (`0x7FEB352D`, `0x846CA68B`). Distinct indices across a run produce distinct 32-bit seeds; *this alone does not prove statistical independence of the resulting game outcomes*.

The parallel scheduler divides duels and forwards nonoverlapping `--seed-start-index` offsets; a 23-duel/4-worker run uses global offsets `0,6,12,18`. Thus a fixed master seed should produce the same per-duel seeds independent of worker launch order.

JSONL `metadata.rngSeedOverride` records the base/offset. Each actual override has an entry event `demo_init/rng_seed_candidate` with `candidateSeedRaw` (native original), `seedOverrideApplied`, `globalDuelIndex` and `effectiveSeedRaw`. The capture footer records `rngSeedOverridesApplied`; the exporter REFUSES to finalize if the requested sequence of seed overrides was not observed, even if all terminal commands arrived.

Important: the original RNG uses CRT per-thread state. The seed setter and initial shuffle are sequential calls in the same native duel-entry path, but full-duel reproducibility still needs to be demonstrated empirically; possible other state and reseeds may influence outcomes.

## Recommended test — one duel per worker, reproducible

From project root:

```powershell
git pull
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 2 --workers 2 --seed 12345 `
  --deck-0 ".\native_hooks\decks\demo-A.json" `
  --deck-1 ".\native_hooks\decks\demo-B.json" `
  --trace-ai --speed-value 600
```

Run the exact command twice. Check BOTH runs show `Complete=True`, `processTerminationVerified=true` and `rngSeedOverridesApplied=1` for each worker. In each run, `rng-audit.json` should contain two unique initial shuffle fingerprints (unless an extraordinary collision). Compare the fingerprints **by global duel index** across runs: they should be the same for seed 12345. Repeat with `--seed 12346`; permutations should normally change.

DO NOT assume worker order or clock time determines seeds when seed override is enabled.

## Next static question

The value passed as `param_4` to `FUN_005BFEB0` by `FUN_0044C3E0` is not yet traced to its original source. To explain the observed time-correlated native defaults, follow the callsite `0x0044C62E` and assignments to that stack argument. The optional `--seed` works without assuming where that original argument comes from.
