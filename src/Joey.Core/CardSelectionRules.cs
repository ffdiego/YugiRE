namespace Joey.Core;

/// <summary>Position in the source zone (not the DAT catalog index) identifies each copy.</summary>
public enum CardSelectionSource { Deck, Graveyard }
public enum CardSelectionDestination { Hand, Field }
public enum CardSelectionFilter { Spell, Trap, MonsterAttackAtMost1500, MonsterDefenseAtMost1500, MonsterAttributeAttackAtMost1500 }
public sealed record CardSelectionCandidate(int SourceIndex, Card Card);

public sealed record PendingCardSelection(
    int Owner, CardSelectionSource Source, CardSelectionDestination Destination,
    CardSelectionFilter Filter, IReadOnlyList<CardSelectionCandidate> EligibleCards,
    int MinSelections, int MaxSelections, ushort EffectResourceId, byte? RequiredAttributeCode = null);

/// <summary>
/// Shared selection contract: currently resolves Graveyard -> Hand (P018)
/// Deck -> Hand (P006), and Deck -> Field (P164).
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

    /// <summary>
    /// Six P164 recruiters share callback 0x0058AC10. Attribute codes use
    /// DAT Properties >> 29: LIGHT=1, DARK=2, WATER=3, FIRE=4,
    /// EARTH=5, WIND=6 (also used by FieldSpellRules).
    /// </summary>
    public static byte? BattleRecruitAttribute(ushort resourceId) => resourceId switch
    {
        1123 => 2, // Mystic Tomato, DARK
        1108 => 5, // Giant Rat, EARTH
        1117 => 1, // Shining Angel, LIGHT
        1110 => 4, // UFO Turtle, FIRE
        1119 => 3, // Mother Grizzly, WATER
        1120 => 6, // Flying Kamakiri #1, WIND
        _ => null
    };

    // Original DAT: class 22 = Spell, 21 = Trap (not the reverse).
    public static bool IsEligible(Card card, CardSelectionFilter filter, byte? requiredAttributeCode = null) => filter switch
    {
        CardSelectionFilter.Spell => card.ClassCode == 22,
        CardSelectionFilter.Trap => card.ClassCode == 21,
        CardSelectionFilter.MonsterAttackAtMost1500 => card.IsMonster && card.Attack <= 1500,
        CardSelectionFilter.MonsterDefenseAtMost1500 => card.IsMonster && card.Defense <= 1500,
        CardSelectionFilter.MonsterAttributeAttackAtMost1500 =>
            card.IsMonster && card.Attack <= 1500 &&
            requiredAttributeCode is >= 1 and <= 6 &&
            (byte)(card.Properties >> 29 & 7) == requiredAttributeCode,
        _ => false
    };

    public static IReadOnlyList<CardSelectionCandidate> Candidates(
        IReadOnlyList<Card> source, CardSelectionFilter filter, byte? requiredAttributeCode = null) =>
        Array.AsReadOnly(source.Select((card, index) => new CardSelectionCandidate(index, card))
            .Where(option => IsEligible(option.Card, filter, requiredAttributeCode)).ToArray());
}
