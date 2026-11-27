namespace Joey.Core;

public sealed record DemoSpellTrapTarget(int Player, int Zone, Card Card)
{
    public bool IsFieldZone => Zone == SpellTrapTargetRules.FieldZone;
}

public sealed record PendingSpellTrapTarget(int Owner, Card Spell, int SourceIndex,
    bool FromSet, IReadOnlyList<DemoSpellTrapTarget> EligibleTargets);

public static class SpellTrapTargetRules
{
    public const ushort MysticalSpaceTyphoon = 1079;
    public const ushort RemoveTrap = 1004;
    public const ushort Spiritualism = 1539;
    public const int FieldZone = 5;

    public static bool Supports(ushort resourceId) => resourceId is MysticalSpaceTyphoon or RemoveTrap;

    // Spiritualism is queue-only: the legacy targeted resolver destroys.
    public static bool SupportsQueued(ushort resourceId) => Supports(resourceId) || ReturnsToHand(resourceId) ||
        resourceId == Joey.Cards.PositionAndRemovalEffects.DeSpell;
    internal static bool ReturnsToHand(ushort resourceId) => resourceId == Spiritualism;
    internal static bool AcceptsSide(ushort resourceId, int caster, int targetSide) =>
        !ReturnsToHand(resourceId) || caster != targetSide;

    // Native 0057B530 accepts occupied slots 5–10. 0057B270
    // narrows that range to face-up class 21 for Remove Trap.
    internal static bool Accepts(ushort resourceId, Card card, bool faceDown) =>
        resourceId == MysticalSpaceTyphoon ||
        resourceId == Joey.Cards.PositionAndRemovalEffects.DeSpell && !faceDown && card.ClassCode == 22 ||
        resourceId == Spiritualism && card.ClassCode is 21 or 22 ||
        resourceId == RemoveTrap && card.ClassCode == 21 && !faceDown;
}
