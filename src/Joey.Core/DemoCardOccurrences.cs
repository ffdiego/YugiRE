namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Each imported physical copy has its own immutable Card value instance.
    // Value equality stays suitable for catalogue/recipe tests; identity is
    // explicitly reference-keyed here and exposed as a per-duel numeric ID.
    private readonly Dictionary<Card, CardOccurrenceId> occurrenceIds =
        new(ReferenceEqualityComparer.Instance);
    private long nextCardOccurrenceId;

    private Card NewOccurrence(Card template)
    {
        var card = template with { };
        occurrenceIds.Add(card, new(checked(++nextCardOccurrenceId)));
        return card;
    }

    /// <summary>Capture the occurrence in a supported zone, not a slot or resource ID.
    /// Direct fixture writes of unregistered/aliased Card objects fail closed.</summary>
    public bool TryCaptureCardTarget(int side, DuelZone zone, int index, out CardTarget target)
    {
        target = default;
        if (side is not (0 or 1) || index < 0) return false;
        Card? card = zone switch
        {
            DuelZone.Hand when index < hands[side].Count => hands[side][index],
            DuelZone.Deck when index < decks[side].Count => decks[side].ElementAt(index),
            DuelZone.Graveyard when index < graves[side].Count => graves[side][index],
            DuelZone.Banished when index < banished[side].Count => banished[side][index],
            DuelZone.Monster when index < 5 => Monsters[side, index],
            DuelZone.SpellTrap when index < 5 => Spells[side, index],
            DuelZone.FieldSpell when index == 0 => FieldSpells[side],
            _ => null
        };
        if (card is null || !occurrenceIds.TryGetValue(card, out var id) || !IsUniqueOccurrence(card))
            return false;
        target = new(id, side, zone);
        return true;
    }

    // Read-only guard for the explicit laboratory producer; keep resolution private.
    internal bool MatchesOccurrenceResource(CardTarget target, ushort resourceId) =>
        TryResolveCardTarget(target, out _, out var card) && card.Id == resourceId;

    private bool TryResolveCardTarget(CardTarget target, out int index, out Card card)
    {
        index = -1;
        card = null!;
        int side = target.Side;
        if (side is not (0 or 1)) return false;
        int count = target.Zone switch
        {
            DuelZone.Hand => hands[side].Count,
            DuelZone.Deck => decks[side].Count,
            DuelZone.Graveyard => graves[side].Count,
            DuelZone.Banished => banished[side].Count,
            DuelZone.Monster or DuelZone.SpellTrap => 5,
            DuelZone.FieldSpell => 1,
            _ => 0
        };
        for (int i = 0; i < count; i++)
        {
            if (!TryCaptureCardTarget(side, target.Zone, i, out var found) || found != target)
                continue;
            index = i;
            card = target.Zone switch
            {
                DuelZone.Hand => hands[side][i],
                DuelZone.Deck => decks[side].ElementAt(i),
                DuelZone.Graveyard => graves[side][i],
                DuelZone.Banished => banished[side][i],
                DuelZone.Monster => Monsters[side, i]!,
                DuelZone.SpellTrap => Spells[side, i]!,
                DuelZone.FieldSpell => FieldSpells[side]!,
                _ => throw new InvalidOperationException("Unsupported target zone.")
            };
            return true;
        }
        return false;
    }

    private bool IsUniqueOccurrence(Card card)
    {
        int count = 0;
        for (int side = 0; side < 2; side++)
        {
            count += hands[side].Count(c => ReferenceEquals(c, card));
            count += decks[side].Count(c => ReferenceEquals(c, card));
            count += graves[side].Count(c => ReferenceEquals(c, card));
            count += fusionDecks[side].Count(c => ReferenceEquals(c, card));
            count += banished[side].Count(c => ReferenceEquals(c, card));
            if (ReferenceEquals(FieldSpells[side], card)) count++;
            for (int slot = 0; slot < 5; slot++)
            {
                if (ReferenceEquals(Monsters[side, slot], card)) count++;
                if (ReferenceEquals(Spells[side, slot], card)) count++;
            }
        }
        return count == 1;
    }
}
