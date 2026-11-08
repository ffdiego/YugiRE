namespace Joey.Core;

/// <summary>
/// MSVC CRT RNG used by original Joey (joey_pc.exe SHA256 db8fe7b7...).
/// Ghidra: FUN_005C3BF5 sets the per-thread RNG state; _rand @005C3C02
/// multiplies by 0x343FD and adds 0x269EC3 (uint32 arithmetic).
/// This class owns its state; it does not change DemoDuel's temporary
/// .NET Random policy or assert that all engine behavior is reproduced.
/// </summary>
public sealed class NativeRng
{
    private uint state;

    public NativeRng(uint seed) => state = seed;

    public uint State => state;

    public int Next15()
    {
        state = unchecked(state * 0x343FDu + 0x269EC3u);
        return (int)((state >> 16) & 0x7FFF);
    }

    /// <summary>
    /// Original FUN_005BDDB0(max) for DAT_005F3284 >= 1000:
    /// (rand() * max) / 32767. The original can return max when
    /// rand() == 32767; intentionally do not silently clamp it.
    /// </summary>
    public int NextIndexHighMode(int remaining)
    {
        if (remaining <= 0 || remaining > int.MaxValue / 0x7FFF)
            throw new ArgumentOutOfRangeException(nameof(remaining));
        return Next15() * remaining / 0x7FFF;
    }

    /// <summary>
    /// Observed Joey demo shuffle branch of FUN_00568D80:
    /// one backward Fisher-Yates-like pass over the entire deck,
    /// regardless of the shufflePasses argument on this branch.
    /// Verified against full 32-bit deck words in independent traces.
    /// </summary>
    public void ShuffleHighMode(Span<uint> deck)
    {
        for (int remaining = deck.Length; remaining > 0; --remaining)
        {
            int index = NextIndexHighMode(remaining);
            if (index >= remaining)
                throw new InvalidOperationException(
                    "Original Joey RNG chose an out-of-range index on the " +
                    "inclusive MSVC rand() maximum. Native behavior has not " +
                    "been established for this rare case.");
            uint value = deck[remaining - 1];
            deck[remaining - 1] = deck[index];
            deck[index] = value;
        }
    }
}
