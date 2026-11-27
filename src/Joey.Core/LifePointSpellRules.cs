namespace Joey.Core;

public readonly record struct LifePointSpellChange(int CasterDelta, int OpponentDelta, bool JointDamage = false);

/// <summary>Compatibility adapter; execution belongs to the Core queue.</summary>
public static class LifePointSpellRules
{
    public const ushort BlueMedicine = 1006;
    public const ushort TremendousFire = 346;

    public static bool Supports(ushort resourceId) =>
        resourceId is not (BlueMedicine or TremendousFire) && SupportsQueued(resourceId);

    public static bool SupportsQueued(ushort resourceId) =>
        Joey.Cards.FixedSpellEffects.LifePoints(resourceId) is not null;

    public static bool SupportsQueued(Card card) =>
        card.ClassCode == 22 && SupportsQueued(card.Id) ||
        card is { ClassCode: 21, Id: Joey.Cards.CountedAndContinuousEffects.GiftOfTheMysticalElf };

    public static LifePointSpellChange? ResolveQueued(ushort resourceId, int caster)
    {
        if (caster is not (0 or 1)) return null;
        return Joey.Cards.FixedSpellEffects.LifePoints(resourceId) is { } effect
            ? new LifePointSpellChange(effect.CasterDelta, effect.OpponentDelta, effect.JointDamage)
            : null;
    }

    public static LifePointSpellChange? ResolveDemo(ushort resourceId, int caster) =>
        Supports(resourceId) ? ResolveQueued(resourceId, caster) : null;
}
