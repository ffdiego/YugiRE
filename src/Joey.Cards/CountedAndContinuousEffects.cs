namespace Joey.Cards;

/// <summary>Card-specific effects by DAT resource ID; no duel mutation.</summary>
public static class CountedAndContinuousEffects
{
    public const ushort GiftOfTheMysticalElf = 1092;
    public const ushort JarOfGreed = 1447;
    public const ushort GravityBind = 1323;
    public const ushort RoyalCommand = 1530;
    public const ushort DestinyBoard = 1528;

    public static bool IsContinuousTrap(ushort id) =>
        id is GravityBind or RoyalCommand or DestinyBoard;

    public static int GiftLifeGain(int ownMonsters, int opposingMonsters) =>
        300 * (ownMonsters + opposingMonsters);

    public static bool GravityBindPreventsAttack(int level) => level >= 4;
}

/// <summary>Pure continuous ATK contributions, one source at a time.</summary>
public static class ElementalAuraEffects
{
    public static bool Supports(ushort id) => id is 491 or 627 or 523 or 597 or 526 or 608;

    public static int AttackDelta(ushort id, byte attribute)
    {
        (byte boost, byte weaken) = id switch
        {
            491 => ((byte)1, (byte)2),
            627 => ((byte)2, (byte)1),
            523 => ((byte)3, (byte)4),
            597 => ((byte)4, (byte)3),
            526 => ((byte)5, (byte)6),
            608 => ((byte)6, (byte)5),
            _ => ((byte)0, (byte)0)
        };
        if (boost == 0) return 0;
        return attribute == boost ? 500 : attribute == weaken ? -400 : 0;
    }
}
