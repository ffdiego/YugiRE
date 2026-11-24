namespace Joey.Core;

/// <summary>
/// P051/P052 same 00584E60 primary damage callback, different pre-callback
/// costs. Result is sandbox provenance, not a native command transcript.
/// </summary>
public enum FlipBanishCostSource { Deck, Hand }

public sealed record DemoFlipBanishBurnResult(
    ushort EffectResourceId, int Owner, FlipBanishCostSource CostSource,
    int RequiredCount, IReadOnlyList<Card> BanishedCards, int Damage, bool Applied);

public static class FlipBanishBurnRules
{
    public const ushort LadyAssailantOfFlames = 1435; // P051
    public const ushort FireSorcerer = 1436;          // P052
    public const int BurnDamage = 800;

    public static FlipBanishCostSource? Cost(ushort id) => id switch
    {
        LadyAssailantOfFlames => FlipBanishCostSource.Deck,
        FireSorcerer => FlipBanishCostSource.Hand,
        _ => null
    };
    public static int RequiredCount(ushort id) => id switch
    {
        LadyAssailantOfFlames => 3,
        FireSorcerer => 2,
        _ => 0
    };

    internal static int[] SampleHandIndices(int count, int required, Random random)
    {
        if (required < 0 || count < required)
            throw new ArgumentOutOfRangeException(nameof(required));
        int[] indices = Enumerable.Range(0, count).ToArray();
        random.Shuffle(indices);
        return indices.Take(required).ToArray();
    }

    /// <summary>
    /// This operation is atomic with respect to card-zone mutations.
    /// When the required number of occurrences does not exist, no cards
    /// are banished, no random value is consumed, and no damage is emitted.
    /// Caller applies the reported damage through its normal LP path.
    /// </summary>
    public static DemoFlipBanishBurnResult Resolve(
        ushort resourceId, int owner, Queue<Card> deck, IList<Card> hand,
        IList<Card> banished, Random random)
    {
        if (owner is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(owner));
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(banished);
        ArgumentNullException.ThrowIfNull(random);
        var source = Cost(resourceId) ??
            throw new ArgumentOutOfRangeException(nameof(resourceId), "Unsupported banish-burn FLIP.");
        int required = RequiredCount(resourceId);
        int available = source == FlipBanishCostSource.Deck ? deck.Count : hand.Count;
        if (available < required)
            return new(resourceId, owner, source, required, Array.Empty<Card>(), 0, false);

        Card[] removed;
        if (source == FlipBanishCostSource.Deck)
        {
            removed = new Card[required];
            for (int i = 0; i < required; i++)
                removed[i] = deck.Dequeue(); // Physical Queue order is deck top.
        }
        else
        {
            // Seeded sandbox permutation; original native RNG is NOT asserted.
            // Sample indices without replacement, then remove descending to
            // avoid shifting an unchosen occurrence or losing duplicate copies.
            int[] selected = SampleHandIndices(hand.Count, required, random);
            removed = selected.Select(index => hand[index]).ToArray();
            foreach (int index in selected.OrderByDescending(index => index))
                hand.RemoveAt(index);
        }
        foreach (Card card in removed) banished.Add(card);
        return new(resourceId, owner, source, required, Array.AsReadOnly(removed),
            BurnDamage, true);
    }
}
