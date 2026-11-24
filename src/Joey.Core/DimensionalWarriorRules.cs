namespace Joey.Core;

/// <summary>
/// Dimensional Warrior (D.D. Warrior), P188, resource ID 255 / DAT index 46.
/// A bounded after-damage-calculation field-to-banish rule; not a claim that
/// the native callback 0058CC00 executes at this exact host call site.
/// </summary>
public static class DimensionalWarriorRules
{
    public const ushort ResourceId = 255;

    /// <summary>
    /// Neither battle participant must be in attack position at resolution:
    /// being attacked face-down or in defense is still battling. The caller
    /// separately checks that both original Card occurrences remain in field.
    /// Direct attacks have no defender and do not call this rule.
    /// </summary>
    public static bool BanishBothAfterDamageCalculation(
        ushort attackerResourceId, ushort defenderResourceId) =>
        attackerResourceId == ResourceId || defenderResourceId == ResourceId;
}
