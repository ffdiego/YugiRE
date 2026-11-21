namespace Joey.LookAndFeel;

/// <summary>Keeps a hand's current X coordinates while its target fan changes.</summary>
public sealed class HandReflow<T> where T : class
{
    private List<(T Card, int X)> slots = [];
    private bool nearSide;

    public void Clear() => slots.Clear();

    public void RemoveAt(int index)
    {
        if ((uint)index < (uint)slots.Count) slots.RemoveAt(index);
    }

    public void Synchronize(IReadOnlyList<T> cards, bool near, int updates, bool animate)
    {
        if (near != nearSide) Clear();
        nearSide = near;
        bool sameCards = cards.Count == slots.Count;
        for (int i = 0; sameCards && i < cards.Count; i++)
            sameCards = ReferenceEquals(cards[i], slots[i].Card);

        if (!sameCards)
        {
            var next = new List<(T Card, int X)>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                int previous = slots.FindIndex(slot => ReferenceEquals(slot.Card, cards[i]));
                int x = previous < 0 ? HandPresentation.CenterX(i, cards.Count, near) : slots[previous].X;
                if (previous >= 0) slots.RemoveAt(previous);
                next.Add((cards[i], x));
            }
            slots = next;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var (card, x) = slots[i];
            int target = HandPresentation.CenterX(i, slots.Count, near);
            for (int update = 0; animate && update < updates && x != target; update++)
                x = HandPresentation.Approach(x, target);
            slots[i] = (card, animate ? x : target);
        }
    }

    public CardBounds Bounds(int index, int count, int lift = 0)
    {
        var bounds = HandPresentation.Bounds(index, count, nearSide, lift);
        return index < slots.Count ? bounds with { X = slots[index].X - bounds.Width / 2 } : bounds;
    }
}
