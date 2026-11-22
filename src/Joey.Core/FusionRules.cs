namespace Joey.Core;

public enum FusionMaterialZone { Hand, Field }
public sealed record FusionMaterial(FusionMaterialZone Zone, int Index, Card Card);
public sealed record FusionSummonOption(int DeckIndex, Card Result, IReadOnlyList<ushort> Recipe,
    IReadOnlyList<FusionMaterial> Materials, IReadOnlyList<FusionMaterial> SuggestedMaterials);
public sealed record PendingFusionSummon(int Owner, Card Spell, int SourceIndex, bool FromSet,
    IReadOnlyList<FusionSummonOption> Options);

public static class FusionRules
{
    public const ushort Polymerization = 1003;
    public const ushort AlternatePolymerization = 1034;
    public const int FusionDeckLimit = 15;

    // Joey tables at 005ECF10/005ED048, decoded in card-static-sweep batch 12.
    private static readonly Dictionary<ushort, ushort[]> recipes = new()
    {
        [14] = [141, 292],
        [29] = [23, 99],
        [36] = [37, 38],
        [42] = [159, 160],
        [68] = [3, 15],
        [85] = [4, 200],
        [91] = [25, 90],
        [114] = [1, 103],
        [132] = [146, 209],
        [167] = [156, 199],
        [188] = [207, 281],
        [214] = [23, 243],
        [216] = [21, 81],
        [222] = [386, 646],
        [229] = [188, 250],
        [271] = [12, 100],
        [293] = [171, 295],
        [369] = [411, 511],
        [379] = [0, 0, 0],
        [384] = [94, 119],
        [388] = [235, 279],
        [408] = [556, 560],
        [426] = [6, 602],
        [436] = [438, 542],
        [441] = [392, 602, 625],
        [457] = [395, 536],
        [464] = [537, 635],
        [466] = [124, 465],
        [470] = [148, 559],
        [472] = [150, 290],
        [481] = [569, 643],
        [486] = [64, 566],
        [494] = [573, 611],
        [507] = [31, 416],
        [508] = [31, 80],
        [520] = [145, 580],
        [530] = [73, 432],
        [532] = [51, 479],
        [544] = [9, 35],
        [563] = [35, 96],
        [564] = [473, 625, 645],
        [571] = [634, 648],
        [593] = [5, 619],
        [612] = [424, 424],
        [616] = [429, 549],
        [617] = [233, 298],
        [638] = [274, 444],
        [706] = [161, 174],
        [712] = [81, 711],
        [1404] = [1403, 1402],
        [1445] = [1444, 1446],
        [812] = [3, 760],
        [1241] = [746, 1240],
        [1334] = [730, 1333],
        [1526] = [1523, 1525],
    };

    public static bool Supports(Card card) =>
        card.ClassCode == 22 && card.Id is Polymerization or AlternatePolymerization;

    public static bool IsFusionMonster(Card card) =>
        card.IsMonster && NormalSummonRules.Category(card) == 2;

    internal static IReadOnlyList<ushort>? Recipe(Card card)
    {
        if (!IsFusionMonster(card)) return null;
        var baseId = (ushort)(card.Id >= 2000 ? card.Id - 2000 : card.Id);
        return recipes.TryGetValue(baseId, out var recipe) ? Array.AsReadOnly(recipe) : null;
    }

    internal static bool Matches(ushort materialId, ushort requiredId) =>
        materialId == requiredId || materialId == requiredId + 2000;

    public static bool Fits(IReadOnlyList<ushort> recipe, IReadOnlyList<FusionMaterial> selected) =>
        recipe.Distinct().All(id => selected.Count(material => Matches(material.Card.Id, id)) <=
            recipe.Count(required => required == id)) &&
        selected.All(material => recipe.Any(id => Matches(material.Card.Id, id)));
}
