namespace Joey.Core;

/// <summary>
/// Bounded F-I-N-A-L field contract. Resource IDs are from the Joey card
/// identities in the global decompilation, NOT internal card indexes.
/// Full effect timing and card-removal cleanup still require native traces.
/// </summary>
public static class DestinyBoardRules
{
    public const ushort BoardResourceId = 1528;
    public static readonly IReadOnlyList<ushort> MessageResourceIds =
        Array.AsReadOnly(new ushort[] { 1541, 1542, 1543, 1544 });

    public static bool IsMessage(ushort id) => MessageResourceIds.Contains(id);

    public static bool HasActiveBoard(int player, Card?[,] spells, bool[,] faceDown)
    {
        Validate(player, spells, faceDown);
        return Enumerable.Range(0, 5).Any(zone => !faceDown[player, zone] &&
            spells[player, zone]?.Id == BoardResourceId);
    }

    /// <summary>
    /// Choose the next missing message only if all earlier messages are
    /// already face-up and no later one is out of order. Caller must also
    /// verify that the other player's End Phase is being resolved.
    /// </summary>
    public static ushort? NextMessage(int player, Card?[,] spells, bool[,] faceDown)
    {
        if (!HasActiveBoard(player, spells, faceDown)) return null;
        for (int i = 0; i < MessageResourceIds.Count; i++)
        {
            ushort id = MessageResourceIds[i];
            if (ContainsFaceUp(player, spells, faceDown, id)) continue;
            return MessageResourceIds.Skip(i + 1).Any(later =>
                ContainsFaceUp(player, spells, faceDown, later)) ? null : id;
        }
        return null;
    }

    /// <summary>
    /// Completion is a FIELD condition, unlike Exodia's hand condition.
    /// Returns original Card snapshots in Board/I/N/A/L order.
    /// </summary>
    public static IReadOnlyList<Card>? CompleteField(
        int player, Card?[,] spells, bool[,] faceDown)
    {
        Validate(player, spells, faceDown);
        ushort[] required = [BoardResourceId, .. MessageResourceIds];
        var pieces = new Card[required.Length];
        for (int i = 0; i < required.Length; i++)
        {
            ushort id = required[i];
            Card? found = Enumerable.Range(0, 5)
                .Where(zone => !faceDown[player, zone])
                .Select(zone => spells[player, zone])
                .FirstOrDefault(card => card?.Id == id);
            if (found is null) return null;
            pieces[i] = found;
        }
        return Array.AsReadOnly(pieces);
    }

    private static bool ContainsFaceUp(int player, Card?[,] spells,
        bool[,] faceDown, ushort id) =>
        Enumerable.Range(0, 5).Any(zone =>
            !faceDown[player, zone] && spells[player, zone]?.Id == id);

    private static void Validate(int player, Card?[,] spells, bool[,] faceDown)
    {
        if (player is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(player));
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(faceDown);
        if (spells.GetLength(0) < 2 || spells.GetLength(1) < 5 ||
            faceDown.GetLength(0) < 2 || faceDown.GetLength(1) < 5)
            throw new ArgumentException("Expected both players and five spell/trap zones.");
    }
}
