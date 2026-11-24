namespace Joey.LookAndFeel;

/// <summary>
/// Resource indices follow the native activate.txt manifest:
/// 0 upper-blue, 1 lower-blue, 2 upper-yellow, 3 lower-yellow.
/// Sprites are loaded from the user's own data.dat, not distributed.
/// </summary>
public static class ActivationSpriteNames
{
    public const string UpperBlue = @"j\duel\effect\kouka_ue_b.yga";
    public const string LowerBlue = @"j\duel\effect\kouka_sita_b.yga";
    public const string UpperYellow = @"j\duel\effect\kouka_ue_y.yga";
    public const string LowerYellow = @"j\duel\effect\kouka_sita_y.yga";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(new[] { UpperBlue, LowerBlue, UpperYellow, LowerYellow });

    public static string At(int resourceIndex) => resourceIndex switch
    {
        0 => UpperBlue, 1 => LowerBlue, 2 => UpperYellow, 3 => LowerYellow,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceIndex))
    };
}
