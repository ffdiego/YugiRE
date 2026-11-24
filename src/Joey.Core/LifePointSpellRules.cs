namespace Joey.Core;

/// <summary>
/// A narrow, runnable demo policy for simple LP spells. The damage branch
/// reuses the audited P045 scalar resolver. Healing amounts are sourced from
/// the card descriptions; they are not claims of native callback parity.
/// </summary>
public readonly record struct LifePointSpellChange(int CasterDelta, int OpponentDelta);

public static class LifePointSpellRules
{
    public static bool Supports(ushort resourceId) =>
        DirectDamageRules.Amount(resourceId).HasValue ||
        resourceId is 338 or 339 or 340 or 341 or 1315;

    public const ushort BlueMedicine = 1006;

    // New rules are admitted only by the typed action producer.
    // Keep the legacy Play/ActivateSetSpell whitelist unchanged.
    public static bool SupportsQueued(ushort resourceId) =>
        resourceId == BlueMedicine || Supports(resourceId);

    public static LifePointSpellChange? ResolveQueued(ushort resourceId, int caster) =>
        caster is not (0 or 1) ? null :
        resourceId == BlueMedicine ? new LifePointSpellChange(400, 0) : ResolveDemo(resourceId, caster);

    /// <summary>
    /// Resolve a simple spell in the two-player C# demo (no chains, negation,
    /// activation costs or response windows). Values are signed LP deltas.
    /// Resource IDs are not internal card indices.
    /// </summary>
    public static LifePointSpellChange? ResolveDemo(ushort resourceId, int caster)
    {
        if (caster is < 0 or > 1) return null;
        if (DirectDamageRules.Amount(resourceId).HasValue)
        {
            // The demo explicitly supplies a clean request and the usual
            // opposing target. Do not infer those inputs from native traces.
            var target = DirectDamageRules.PrepareTarget((byte)caster);
            var damage = DirectDamageRules.Resolve(resourceId, 0, (byte?)target);
            if (damage.Status != DirectDamageStatus.Command ||
                damage.Command is null) return null;
            int amount = damage.Command.Amount;
            return damage.Command.TargetPlayer == caster
                ? new LifePointSpellChange(-amount, 0)
                : new LifePointSpellChange(0, -amount);
        }

        // P041 healing family and Rain of Mercy. Unlike Mooyan Curry
        // (337), these spells need no player selection in the basic demo.
        return resourceId switch
        {
            338 => new LifePointSpellChange(500, 0),   // Red Medicine
            339 => new LifePointSpellChange(600, 0),   // Goblin's Secret Remedy
            340 => new LifePointSpellChange(800, 0),   // Soul of the Pure
            341 => new LifePointSpellChange(1000, 0),  // Dian Keto the Cure Master
            1315 => new LifePointSpellChange(1000, 1000), // Rain of Mercy
            _ => null
        };
    }
}
