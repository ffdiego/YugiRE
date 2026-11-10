namespace Joey.Core;

/// <summary>Loaded profile fields A54DB3, DB4, DAF, DB0. Previous displayed
/// difficulty A54DB2 is acknowledged separately by UI and is not changed here.</summary>
public readonly record struct CpuDifficultyState(byte BaseDifficulty, byte Pool, byte LastResultClass, byte Streak);

/// <summary>Difficulty/streak portion of 00446DF0 after profile load.
/// Does not implement profile I/O, result statistics, or native match orchestration.</summary>
public static class CpuDifficultyProgression
{
    private static readonly byte[] winThreshold = [0, 3, 4, 5, 5, 5, 6, 6, 7, 0];
    private static readonly byte[] nonWinThreshold = [0, 0, 3, 4, 4, 4, 4, 3, 2, 2];

    public static CpuDifficultyState Apply(CpuDifficultyState profile, byte nativeResult,
        bool matchComplete, int sceneMode = 0, byte modeFlags = 0)
    {
        // Inputs intentionally bound to understood profile states, not arbitrary corrupted memory.
        if (profile.Pool > 1 || profile.BaseDifficulty > 7 || nativeResult > 3)
            throw new ArgumentException("Unsupported profile/result state");
        if (sceneMode == 3) return profile;
        if ((modeFlags & 0x40) != 0)
            return profile.LastResultClass == 1 ? profile with { Streak = 0 } : profile;

        bool win = nativeResult == 1;
        byte streak = (profile.LastResultClass == 1) == win
            ? (byte)Math.Min(255, profile.Streak + 1) : (byte)1;
        var updated = profile with { LastResultClass = win ? (byte)1 : (byte)2, Streak = streak };
        if (!matchComplete) return updated;
        int offset = profile.Pool * 2;
        int level = Math.Max(1, profile.BaseDifficulty + offset);
        if (win)
        {
            int maximum = profile.Pool == 0 ? 7 : 9;
            if (level < maximum && streak >= winThreshold[level])
                return updated with { BaseDifficulty = (byte)(level - offset + 1), Streak = 0 };
        }
        else if (level > offset + 1 && streak >= nonWinThreshold[level])
            return updated with { BaseDifficulty = (byte)(level - offset - 1), Streak = 0 };
        return updated;
    }
}
