namespace Joey.Cards;

/// <summary>Fixed effect definitions by DAT resource ID; independent of the duel state and presentation.</summary>
public static class FixedSpellEffects
{
    public static int? OpponentDamage(ushort id) => id switch
    {
        342 => 200, 343 => 500, 344 => 600, 345 => 800, 1007 => 300,
        _ => null
    };

    public static (int CasterDelta, int OpponentDelta, bool JointDamage)? LifePoints(ushort id)
    {
        if (OpponentDamage(id) is int damage) return (0, -damage, false);
        return id switch
        {
            338 => (500, 0, false), 339 => (600, 0, false),
            340 => (800, 0, false), 341 => (1000, 0, false),
            1315 => (1000, 1000, false), 1006 => (400, 0, false),
            346 => (-500, -1000, true), _ => null
        };
    }

    public static (int Cards, int OpponentLifeGain)? Draw(ushort id) => id switch
    {
        1010 => (2, 0), 1065 => (1, 1000), _ => null
    };
}
