using Joey.Core;

internal static class DuelArtworkPolicy
{
    // Native 0041A800 loads independent large and miniature image banks.
    // A missing miniature must not silently fall back to the large image.
    internal static string Require(Card card, string? resource, bool miniature)
    {
        if (!string.IsNullOrWhiteSpace(resource)) return resource;
        throw new InvalidDataException(
            $"Missing original {(miniature ? "miniature" : "large artwork")} for card ID {card.Id}, index {card.Index}: {card.Name}.");
    }

    internal static void ValidateMappings(IEnumerable<Card> cards,
        Func<Card,string?> large, Func<Card,string?> mini)
    {
        foreach (var card in cards)
        {
            Require(card,large(card),false);
            Require(card,mini(card),true);
        }
    }

    // The unused-record inspection is explicitly opt-in and not a duel.
    internal static bool AllowInspectionFallback(bool includeUnused, bool inDuel, bool playable) =>
        includeUnused && !inDuel && !playable;

    internal static string RequireBack(string? resource, bool miniature)
    {
        if (!string.IsNullOrWhiteSpace(resource)) return resource;
        throw new InvalidDataException(
            $"Missing original {(miniature ? "mini/card_ura.bmp" : "card/card_ura.bmp")} card back; no faithful placeholder.");
    }
}
