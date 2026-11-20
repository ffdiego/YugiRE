namespace Joey.Core;

/// <summary>
/// Audit of an automatic, non-targeting Deck movement effect. The current
/// sandbox has no chain, draw-response triggers or native card-move events.
/// </summary>
public sealed record DemoDeckFlowResult(
    ushort EffectResourceId, int Controller, int AffectedPlayer,
    int Requested, int MovedFromDeck, int KeptInHand,
    int SentToGraveyard, int NewlyDrawnSpellsDiscarded, bool DrawFailed);

/// <summary>
/// Reusable low-level deck transfer operations for automatic card effects.
/// Both methods use the physical Queue order as the observed deck top.
/// </summary>
public static class DeckFlowRules
{
    /// <summary>
    /// A mill is not a draw. Running out of cards while milling does not
    /// itself cause deck-out; the next mandatory draw will still check.
    /// </summary>
    public static DemoDeckFlowResult SendTopToGraveyard(
        ushort effectResourceId, int controller, int target,
        Queue<Card> deck, IList<Card> graveyard, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        int moved = 0;
        while (moved < count && deck.TryDequeue(out Card? card))
        {
            graveyard.Add(card);
            moved++;
        }
        return new(effectResourceId, controller, target, count, moved, 0, moved, 0, false);
    }

    /// <summary>
    /// Draws up to count cards and discards only newly drawn Spells, preserving
    /// the pre-existing hand and the draw order of Spells sent to the GY.
    /// Spell=22 and Trap=21 in the original DAT, even though other sandbox
    /// test fixtures can contain deliberately synthetic class codes.
    /// </summary>
    public static DemoDeckFlowResult DrawThenDiscardNewSpells(
        ushort effectResourceId, int controller, int target,
        Queue<Card> deck, IList<Card> hand, IList<Card> graveyard, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        int originalHandCount = hand.Count;
        int drawn = 0;
        while (drawn < count && deck.TryDequeue(out Card? card))
        {
            hand.Add(card);
            drawn++;
        }

        int discarded = 0;
        for (int i = 0; i < drawn; i++)
        {
            int currentIndex = originalHandCount + i - discarded;
            var card = hand[currentIndex];
            if (card.ClassCode != 22) continue;
            hand.RemoveAt(currentIndex);
            graveyard.Add(card);
            discarded++;
        }
        return new(effectResourceId, controller, target, count, drawn,
            drawn - discarded, discarded, discarded, drawn < count);
    }
}
