using Joey.Core;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

static class CpuDeckSelectionTests
{
    public static void Run(Action<bool, string> check)
    {
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/cpu-deck-selection-tables.json")));
        var tables = evidence.RootElement.GetProperty("tables").EnumerateArray();
        foreach (var row in tables)
        {
            byte level = row.GetProperty("effective_level").GetByte();
            byte pool = row.GetProperty("pool").GetString() == "CPU" ? (byte)0 : (byte)1;
            byte raw = pool == 0 ? level : (byte)(level - 2);
            var options = CpuDeckSelection.Options(raw, pool);
            var expected = row.GetProperty("resources").EnumerateArray().Select(item => item.GetString()!).ToArray();
            check(options.Select(path => path[5..].Replace('/', '\\').ToLowerInvariant()).SequenceEqual(expected),
                $"CPU selector {row.GetProperty("pool").GetString()} level {level} matches pinned EXE table order");
            check(Enumerable.Range(0, options.Count).All(slot => CpuDeckSelection.Select(raw, pool, (uint)slot) == options[slot]),
                $"CPU selector level {level}, pool {pool} exposes every table option");
        }
        check(CpuDeckSelection.Select(1, 0, uint.MaxValue).EndsWith("CPU_000.ydc"),
            "CPU selection reduces the full unsigned 32-bit RNG value");
        check(CpuDeckSelection.EffectiveLevel(0, 0) == 1 && CpuDeckSelection.EffectiveLevel(255, 0) == 7 &&
            CpuDeckSelection.EffectiveLevel(0, 1) == 3 && CpuDeckSelection.EffectiveLevel(255, 255) == 9,
            "CPU/FUL clamps preserve unsigned profile-byte boundary behavior");
        int calls = 0;
        CpuDeckSelection.SelectNext(7, 1, () => { calls++; return 0; });
        check(calls == 1, "One resource selection consumes exactly one MT draw");
        check(CpuDeckSelection.Select(6, 1, 0) == CpuDeckSelection.Select(7, 1, 0),
            "Raising difficulty may retain an overlapping FUL deck");

        // Independent CPython MT twist/tempering over words built by the native legacy seed recurrence.
        // 1250 outputs cross two complete 624-word twists and start a third block.
        foreach (var (seed, hash) in new[]
        {
            (0u, "CE2BCEDE2DE5C06FF0246D84BB1683BE7023BF537BBB9E669824B81C8B712722"),
            (1u, "E549EB0688D054B7C557655B6CD6E392E9408DB3B9DAD383B0D4658AF15AF8EE"),
            (4357u, "A2BF4D5CBF3BCBEDCDAB5388BBD2F5757687856AA28EE5C1F1D80107E8993F18"),
            (uint.MaxValue, "6D29EA870B6E551F33531EEE04AEAA2AB39FB7336797F9E9A8823AD1E72AF93A")
        })
        {
            var rng = NativeDeckRng.LegacySeeded(seed);
            byte[] bytes = new byte[1250 * 4];
            for (int i = 0; i < 1250; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), rng.Next32());
            check(Convert.ToHexString(SHA256.HashData(bytes)) == hash, $"Legacy MT sequence matches independent reference for seed {seed}");
        }
        check(new NativeDeckRng().Next32() == 2867219139u, "Fresh native UI/deck generator uses lazy seed 4357");
        var original = NativeDeckRng.LegacySeeded(1);
        for (int i = 0; i < 623; i++) original.Next32();
        var resumed = NativeDeckRng.FromSnapshot(original.SnapshotWords(), original.Index);
        check(Enumerable.Range(0, 630).All(_ => original.Next32() == resumed.Next32()),
            "Restored shared-generator snapshot continues identically across twist boundary");
    }
}
