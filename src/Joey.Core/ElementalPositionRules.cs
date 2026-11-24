namespace Joey.Core;

// Pure card-text contract for the future phase producer (A07/A08).
// The exact native phase dispatch and resolution flags are not yet recovered.
// No new duel phase, pending prompt, mutation, or automatic trigger here.
public enum ElementalPositionOpportunity { Standby, End }
public readonly record struct ElementalPositionIntent(
    ushort SourceResourceId, int Controller, int TargetSide,
    ElementalPositionOpportunity Opportunity, bool RestrictsManualPositionForTurn);

public static class ElementalPositionRules
{
    public const ushort AquaSpirit = 1517;
    public const ushort Garuda = 1519;

    public static ElementalPositionIntent? AtOpportunity(ushort sourceId, int controller,
        int activeSide, ElementalPositionOpportunity opportunity, bool sourceFaceUp,
        bool sourceSuppressed, bool alreadyOfferedThisOpportunity, bool gameOver)
    {
        if (controller is not (0 or 1) || activeSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(controller));
        if (!Enum.IsDefined(opportunity)) throw new ArgumentOutOfRangeException(nameof(opportunity));
        if (controller == activeSide || !sourceFaceUp || sourceSuppressed ||
            alreadyOfferedThisOpportunity || gameOver) return null;
        return (sourceId, opportunity) switch
        {
            (AquaSpirit, ElementalPositionOpportunity.Standby) =>
                new(sourceId, controller, activeSide, opportunity, true),
            (Garuda, ElementalPositionOpportunity.End) =>
                new(sourceId, controller, activeSide, opportunity, false),
            _ => null
        };
    }

    // Only a current opposing face-up monster is eligible; selection remains
    // optional and is revalidated by the eventual occurrence-aware adapter.
    public static bool IsEligible(ElementalPositionIntent intent, int targetSide,
        Card? card, bool faceDown, bool markedForRemoval) =>
        intent.TargetSide == targetSide && targetSide != intent.Controller &&
        card is { IsMonster: true } && !faceDown && !markedForRemoval;
}
