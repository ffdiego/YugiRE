namespace Joey.Core;

/// <summary>FLIP selection operation; target identity and resolution are distinct.</summary>
public enum FlipSpellTrapOperation { Destroy, ReturnToOwnerHand }

/// <summary>
/// A mandatory pair of S/T targets for one FLIP request, independent of the
/// Main Phase spell-activation selector. No native chain or targeting parity
/// is claimed. DemoSpellTrapTarget retains the original card occurrence.
/// </summary>
public sealed record PendingFlipSpellTrapTargets(
    int Owner, Card Source, FlipSpellTrapOperation Operation, bool RequireFaceDown,
    IReadOnlyList<DemoSpellTrapTarget> EligibleTargets)
{
    public const int RequiredCount = 2;
}

public static class FlipSpellTrapSelectionRules
{
    public static FlipSpellTrapOperation? Effect(ushort resourceId) => resourceId switch
    {
        FlipEffectRules.Greenkappa => FlipSpellTrapOperation.Destroy,
        FlipEffectRules.TornadoBird => FlipSpellTrapOperation.ReturnToOwnerHand,
        _ => null
    };

    public static bool IsEligible(ushort resourceId, Card card, bool faceDown) =>
        card.ClassCode is 21 or 22 &&
        (resourceId == FlipEffectRules.TornadoBird ||
         resourceId == FlipEffectRules.Greenkappa && faceDown);

    /// <summary>
    /// Native S/T slots 5..10 map to the five ordinary slots and a Field Zone.
    /// Both sides are included. Identity and face state must be revalidated.
    /// </summary>
    public static IReadOnlyList<DemoSpellTrapTarget> Candidates(
        ushort resourceId, Card?[,] spells, bool[,] faceDown,
        Card?[] fieldSpells, bool[] fieldFaceDown)
    {
        var result = new List<DemoSpellTrapTarget>();
        if (Effect(resourceId) is null) return result.AsReadOnly();
        for (int player = 0; player < 2; player++)
        for (int zone = 0; zone <= SpellTrapTargetRules.FieldZone; zone++)
        {
            bool field = zone == SpellTrapTargetRules.FieldZone;
            Card? card = field ? fieldSpells[player] : spells[player, zone];
            bool hidden = field ? fieldFaceDown[player] : faceDown[player, zone];
            if (card is not null && IsEligible(resourceId, card, hidden))
                result.Add(new(player, zone, card));
        }
        return result.AsReadOnly();
    }
}
