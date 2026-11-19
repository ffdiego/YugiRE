namespace Joey.Core;

/// <summary>Position in the source zone (not the DAT catalog index) identifies each copy.</summary>
public enum CardSelectionSource { Deck, Graveyard }
public enum CardSelectionDestination { Hand, Field }
public enum CardSelectionFilter { Spell, Trap, MonsterAttackAtMost1500, MonsterDefenseAtMost1500 }
public sealed record CardSelectionCandidate(int SourceIndex, Card Card);

public sealed record PendingCardSelection(
    int Owner, CardSelectionSource Source, CardSelectionDestination Destination,
    CardSelectionFilter Filter, IReadOnlyList<CardSelectionCandidate> EligibleCards,
    int MinSelections, int MaxSelections, ushort EffectResourceId);

/// <summary>
/// Shared selection contract: currently resolves Graveyard -> Hand (P018)
/// and Deck -> Hand (P006), not Deck -> Field (P164).
/// </summary>
public static class CardSelectionRules
{
    public static CardSelectionFilter? FlipGraveyardFilter(ushort resourceId) => resourceId switch
    {
        FlipEffectRules.MagicianOfFaith => CardSelectionFilter.Spell,
        FlipEffectRules.MaskOfDarkness => CardSelectionFilter.Trap,
        _ => null
    };

    public static CardSelectionFilter? OnFieldToGraveyardFilter(ushort resourceId) => resourceId switch
    {
        47 => CardSelectionFilter.MonsterAttackAtMost1500, // Sangan, callback P006
        573 => CardSelectionFilter.MonsterDefenseAtMost1500, // Witch of the Black Forest, P006
        _ => null
    };

    // Original DAT: class 22 = Spell, 21 = Trap (not the reverse).
    public static bool IsEligible(Card card, CardSelectionFilter filter) => filter switch
    {
        CardSelectionFilter.Spell => card.ClassCode == 22,
        CardSelectionFilter.Trap => card.ClassCode == 21,
        CardSelectionFilter.MonsterAttackAtMost1500 => card.IsMonster && card.Attack <= 1500,
        CardSelectionFilter.MonsterDefenseAtMost1500 => card.IsMonster && card.Defense <= 1500,
        _ => false
    };

    public static IReadOnlyList<CardSelectionCandidate> Candidates(
        IReadOnlyList<Card> source, CardSelectionFilter filter) =>
        Array.AsReadOnly(source.Select((card, index) => new CardSelectionCandidate(index, card))
            .Where(option => IsEligible(option.Card, filter)).ToArray());
}
