namespace Joey.Core;

public static class MonsterAuraRules
{
    /// <summary>Signed ATK contribution for one face-up source in native table 005DCF98.</summary>
    public static int AttackDelta(ushort sourceId, byte targetAttribute) =>
        Joey.Cards.ElementalAuraEffects.AttackDelta(sourceId, targetAttribute);
}
