namespace Joey.Core;

/// <summary>
/// Reusable sandbox FLIP dispatch, independent of animation timing.
/// Resource IDs are original card IDs, not internal DAT indexes.
/// </summary>
public enum DemoFlipEffect
{
    None, DestroyMonster, ReplaceHands, ReturnUpToTwo, ReturnOne, ShuffleAndExcavate,
    RecoverFromGraveyard, MillOpponentDeck, DrawAndDiscardOpponentSpells,
    DestroyOpponentFaceUpLevelFour, OptionalDrawOne,
    DestroyTwoSetSpellTraps, ReturnTwoSpellTraps,
    BanishThreeDeckBurn800, BanishTwoRandomHandBurn800,
    Gain3000LifePoints,
    DestroyOpponentMonster, RecruitSameNameFaceDown, DamagePerOpposingSpellTrap, RaiseSelfStats
}

public enum DemoMonsterEffectTrigger { Flip, PositionChange }

public static class FlipEffectRules
{
    public const ushort ManEaterBug = 500;
    public const ushort MorphingJar = 590;
    public const ushort PenguinSoldier = 601;
    public const ushort HaneHane = 540;
    public const ushort MorphingJar2 = 1163;
    public const ushort MagicianOfFaith = 427;
    public const ushort MaskOfDarkness = 101;
    public const ushort NeedleWorm = 561;
    public const ushort HirosShadowScout = 610;
    public const ushort FourStarredLadybugOfDoom = 1338;
    public const ushort Skelengel = 539;
    public const ushort PrincessOfTsurugi = 265;
    public const ushort Greenkappa = 585;
    public const ushort TornadoBird = 1521;
    public const ushort LadyAssailantOfFlames = 1435;
    public const ushort FireSorcerer = 1436;
    public const ushort ImmortalOfThunder = 461;
    public const ushort BubonicVermin = 1307;
    public const ushort SlateWarrior = 1254;

    public static DemoFlipEffect Effect(ushort resourceId) => resourceId switch
    {
        ManEaterBug => DemoFlipEffect.DestroyMonster,
        MorphingJar => DemoFlipEffect.ReplaceHands,
        PenguinSoldier => DemoFlipEffect.ReturnUpToTwo,
        HaneHane => DemoFlipEffect.ReturnOne,
        MorphingJar2 => DemoFlipEffect.ShuffleAndExcavate,
        MagicianOfFaith or MaskOfDarkness => DemoFlipEffect.RecoverFromGraveyard,
        NeedleWorm => DemoFlipEffect.MillOpponentDeck,
        HirosShadowScout => DemoFlipEffect.DrawAndDiscardOpponentSpells,
        FourStarredLadybugOfDoom => DemoFlipEffect.DestroyOpponentFaceUpLevelFour,
        Skelengel => DemoFlipEffect.OptionalDrawOne,
        PrincessOfTsurugi => DemoFlipEffect.DamagePerOpposingSpellTrap,
        Greenkappa => DemoFlipEffect.DestroyTwoSetSpellTraps,
        TornadoBird => DemoFlipEffect.ReturnTwoSpellTraps,
        LadyAssailantOfFlames => DemoFlipEffect.BanishThreeDeckBurn800,
        FireSorcerer => DemoFlipEffect.BanishTwoRandomHandBurn800,
        ImmortalOfThunder => DemoFlipEffect.Gain3000LifePoints,
        BubonicVermin => DemoFlipEffect.RecruitSameNameFaceDown,
        SlateWarrior => DemoFlipEffect.RaiseSelfStats,
        _ => DemoFlipEffect.None
    };
    public static bool Supports(ushort resourceId) => Effect(resourceId) != DemoFlipEffect.None;
}

/// <summary>
/// Existing monster picker; also used by position-triggered effects.
/// Battle-destroyed monsters stay on the field until the end of the Damage Step,
/// but cannot be returned to the hand by bounce effects.
/// </summary>
public sealed record DemoFlipTarget(int Owner, int SourceZone, Card SourceCard,
    DemoFlipEffect Effect = DemoFlipEffect.DestroyMonster,
    DemoMonsterEffectTrigger Trigger = DemoMonsterEffectTrigger.Flip);

public sealed record DemoSelectedMonster(int Player, int Zone, Card Card);

    
/// <summary>
/// Audit of a Morphing Jar #2 sandbox effect: counts are per player's own Main Deck.
/// Excavate is not drawing, so deck exhaustion here does not automatically lose LP.
/// Extra Deck, tokens and control changes are not modeled.
/// </summary>
public sealed record DemoExcavationResult(
    int Player, int ReturnedToDeck, int Excavated, int MonstersFound,
    int SpecialSummoned, int SentToGraveyard, bool DeckExhausted);
