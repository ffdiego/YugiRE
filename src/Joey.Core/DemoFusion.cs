namespace Joey.Core;

public sealed partial class DemoDuel
{
    private readonly List<Card>[] fusionDecks = [[], []];
    public PendingFusionSummon? PendingFusionSummon { get; private set; }

    public IReadOnlyList<Card> FusionDeck(int player) => player is 0 or 1
        ? fusionDecks[player].AsReadOnly() : throw new ArgumentOutOfRangeException(nameof(player));

    private void LoadFusionDeck(int player, IReadOnlyList<Card>? cards)
    {
        if (cards is null) return;
        if (cards.Count > FusionRules.FusionDeckLimit || cards.Any(card => !FusionRules.IsFusionMonster(card)))
            throw new ArgumentException("A Fusion Deck must contain at most 15 Fusion Monsters.", nameof(cards));
        fusionDecks[player].AddRange(cards);
    }

    private IEnumerable<FusionSummonOption> FusionOptions(int owner)
    {
        bool full = FirstFreeMonsterZone() < 0;
        var hand = hands[owner].Select((card, index) => new FusionMaterial(FusionMaterialZone.Hand, index, card))
            .Where(material => material.Card.IsMonster);
        var field = Enumerable.Range(0, 5)
            .Where(zone => Monsters[owner, zone] is Card card && card.IsMonster)
            .Select(zone => new FusionMaterial(FusionMaterialZone.Field, zone, Monsters[owner, zone]!));
        // If the board is full, choosing field occurrences first maximizes
        // the chance of freeing a zone without changing the material quotas.
        var candidates = (full ? field.Concat(hand) : hand.Concat(field)).ToArray();

        for (int index = 0; index < fusionDecks[owner].Count; index++)
        {
            Card result = fusionDecks[owner][index];
            if (FusionRules.Recipe(result) is not { } recipe) continue;
            var eligible = candidates.Where(material => FusionRules.MaySupply(recipe, material)).ToArray();
            if (FusionRules.Complete(recipe, eligible, [], full) is { } suggested)
                yield return new(index, result, recipe, Array.AsReadOnly(eligible), Array.AsReadOnly(suggested));
        }
    }

    private bool BeginFusionSummon(Card spell, int source, bool fromSet, out string message)
    {
        var options = FusionOptions(ActivePlayer).ToArray();
        if (options.Length == 0)
        {
            message = "No Fusion Monster has matching materials and a legal destination.";
            return false;
        }
        PendingFusionSummon = new(ActivePlayer, spell, source, fromSet, Array.AsReadOnly(options));
        message = $"{spell.Name}: choose a Fusion Monster and its materials.";
        return true;
    }

    public bool CancelFusionSummon(out string message)
    {
        message = "No Fusion Summon is pending.";
        if (PendingFusionSummon is null) return false;
        PendingFusionSummon = null;
        message = "Fusion selection canceled.";
        return true;
    }

    public bool CanCompleteFusionSelection(FusionSummonOption option, IReadOnlyList<FusionMaterial> selected) =>
        PendingFusionSummon is { } pending && pending.Options.Contains(option) &&
        selected.All(option.Materials.Contains) &&
        FusionRules.Complete(option.Recipe, option.Materials, selected, FirstFreeMonsterZone() < 0) is not null;

    public bool ResolveFusionSummon(int deckIndex, IReadOnlyList<FusionMaterial> materials, out string message,
        bool defense = false)
    {
        message = "Choose separate occurrences that match the recipe.";
        if (GameOver || Phase != DemoPhase.Main || PendingFusionSummon is not { } pending ||
            pending.Owner != ActivePlayer ||
            pending.Options.FirstOrDefault(option => option.DeckIndex == deckIndex) is not { } choice ||
            materials.Count != choice.Recipe.Count ||
            FusionRules.Complete(choice.Recipe, [], materials, needsField: false) is null)
            return false;

        int owner = pending.Owner;
        if (deckIndex >= fusionDecks[owner].Count || !ReferenceEquals(fusionDecks[owner][deckIndex], choice.Result) ||
            materials.Any(material => !choice.Materials.Contains(material) ||
                !ReferenceEquals(material.Card, FusionMaterialAt(owner, material))))
        {
            message = "A selected card is no longer in its original position.";
            return false;
        }

        int source = pending.SourceIndex;
        if (pending.FromSet
            ? !SpellFaceDown[owner, source] || !ReferenceEquals(Spells[owner, source], pending.Spell)
            : source >= Hand.Count || !ReferenceEquals(Hand[source], pending.Spell) || FirstFreeSpellZone() < 0)
        {
            message = "The activating Spell is no longer available.";
            return false;
        }

        var fieldSlots = materials.Where(material => material.Zone == FusionMaterialZone.Field)
            .Select(material => material.Index).ToArray();
        int destination = FirstFreeMonsterZone(fieldSlots);
        if (destination < 0)
        {
            message = "Choose a material from the field to free a Monster Zone.";
            return false;
        }

        // Commit only after the source, result, materials and destination are validated.
        // Finalize movement and placement before offering any material's Graveyard effect.
        deferringFieldRemovalTriggers = true;
        try
        {
            PendingFusionSummon = null;
            if (pending.FromSet) ClearSpellTrapSlot(owner, source);
            var spentHandSlots = materials.Where(material => material.Zone == FusionMaterialZone.Hand)
                .Select(material => material.Index);
            if (!pending.FromSet) spentHandSlots = spentHandSlots.Append(source);
            foreach (int index in spentHandSlots.OrderDescending())
                hands[owner].RemoveAt(index);
            graves[owner].Add(pending.Spell);
            foreach (var material in materials)
            {
                if (material.Zone == FusionMaterialZone.Hand) graves[owner].Add(material.Card);
                else RemoveMonster(owner, material.Index, isFusionMaterial: true);
            }
            fusionDecks[owner].RemoveAt(deckIndex);
            PlaceSelectedMonster(owner, destination, choice.Result, false, faceUpDefense: defense);
        }
        finally { deferringFieldRemovalTriggers = false; }

        BeginNextQueuedDeckSearch();
        message = $"Fusion Summoned {choice.Result.Name}.";
        return true;
    }

    private Card? FusionMaterialAt(int owner, FusionMaterial material) => material.Zone switch
    {
        FusionMaterialZone.Hand when material.Index >= 0 && material.Index < Hand.Count => Hand[material.Index],
        FusionMaterialZone.Field when material.Index is >= 0 and < 5 => Monsters[owner, material.Index],
        _ => null
    };
}
