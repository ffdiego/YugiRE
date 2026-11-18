namespace Joey.Core;

/// <summary>
/// Reusable sandbox FLIP dispatch, independent of animation timing.
/// Resource IDs are original card IDs, not internal DAT indexes.
/// </summary>
public enum DemoFlipEffect { None, DestroyMonster, ReplaceHands, ReturnUpToTwo, ReturnOne }

public static class FlipEffectRules
{
    public const ushort ManEaterBug = 500;
    public const ushort MorphingJar = 590;
    public const ushort PenguinSoldier = 601;
    public const ushort HaneHane = 540;

    public static DemoFlipEffect Effect(ushort resourceId) => resourceId switch
    {
        ManEaterBug => DemoFlipEffect.DestroyMonster,
        MorphingJar => DemoFlipEffect.ReplaceHands,
        PenguinSoldier => DemoFlipEffect.ReturnUpToTwo,
        HaneHane => DemoFlipEffect.ReturnOne,
        _ => DemoFlipEffect.None
    };
    public static bool Supports(ushort resourceId) => Effect(resourceId) != DemoFlipEffect.None;
}

/// <summary>
/// A FLIP waiting for target selection. Owner chooses targets in the hot-seat demo.
/// Attack-destroyed monsters remain physically on the field until the end of
/// the Damage Step, but cannot be returned to the hand by bounce FLIP effects.
/// </summary>
public sealed record DemoFlipTarget(int Owner, int SourceZone, Card SourceCard,
                                     DemoFlipEffect Effect = DemoFlipEffect.DestroyMonster);

public sealed record DemoSelectedMonster(int Player, int Zone, Card Card);
