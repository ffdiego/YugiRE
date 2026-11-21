namespace Joey.Core;

/// <summary>
/// The five distinct Forbidden One pieces win when held simultaneously.
/// This intentionally models the observable hand condition, not the native
/// callback ABI or the Exodia presentation object at 00453B60/00454010.
/// The shipped English DAT contains the corresponding card names.
/// </summary>
public static class ExodiaRules
{
    public static readonly IReadOnlyList<string> RequiredNames =
    [
        "Exodia the Forbidden One",
        "Right Arm of the Forbidden One",
        "Left Arm of the Forbidden One",
        "Right Leg of the Forbidden One",
        "Left Leg of the Forbidden One"
    ];

    /// <summary>
    /// Returns the source-card snapshots in Exodia display order. Distinct
    /// exact names are required: extra copies of an arm cannot substitute
    /// for a leg. Generic "Exodia" monster names do not count.
    /// </summary>
    public static IReadOnlyList<Card>? CompleteHand(IEnumerable<Card> hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        var pieces = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
        foreach (Card card in hand)
        {
            if (!card.IsMonster) continue;
            string name = card.Name.Trim();
            if (RequiredNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                pieces.TryAdd(name, card);
        }

        if (pieces.Count != RequiredNames.Count) return null;
        return Array.AsReadOnly(RequiredNames.Select(name => pieces[name]).ToArray());
    }
}
