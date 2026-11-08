# Joey native shuffle: matching reconstruction independent of the EXE

**Date:** 2026-11-08. **Original EXE SHA256:**
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

**Inputs:** `rng-seed-{core,callers,init}.txt`, `rng-recon-{shuffle,crt}.txt`,
`parallel-mjj1z5ia.zip`, `parallel-a1xf_v2y.zip`.
No game execution occurred for the offline reproduction test.

## Confirmed native call chain

`FUN_005BFEB0` invokes `FUN_005C3BF5(seed)` before
`FUN_005B9E50`, which prepares both decks
(`FUN_00569380(0/1)`) and shuffles both with
`FUN_00568D80(side,8)`.

`FUN_005C3BF5` stores seed in CRT thread state +0x14;
`_rand` updates state with:

```c
uint32_t state = seed;
state = state * 0x343FD + 0x269EC3; // uint32 wrap
int r = (state >> 16) & 0x7FFF;
```

For the demo configuration observed, the `DAT_005F3284`
selector takes the **high-mode shuffle** branch. It ignores
`shufflePasses` and proceeds backward through the deck:
```c
for (int remaining = count; remaining > 0; --remaining) {
    int index = JoeyRand() * remaining / 32767;
    swap(deck[remaining-1], deck[index]);
}
```

IMPORTANT: The native calculation can produce
`index == remaining` when `rand()==32767`.
No such event was encountered in the four tested duels
(two unique duel seeds, repeated across two independent runs).
The original behavior of that rare out-of-bounds position
has not been established; the safe C# reimplementation
**throws** rather than silently clamping or accessing
memory outside an array. This is a deliberate exception
to bitwise matching until investigated.

The other shuffle branch (mode <=1000) has a different
random-swap algorithm; it is NOT implemented in
`NativeRng.ShuffleHighMode`. Do not claim universal
shuffle fidelity from these two tests.

## Matched real deck words, not just card IDs

For each duel, take the exact input words observed in
`demo_init/deck_shuffle/enter` and the applied seed
from `demo_init/rng_seed_candidate/enter`.
Run the MSVC RNG continuously over side 1's shuffle,
then side 0's shuffle (no per-side reseed).

| Global seed index | Effective seed | Side 1 SHA256 | Side 0 SHA256 |
|---|---:|---|---|
| 0 | 2435775735 | `815E6A6D9D2CEC7078068BC6DB610F787C788407B9655032FED0AEE16D6C8B73` | `6822E6D3D6B046E940A1418CF6D86C79C106B3D8F0B91DD2335B55E2968AAAFE` |
| 1 | 4228375765 | `F2DDF944A459D5E65C34864F06A43D09700AA8956F4BE18101A9AB7BF7D14AB4` | `8717AA5F0D1A05C69CD742537F5CD3DA846845083491EA8B2E059FB8B45911E8` |

SHA256 here is computed over ASCII decimal words joined with a
comma (no trailing delimiter). Each deck word preserves the
original's 32-bit **ordinal/side/index** fields.

**Independent offline results:** eight of eight initial
deck shuffles matched exactly (two sides * two duel seeds *
two Windows captures). Both independently captured runs had
identical full word sequences for corresponding seeds.

## C# code and executable tests

- `src/Joey.Core/NativeRng.cs`: stateful native MSVC `rand`
  and high-mode deck shuffle, no modification to the existing demo.
- `src/tests/Joey.Tests/NativeRngTests.cs`: reconstruct source deck
  words using the two exact 40/43-card deck plans from
  `parallel-mjj1z5ia.zip`, hash the shuffled outputs
  and compare to the **original captured hashes**, not
  outputs produced by C# itself.
- `src/tests/Joey.Tests/Program.cs` runs the new tests.

Windows command from the repository root:
```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```

**Not yet compiled or run with .NET in the assistant environment**
(no local dotnet SDK). The Python reconstruction and independent
trace comparison were executed successfully; the C# test suite
still requires running the command above.

## Next steps

1. Investigate the meaning of the upper 16 command bits using
   `scripts/Run-CommandHighWordRecon.ps1`.
2. Export deterministic replay fixtures from the YugiLab trace
   in an explicit schema for future tests in Joey.Core.
3. Track the original result/victory state independently of the
   terminal opcode `0x0004` before asserting full duel equality.
