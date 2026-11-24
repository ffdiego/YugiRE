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
