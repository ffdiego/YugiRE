using Joey.Core;
using System.Text.Json;

static class CpuDifficultyProgressionTests
{
    public static void Run(Action<bool, string> check)
    {
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/cpu-deck-selection-tables.json")));
        var thresholds = evidence.RootElement.GetProperty("progression_thresholds");
        var fresh = new CpuDifficultyState(1, 0, 0, 0);
        var profile = fresh;
        profile = CpuDifficultyProgression.Apply(profile, 1, true);
        profile = CpuDifficultyProgression.Apply(profile, 1, true);
        check(profile.BaseDifficulty == 1 && profile.Streak == 2,
            "Two consecutive wins do not yet advance the first CPU level");
        profile = CpuDifficultyProgression.Apply(profile, 1, true);
        check(profile.BaseDifficulty == 2 && profile.Streak == 0,
            "Third win advances level one and clears the streak");
        foreach (var (pool, first, last) in new[] { (0, 1, 6), (1, 3, 8) })
        {
            for (int level = first; level <= last; level++)
            {
                int required = thresholds.GetProperty("win").GetProperty(level.ToString()).GetInt32();
                var state = new CpuDifficultyState((byte)(level - pool * 2), (byte)pool, 1, (byte)(required - 2));
                var before = CpuDifficultyProgression.Apply(state, 1, true);
                var after = CpuDifficultyProgression.Apply(before, 1, true);
                check(before.BaseDifficulty == state.BaseDifficulty && after.BaseDifficulty == state.BaseDifficulty + 1 && after.Streak == 0,
                    $"Win threshold boundary level {level}, pool {pool}");
            }
        }
        foreach (int pool in new[] { 0, 1 })
            for (int level = pool * 2 + 2; level <= (pool == 0 ? 7 : 9); level++)
            {
                int required = thresholds.GetProperty("non_win").GetProperty(level.ToString()).GetInt32();
                var state = new CpuDifficultyState((byte)(level - pool * 2), (byte)pool, 2, (byte)(required - 2));
                var before = CpuDifficultyProgression.Apply(state, 2, true);
                var after = CpuDifficultyProgression.Apply(before, 2, true);
                check(before.BaseDifficulty == state.BaseDifficulty && after.BaseDifficulty == state.BaseDifficulty - 1 && after.Streak == 0,
                    $"Non-win threshold boundary level {level}, pool {pool}");
            }
        check(CpuDifficultyProgression.Apply(profile with { Streak = 10, LastResultClass = 1 }, 2, true).Streak == 1,
            "Changing result class restarts the streak");
        var draw = CpuDifficultyProgression.Apply(new(2, 0, 2, 2), 3, true);
        check(draw.BaseDifficulty == 1 && draw.LastResultClass == 2,
            "Native result three participates in the non-win streak branch");
        var incomplete = CpuDifficultyProgression.Apply(new(1, 0, 1, 2), 1, false);
        check(incomplete.BaseDifficulty == 1 && incomplete.Streak == 3,
            "An unfinished match updates streak but defers difficulty advancement");
        check(CpuDifficultyProgression.Apply(incomplete, 1, true).BaseDifficulty == 2,
            "Completed match can use the accumulated round streak");
        check(CpuDifficultyProgression.Apply(fresh, 1, true, sceneMode: 3) == fresh,
            "Scene three skips profile result processing");
        var oldWin = new CpuDifficultyState(4, 1, 1, 8);
        check(CpuDifficultyProgression.Apply(oldWin, 2, true, modeFlags: 0x40) == oldWin with { Streak = 0 } &&
            CpuDifficultyProgression.Apply(oldWin with { LastResultClass = 2 }, 1, true, modeFlags: 0x40).Streak == 8,
            "Mode flag 0x40 bypasses progression and conditionally clears an old win streak");
        check(CpuDifficultyProgression.Apply(new(7, 0, 1, 255), 1, true) == new CpuDifficultyState(7, 0, 1, 255) &&
            CpuDifficultyProgression.Apply(new(7, 1, 1, 255), 1, true) == new CpuDifficultyState(7, 1, 1, 255),
            "Streak saturates at 255 and each pool respects its upper level");
        check(CpuDifficultyProgression.Apply(fresh, 2, true).BaseDifficulty == 1 &&
            CpuDifficultyProgression.Apply(new(1, 1, 2, 255), 2, true).BaseDifficulty == 1,
            "Lower levels remain one for CPU and three effective for FUL");
    }
}
