namespace Joey.Core;

/// <summary>Joey 0055C140, shared UI/deck MT generator with legacy 0055C0E0 seeding.
/// Separate from NativeRng (CRT). Does not connect itself to DemoDuel or the UI.</summary>
public sealed class NativeDeckRng
{
    private readonly uint[] state = new uint[624];
    private int index = 625; // 0040F150 constructor: lazy default seed 4357.
    public int Index => index;
    public uint[] SnapshotWords() => (uint[])state.Clone();

    public static NativeDeckRng FromSnapshot(ReadOnlySpan<uint> words, int index)
    {
        if (words.Length != 624 || index is < 0 or > 625)
            throw new ArgumentException("Expected 624 words and native index 0..625");
        var rng = new NativeDeckRng { index = index };
        words.CopyTo(rng.state);
        return rng;
    }

    public static NativeDeckRng LegacySeeded(uint seed)
    {
        var rng = new NativeDeckRng();
        rng.Seed(seed);
        return rng;
    }

    private void Seed(uint seed)
    {
        for (int i = 0; i < state.Length; i++)
        {
            uint high = seed & 0xFFFF0000u;
            seed = unchecked(seed * 69069u + 1);
            state[i] = high | seed >> 16;
            seed = unchecked(seed * 69069u + 1);
        }
        index = 624;
    }

    public uint Next32()
    {
        if (index >= 624)
        {
            if (index == 625) Seed(4357);
            // In-place twist matters: the second span reads words already updated.
            for (int i = 0; i < 624; i++)
            {
                uint mixed = (state[i] & 0x80000000u) | (state[(i + 1) % 624] & 0x7FFFFFFFu);
                state[i] = state[(i + 397) % 624] ^ (mixed >> 1) ^ ((mixed & 1) != 0 ? 0x9908B0DFu : 0);
            }
            index = 0;
        }
        uint value = state[index++];
        value ^= value >> 11;
        value ^= (value & 0xFF3A58ADu) << 7;
        value ^= (value & 0xFFFFDF8Cu) << 15;
        return value ^ value >> 18;
    }
}
