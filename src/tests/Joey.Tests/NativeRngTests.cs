using Joey.Core;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Reference checks extracted from Joey's real JSONL deck_shuffle/leave,
// NOT calculated by the C# implementation under test.
// Source: parallel-mjj1z5ia.zip, two independently seeded demo duels.
internal static class NativeRngTests
{
    private static readonly ushort[] Side0 =
    [
        1082, 211, 1099, 1099, 525, 525, 525, 834, 834, 615,
        862, 151, 151, 151, 1052, 1052, 1052, 364, 616, 591,
        1093, 630, 1026, 86, 738, 610, 929, 929, 461, 376,
        309, 309, 552, 552, 703, 717, 16, 207, 207, 282
    ];

    private static readonly ushort[] Side1 =
    [
        1082, 887, 887, 887, 886, 886, 886, 885, 885, 885,
        1038, 1038, 828, 828, 700, 700, 100, 100, 358, 385,
        385, 868, 868, 1101, 291, 539, 539, 969, 20, 376,
        309, 309, 929, 929, 552, 717, 715, 715, 763, 763,
        290, 52, 167
    ];

    // FUN_00569380 creates card words with an ordinal in bits 24-30,
    // side in bit 12, and the original card index in the low 12 bits.
    private static uint[] Prepare(ushort[] cards, int side)
    {
        return cards.Select((id, index) =>
            ((uint)(index + 1) << 24) | ((uint)side << 12) | id).ToArray();
    }

    private static string Digest(uint[] words)
    {
        string text = string.Join(",", words.Select(w =>
            w.ToString(CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(text)));
    }

    public static void Run(Action<bool, string> check)
    {
        var basic = new NativeRng(1);
        check(basic.Next15() == 41, "MSVC RNG seed 1 gives rand=41");
        check(new NativeRng(0).Next15() == 38, "MSVC RNG seed 0 gives rand=38");

        foreach (var (seed, hashSide1, hashSide0) in new[]
        {
            (2435775735u,
             "815E6A6D9D2CEC7078068BC6DB610F787C788407B9655032FED0AEE16D6C8B73",
             "6822E6D3D6B046E940A1418CF6D86C79C106B3D8F0B91DD2335B55E2968AAAFE"),
            (4228375765u,
             "F2DDF944A459D5E65C34864F06A43D09700AA8956F4BE18101A9AB7BF7D14AB4",
             "8717AA5F0D1A05C69CD742537F5CD3DA846845083491EA8B2E059FB8B45911E8")
        })
        {
            var rng = new NativeRng(seed);
            uint[] shuffledSide1 = Prepare(Side1, 1);
            uint[] shuffledSide0 = Prepare(Side0, 0);
            rng.ShuffleHighMode(shuffledSide1);
            rng.ShuffleHighMode(shuffledSide0);
            check(Digest(shuffledSide1) == hashSide1,
                $"Original side 1 shuffle matches native 32-bit words for seed {seed}");
            check(Digest(shuffledSide0) == hashSide0,
                $"Original side 0 shuffle matches native 32-bit words for seed {seed}");
        }

        // rand() can output RAND_MAX==32767; original bounded scaling can
        // return 'remaining', a one-past-end index. Do not emulate unsafe
        // native memory writes by silently clamping the generated index.
        var edge = new NativeRng(4028364353u);
        check(edge.NextIndexHighMode(1) == 1,
            "Original bounded RNG can return one past the last valid index");
        bool rejected = false;
        try { new NativeRng(4028364353u).ShuffleHighMode(new uint[] { 1 }); }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Unsafe inclusive-upper-bound shuffle stays explicit");
    }
}
