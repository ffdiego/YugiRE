namespace Joey.Core;

public sealed partial class DemoDuel
{
    private sealed record CountedAttributeRule(int PerCard, Func<DemoDuel, int, int> Count, bool IncludesDefense = false);

    // 0056DD30 counts the current zones. The DELTA belongs to the
    // visible occurrence and is recalculated with field and equip modifiers.
    private static readonly Dictionary<ushort, CountedAttributeRule> CountedAttributeRules = new()
    {
        [11] = new(500, (d, side) => d.CountFaceUp(side, c => c.Id == 553)), // Swamp Battleguard
        [553] = new(500, (d, side) => d.CountFaceUp(side, c => c.Id == 11)), // Lava Battleguard
        [234] = new(100, (d, _) => d.CountFaceUpOnBothSides(c => c.ClassCode == 13)), // Wodan
        [406] = new(100, (d, _) => d.CountFaceUpOnBothSides(c => c.ClassCode == 7)), // Machine King
        [385] = new(300, (d, _) => d.CountFaceUpOnBothSides(c => c.Id is 61 or 1249), IncludesDefense: true),
        [367] = new(100, (d, side) => d.graves[side].Count(c => c.IsMonster)), // Shadow Ghoul
        [759] = new(300, (d, _) => d.graves.SelectMany(grave => grave).Count(c => c.Id is 34 or 721 or 1210 or 2034)),
        [808] = new(500, (d, side) => d.CountFaceUp(1 - side, c => c.ClassCode == 1) +
            d.graves[1 - side].Count(c => c.ClassCode == 1)), // Buster Blader
        [515] = new(300, (d, side) => d.hands[side].Count, IncludesDefense: true), // Muka Muka
        [1111] = new(-400, (d, side) => d.hands[side].Count, IncludesDefense: true), // Flash Assailant
    };

    private int CountFaceUp(int side, Func<Card, bool> matches) =>
        Enumerable.Range(0, 5).Count(z => !MonsterFaceDown[side, z] &&
            Monsters[side, z] is { } card && matches(card));

    private int CountFaceUpOnBothSides(Func<Card, bool> matches) =>
        CountFaceUp(0, matches) + CountFaceUp(1, matches);

    private NativeAiAttributeDelta CountedMonsterAttributes(int side, ushort resourceId)
    {
        if (!HasActionQueue || !CountedAttributeRules.TryGetValue(resourceId, out var rule)) return default;
        int delta = rule.PerCard * rule.Count(this, side);
        return new(delta, rule.IncludesDefense ? delta : 0);
    }
}
