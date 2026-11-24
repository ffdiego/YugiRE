namespace Joey.Core;

/// <summary>Reusable cost validation for inherent Special Summons from Hand.
/// P252: 0059DEF0 returns 3; 00599E30 scans own GY for class code 3.
/// Spirit Elimination, other summon sources and effects after summoning
/// are outside this bounded contract.</summary>
public static class GraveyardBanishSummonRules
{
    public const ushort DarkNecrofear = 1514;
    public const int FiendClass = 3;
    public const int DarkNecrofearCost = 3;

    public static bool IsEligible(ushort sourceId, Card candidate) =>
        sourceId == DarkNecrofear && candidate.IsMonster &&
        candidate.ClassCode == FiendClass;

    public static int RequiredCount(ushort sourceId) =>
        sourceId == DarkNecrofear ? DarkNecrofearCost : 0;

    // Native 0059DEF0: 0x5ea -> 3, 0x5eb -> 2, 0x5ec..0x5ef -> 1.
    // Native 00599E30 uses class 3 for Necrofear, same DAT attribute
    // as source for the other five. Keep legacy ID-only admission scoped
    // to Necrofear until the UI/CPU routes and other effects are migrated.
    public static int QueuedRequiredCount(Card source) =>
        !source.IsMonster || !NormalSummonRules.RequiresSpecialSummon(source) ? 0 : source.Id switch
        {
            DarkNecrofear => 3,
            1515 => 2,
            >= 1516 and <= 1519 => 1,
            _ => 0
        };

    public static bool QueuedIsEligible(Card source, Card candidate)
    {
        if (QueuedRequiredCount(source) == 0 || !candidate.IsMonster) return false;
        if (source.Id == DarkNecrofear) return candidate.ClassCode == FiendClass;
        uint attribute = source.Properties >> 29 & 7;
        return attribute is >= 1 and <= 6 && (candidate.Properties >> 29 & 7) == attribute;
    }

    public static IReadOnlyList<CardSelectionCandidate> Candidates(
        ushort sourceId, IReadOnlyList<Card> graveyard) =>
        Array.AsReadOnly(graveyard.Select((c, i) => new CardSelectionCandidate(i, c))
            .Where(c => IsEligible(sourceId, c.Card)).ToArray());

    // Validate exact source positions before mutating either zone. Distinct
    // indices, not card IDs or reference equality alone, represent copies.
    public static bool CanPay(ushort sourceId, IReadOnlyList<Card> graveyard,
        IReadOnlyList<CardSelectionCandidate> selected) =>
        RequiredCount(sourceId) > 0 &&
        selected.Count == RequiredCount(sourceId) &&
        selected.Select(c => c.SourceIndex).Distinct().Count() == selected.Count &&
        selected.All(c => c.SourceIndex >= 0 && c.SourceIndex < graveyard.Count &&
            ReferenceEquals(graveyard[c.SourceIndex], c.Card) && IsEligible(sourceId, c.Card));
}
