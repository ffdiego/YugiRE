namespace Joey.Core;

/// <summary>
/// Small FLIP-effect dispatch table for the playable C# sandbox.
/// Resource IDs are from the original Joey card catalog, not internal indices.
/// This does not model chains, responses, negation, replacement or native timing.
/// </summary>
public enum DemoFlipEffect { None, DestroyMonster, ReplaceHands }

public static class FlipEffectRules
{
    public const ushort ManEaterBug = 500;
    public const ushort MorphingJar = 590;

    public static DemoFlipEffect Effect(ushort resourceId) => resourceId switch
    {
        ManEaterBug => DemoFlipEffect.DestroyMonster,
        MorphingJar => DemoFlipEffect.ReplaceHands,
        _ => DemoFlipEffect.None
    };
    public static bool Supports(ushort resourceId) => Effect(resourceId) != DemoFlipEffect.None;
}

/// <summary>
/// Pending targeted FLIP effect. In a local hot-seat demo the card's owner
/// chooses any occupied monster zone; a queued battle waits for resolution.
/// </summary>
public sealed record DemoFlipTarget(int Owner, int SourceZone, Card SourceCard);
