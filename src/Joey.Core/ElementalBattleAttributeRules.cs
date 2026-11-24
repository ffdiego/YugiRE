namespace Joey.Core;

/// <summary>Bounded phase-dependent ATK modifiers from native 0056DD30.
/// Native battle phase mask is 6; DemoPhase.Battle is the sandbox equivalent.
/// Source suppression beyond face-up/marked removal is not modeled here.</summary>
public static class ElementalBattleAttributeRules
{
    public const ushort SoulOfPurityAndLight = 1515;
    public const ushort SpiritOfFlames = 1516;
    public const ushort RockSpiritResource = 1518;

    public static int AttackDelta(ushort resourceId,int controller,int activePlayer,bool battlePhase,int opposingSouls)
    {
        if (controller is < 0 or > 1 || activePlayer is < 0 or > 1 || opposingSouls is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(controller));
        if (!battlePhase) return 0;
        int own = resourceId switch
        {
            SpiritOfFlames when controller == activePlayer => 300,
            RockSpiritResource when controller != activePlayer => 300,
            _ => 0
        };
        return own - (controller == activePlayer ? 300 * opposingSouls : 0);
    }
}
