# Joey the Passion: static RNG trace from Ghidra (2026-11-08)

**Executable SHA256:** `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
**Input reports:** `rng-recon-shuffle.txt`, `rng-recon-crt.txt`, `rng-recon-alternatives.txt` provided by the operator.

## Confirmed call chain

- `FUN_005B9E50` initializes the duel state, prepares both decks with `FUN_00569380(0/1)` and calls `FUN_00568D80(side,8)` for each side.
- `FUN_00568D80` shuffles the deck by calling `FUN_005BDDB0(n)` to select positions and `FUN_005675D0` to swap card words.
- `FUN_005BDDB0(n)` calls `_rand` at `0x005C3C02`, then selects either `rand() % n` or `(rand() * n) / 0x7fff` depending on `FUN_0044BAA0()` being less than 1000. The behavior and role of the branch selector are not yet confirmed.
- `FUN_00408B90` explicitly invokes `FUN_005C3BF5(uVar7 | uVar2)` and calls `FUN_00568D80(side,3)` on a path that resumes shuffle.
- `FUN_00558BF0` calls `FUN_005C3BF5(0)` immediately before using `_rand` to fill 256 bytes. This strongly suggests `FUN_005C3BF5` initializes or resets the shared RNG, but its body is not in the three Ghidra reports.

## What remains unproven

- Whether `FUN_005C3BF5` is a direct equivalent of `srand` and where the internal PRNG state is stored.
- Whether a seed is derived from time, duel data, command data, or a fixed constant at the beginning of a demo.
- Whether changing a seed affects more subsystems than deck shuffle, including card effects and AI tie-breaking.
- Whether one fixed seed reliably reproduces a complete duel in the same executable and state.

The alternatives report did not identify `CryptGenRandom`, `BCryptGenRandom`, or `QueryPerformanceCounter` symbols. That is not evidence that another RNG cannot exist.

## Next investigation

The new read-only script `scripts/Run-RngSeedRecon.ps1` queries:

- Body and instructions of `FUN_005C3BF5` and `_rand @0x005C3C02`.
- `FUN_0044BAA0` (modulo vs scale mode).
- Callers and code around `FUN_00408B90`, `FUN_0044BEE0`, `FUN_0044B980`.

Run:

```powershell
.\scripts\Run-RngSeedRecon.ps1
```

Send the three reports generated as `rng-seed-core.txt`, `rng-seed-callers.txt`, `rng-seed-init.txt`.

## Minimal passive dynamic follow-up

`native_rules/yugire_dual_loader.js` now adds an observational probe
`demo_init/rng_seed_candidate` at `0x005C3BF5`, logging
`candidateSeedRaw`, `candidateSeedHex`, `caller`, `seq`, and `threadId`.
This does **not** call/replace the function, patch its arguments or override the RNG.
It can run under the existing `yugilab --trace-ai` command, or without AI probe because demo hooks are always armed.

`yugilab.rng_audit` associates the most recent raw candidate argument observed
before `scene_config` with the first pair of initial deck-shuffle arrays.
Any candidate *called after scene entry* remains in the per-source event list;
do not misinterpret its absence from `seedCandidateBeforeScene`.

**Do not provide or use `--seed` until the decompiler and a controlled runtime experiment establish safe semantics.**
