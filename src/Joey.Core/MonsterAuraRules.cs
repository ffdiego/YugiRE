namespace Joey.Core;

public static class MonsterAuraRules
{
    /// <summary>Signed ATK contribution for one face-up source in native table 005DCF98.</summary>
    public static int AttackDelta(ushort sourceId, byte targetAttribute)
    {
        (byte boost, byte weaken) = sourceId switch
        {
            491 => ((byte)1, (byte)2), // Hoshiningen
            627 => ((byte)2, (byte)1), // Witch's Apprentice
            523 => ((byte)3, (byte)4), // Star Boy
            597 => ((byte)4, (byte)3), // Little Chimera
            526 => ((byte)5, (byte)6), // Milus Radiant
            608 => ((byte)6, (byte)5), // Bladefly
            _ => ((byte)0, (byte)0)
        };
        if (boost == 0) return 0;
        if (targetAttribute == boost) return 500;
        return targetAttribute == weaken ? -400 : 0;
    }
}
