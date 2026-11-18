namespace Joey.Core;

/// <summary>Position in the source zone (not the DAT catalog index) identifies each copy.</summary>
public enum CardSelectionSource { Deck, Graveyard }
public enum CardSelectionDestination { Hand, Field }
public enum CardSelectionFilter { Spell, Trap }
public sealed record CardSelectionCandidate(int SourceIndex, Card Card);

public sealed record PendingCardSelection(
    int Owner, CardSelectionSource Source, CardSelectionDestination Destination,
    CardSelectionFilter Filter, IReadOnlyList<CardSelectionCandidate> EligibleCards,
    int MinSelections, int MaxSelections, ushort EffectResourceId);

/// <summary>
/// Shared decision contract. DemoDuel currently resolves only Graveyard -> Hand.
/// Deck searches and special summons will extend the same contract later.
/// </summary>
public static class CardSelectionRules
{
    public static CardSelectionFilter? FlipGraveyardFilter(ushort resourceId) => resourceId switch
    {
        FlipEffectRules.MagicianOfFaith => CardSelectionFilter.Spell,
        FlipEffectRules.MaskOfDarkness => CardSelectionFilter.Trap,
        _ => null
    };

    // Original DAT: class 22 = Spell, 21 = Trap (not the reverse).
    public static bool IsEligible(Card card, CardSelectionFilter filter) => filter switch
    {
        CardSelectionFilter.Spell => card.ClassCode == 22,
        CardSelectionFilter.Trap => card.ClassCode == 21,
        _ => false
    };

    public static IReadOnlyList<CardSelectionCandidate> Candidates(
        IReadOnlyList<Card> source, CardSelectionFilter filter) =>
        Array.AsReadOnly(source.Select((card, index) => new CardSelectionCandidate(index, card))
            .Where(option => IsEligible(option.Card, filter)).ToArray());
}
