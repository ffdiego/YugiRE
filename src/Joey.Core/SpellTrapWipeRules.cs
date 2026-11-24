namespace Joey.Core;

public static class SpellTrapWipeRules
{
    public const ushort HarpiesFeatherDuster = 671;
    public const ushort HeavyStorm = 1061;

    public static bool Supports(ushort resourceId) => resourceId is HarpiesFeatherDuster or HeavyStorm;

    // Native 0x7E0 covers the five Spell/Trap slots and the Field Spell slot.
    internal static bool HitsOwnSide(ushort resourceId) => resourceId == HeavyStorm;

    internal static int CountOnSide(int player, Card?[,] spells, Card?[] fieldSpells, int excludedZone = -1)
    {
        int count = fieldSpells[player] is null ? 0 : 1;
        for (int zone = 0; zone < 5; zone++)
            if (zone != excludedZone && spells[player, zone] is not null)
                count++;
        return count;
    }
}
