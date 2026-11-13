using Joey.Core;

// Independent catalog base attributes (not parsed from the F40 stats output).
// The native 12-byte buffers below are comparison targets only.
// A manifest declared empty here is a scenario, not proof of a real duel.
internal static class PlainMonsterExpansionChecks
{
    private sealed record Vector(int Index, ushort Resource, string Name, int Type,
        int Attribute, int Attack, int Defense, string NativeBuffer);
    private static readonly Vector[] Verified =
    [
        new(24, 0x004d, "Axe Raider", 15, 5, 1700, 1150, "18000F05A40600007E040000"),
        new(550, 0x0213, "Gemini Elf", 18, 5, 1900, 900, "260212056C07000084030000"),
        new(733, 0x057f, "Amphibian Beast", 8, 3, 2400, 2000, "DD02080360090000D0070000"),
        new(857, 0x0491, "Darkfire Soldier #2", 4, 4, 1700, 1100, "59030404A40600004C040000"),
        new(954, 0x05de, "The Gross Ghost of Fled Dreams", 3, 2, 1300, 1800, "BA0303021405000008070000"),
        new(380, 0x0541, "Island Turtle", 14, 3, 1100, 2000, "7C010E034C040000D0070000"),
        new(525, 0x003f, "Tiger Axe", 12, 5, 1300, 1100, "0D020C05140500004C040000"),
        new(1099, 0x02f8, "Alligator's Sword", 11, 5, 1500, 1200, "4B040B05DC050000B0040000")
    ];

    private static readonly (int Index, ushort Resource)[] UnsupportedEffects =
    [
        (261, 0x023d), // Witch of the Black Forest — search on graveyard
        (453, 0x045b), // Nimble Momonga — battle destruction
        (364, 0x02e8), // Panther Warrior — attack tribute gate
        (515, 0x05f3), // Zombyra the Dark — conditional ATK reduction
    ];

    private static Card Synthetic(Vector v)
    {
        uint properties = ((uint)v.Attribute << 29) | ((uint)v.Type << 20) |
            ((uint)(v.Attack / 10) << 9) | (uint)(v.Defense / 10);
        return new Card(v.Index, v.Resource, v.Name, "", properties);
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (var v in Verified)
        {
            var card = Synthetic(v);
            var calculated = NativeAiAttributes.FromPlainCard(card, NativeAiAttributeContext.Resolved());
            check(card.IsMonster && calculated.IsSupported &&
                calculated.Value.CardIndex == v.Index &&
                calculated.Value.TypeCode == v.Type &&
                calculated.Value.AttributeCode == v.Attribute &&
                calculated.Value.Attack == v.Attack &&
                calculated.Value.Defense == v.Defense &&
                Convert.ToHexString(calculated.Value.ToBytes()) == v.NativeBuffer,
                $"Closed normal-monster base {v.Name} reproduces its native reference buffer under hypothetical empty modifiers");
            check(!NativeAiAttributes.FromPlainCard(card).IsSupported &&
                !NativeAiAttributes.FromPlainCard(card, NativeAiAttributeContext.Unknown).IsSupported,
                $"{v.Name} remains unsupported without a resolved modifier manifest");
        }
        foreach (var (index, resource) in UnsupportedEffects)
        {
            var unsupported = new Card(index, resource, "Effect card", "",
                (15u << 20) | (5u << 29) | (100u << 9) | 100u);
            check(!NativeAiAttributes.FromPlainCard(unsupported, NativeAiAttributeContext.Resolved()).IsSupported,
                $"Effect resource 0x{resource:X4} remains outside the closed normal-monster policy");
        }
        var bonus = NativeAiAttributes.FromPlainCard(Synthetic(Verified[2]),
            NativeAiAttributeContext.Resolved([0x0291]));
        check(bonus.IsSupported && bonus.Value.Attack == 2700 && bonus.Value.Defense == 2300,
            "Known equipment contribution still applies to the new closed plain subset");
        check(!NativeAiAttributes.FromPlainCard(Synthetic(Verified[2]),
            NativeAiAttributeContext.Resolved([0xffff])).IsSupported,
            "Unresolved equipment rejects expanded normal monsters instead of silently defaulting to zero");
    }

    public static void RunCatalog(Action<bool, string> check, Archive archive)
    {
        var cards = Catalog.Load(archive).ToDictionary(c => c.Index);
        foreach (var v in Verified)
        {
            if (!cards.TryGetValue(v.Index, out var card))
                throw new InvalidDataException($"Missing pinned card index {v.Index}");
            var calculated = NativeAiAttributes.FromPlainCard(card, NativeAiAttributeContext.Resolved());
            check(card.Id == v.Resource && card.Name == v.Name && card.ClassCode == v.Type &&
                ((card.Properties >> 29) & 7) == (uint)v.Attribute &&
                card.Attack == v.Attack && card.Defense == v.Defense &&
                calculated.IsSupported &&
                Convert.ToHexString(calculated.Value.ToBytes()) == v.NativeBuffer,
                $"Catalog-derived {v.Name} base stats match the independently captured bytes under an explicitly declared empty manifest");
        }
        foreach (var (index, resource) in UnsupportedEffects)
        {
            check(cards.TryGetValue(index, out var card) && card.Id == resource &&
                !NativeAiAttributes.FromPlainCard(card, NativeAiAttributeContext.Resolved()).IsSupported,
                $"DAT effect resource 0x{resource:X4} is still excluded");
        }
    }
}
