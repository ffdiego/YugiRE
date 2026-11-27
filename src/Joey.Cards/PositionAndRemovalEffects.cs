namespace Joey.Cards;

/// <summary>Card-specific definitions; legality and occurrence movement belong to Core.</summary>
public static class PositionAndRemovalEffects
{
    public const ushort StopDefense = 319;
    public const ushort BlockAttack = 1043;
    public const ushort DeSpell = 1009;

    public static bool? DesiredDefense(ushort id) => id switch
    {
        StopDefense => false,
        BlockAttack => true,
        _ => null
    };
}
