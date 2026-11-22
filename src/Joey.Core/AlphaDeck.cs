namespace Joey.Core;

/// <summary>Curated 40-card alpha pool, resolved by resource ID so DAT variants cannot enter by name.</summary>
public static class AlphaDeck
{
    private static readonly (ushort Id, int Copies)[] Pool =
    [
        (77, 2), (25, 3), (40, 2), (378, 1), (90, 1), // Axe Raider, Battle Ox, Celtic Guardian, La Jinn, Mystic Horseman
        (439, 1), (1, 2), (73, 2), (1050, 2), // 7 Colored Fish, Mystical Elf, Giant Soldier, Neo
        (491, 1), (421, 1),                   // Hoshiningen, Jinzo #7
        (500, 2), (590, 1), (427, 2), (101, 1), // FLIP: Bug, Jar, Faith, Mask
        (FlipEffectRules.FourStarredLadybugOfDoom, 1), (FlipEffectRules.Skelengel, 1),
        (343, 1), (342, 1), (338, 1), (341, 1), // LP: Hinotama, Sparks, Red Medicine, Dian Keto
        (FusionRules.Polymerization, 1), (FusionRules.FusionGate, 1),
        (SpellTrapWipeRules.HarpiesFeatherDuster, 1),
        (SpellTrapTargetRules.MysticalSpaceTyphoon, 1),
        (DrawSpellRules.PotOfGreed, 1), (DrawSpellRules.UpstartGoblin, 1),
        (300, 1), (1002, 2), (1056, 1)         // Sword, Trap Hole, Mirror Force
    ];

    public static IReadOnlyList<Card> Create(IReadOnlyList<Card> catalog)
    {
        var deck = new List<Card>(40);
        foreach (var (id, copies) in Pool)
        {
            var matching = catalog.Where(c => c.Id == id && c.Name.Length > 0).ToArray();
            if (matching.Length != 1)
                throw new InvalidDataException($"Alpha needs one catalog entry for resource ID {id} (found {matching.Length}).");
            var card = matching[0];
            bool supported = card.ClassCode switch
            {
                21 => SummonTrapRules.Supports(id) || AttackTrapRules.Supports(id),
                22 => LifePointSpellRules.Supports(id) || EquipmentRules.Supports(id) ||
                    SpellTrapWipeRules.Supports(id) || SpellTrapTargetRules.Supports(id) ||
                    DrawSpellRules.Effect(card) is not null || FusionRules.Supports(card) || id == FusionRules.FusionGate,
                _ => card.IsMonster && card.Level is >= 1 and <= 4 && !NormalSummonRules.RequiresSpecialSummon(card)
            };
            if (!supported) throw new InvalidDataException("Unexpected properties for alpha resource " + id);
            deck.AddRange(Enumerable.Repeat(card, copies));
        }
        return deck.AsReadOnly();
    }

    public static IReadOnlyList<Card> CreateFusionDeck(IReadOnlyList<Card> catalog)
    {
        const ushort rabidHorseman = 91;
        var card = catalog.SingleOrDefault(card => card.Id == rabidHorseman);
        if (card is null || FusionRules.Recipe(card) is null)
            throw new InvalidDataException("Alpha needs Rabid Horseman in its Fusion Deck.");
        return Array.AsReadOnly(new[] { card });
    }
}
