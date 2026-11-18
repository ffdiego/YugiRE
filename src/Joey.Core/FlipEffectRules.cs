namespace Joey.Core;

/// <summary>
/// Reusable sandbox FLIP dispatch, independent of animation timing.
/// Resource IDs are original card IDs, not internal DAT indexes.
/// </summary>
public enum DemoFlipEffect { None, DestroyMonster, ReplaceHands, ReturnUpToTwo, ReturnOne, ShuffleAndExcavate }

public static class FlipEffectRules
{
    public const ushort ManEaterBug = 500;
    public const ushort MorphingJar = 590;
    public const ushort PenguinSoldier = 601;
    public const ushort HaneHane = 540;
    public const ushort MorphingJar2 = 1163;

    public static DemoFlipEffect Effect(ushort resourceId) => resourceId switch
    {
        ManEaterBug => DemoFlipEffect.DestroyMonster,
        MorphingJar => DemoFlipEffect.ReplaceHands,
        PenguinSoldier => DemoFlipEffect.ReturnUpToTwo,
        HaneHane => DemoFlipEffect.ReturnOne,
        MorphingJar2 => DemoFlipEffect.ShuffleAndExcavate,
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

    
/// <summary>
/// Audit of a Morphing Jar #2 sandbox effect: counts are per player's own Main Deck.
/// Excavate is not drawing, so deck exhaustion here does not automatically lose LP.
/// Extra Deck, tokens and control changes are not modeled.
/// </summary>
public sealed record DemoExcavationResult(
    int Player, int ReturnedToDeck, int Excavated, int MonstersFound,
    int SpecialSummoned, int SentToGraveyard, bool DeckExhausted);
