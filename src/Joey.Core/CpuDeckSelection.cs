namespace Joey.Core;

/// <summary>Static Joey selector at VA 0044BBE0. Raw profile bytes, not demo policy.
/// The caller supplies the shared 0055C140 generator; never substitute the CRT shuffle RNG.</summary>
public static class CpuDeckSelection
{
    private static readonly int[][] cpu =
    [
        [0, 1, 2, 3, 4], [3, 4, 10, 11, 12], [10, 11, 12, 13, 14],
        [13, 14, 15, 20, 21], [20, 21, 22, 23, 24],
        [23, 24, 25, 26, 27], [25, 26, 27, 28, 29]
    ];
    private static readonly string[][] full =
    [
        ["0GE1", "0UMI", "0VAL"],
        ["1DEC", "1RED", "1TOO", "1INS", "1UIJ", "1UMI", "1VAL"],
        ["1FUS", "1GAM", "1GE1", "1GE2", "1HIK", "1MIX"],
        ["2DEC", "2VAL", "2TOO", "2GE1", "2HIK", "2INS", "2UIJ"],
        ["2FUS", "2GAM", "2GE2", "2MIX", "2RED", "2UMI"],
        ["3ALL", "3DEC", "3YAM", "3FUS", "3TOO", "3HND"],
        ["3ALL", "3DEC", "3EXO", "3FUS", "3GRV", "3HND"]
    ];
    public static int EffectiveLevel(byte baseDifficulty, byte pool) => pool == 0
        ? Math.Clamp((int)baseDifficulty, 1, 7)
        : Math.Clamp(baseDifficulty + 2 * pool, 3, 9);

    public static IReadOnlyList<string> Options(byte baseDifficulty, byte pool)
    {
        int level = EffectiveLevel(baseDifficulty, pool);
        return Array.AsReadOnly(pool == 0
            ? cpu[level - 1].Select(number => $"data/J/file/CPU_{number:D3}.ydc").ToArray()
            : full[level - 3].Select(suffix => $"data/J/file/FUL_{suffix}.ydc").ToArray());
    }

    public static string Select(byte baseDifficulty, byte pool, uint randomValue)
    {
        var options = Options(baseDifficulty, pool);
        return options[(int)(randomValue % (uint)options.Count)];
    }

    public static string SelectNext(byte baseDifficulty, byte pool, Func<uint> next32)
    {
        ArgumentNullException.ThrowIfNull(next32);
        // Every reachable table row is nonempty, including rows with clamped input.
        return Select(baseDifficulty, pool, next32());
    }
}
