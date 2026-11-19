namespace Joey.Core;

/// <summary>
/// Exact resource IDs from the 16 ritual spell/monster bindings independently
/// extracted from the packed table at EXE VA 0x005ED060. These identities
/// are a closed corpus, not a claim to implement Ritual Summoning.
/// Source: docs/evidence/ritual-rules-catalog.json.
/// </summary>
public static class RitualSearchRules
{
    public const ushort SonicBird = 1122;
    public const ushort SenjuOfTheThousandHands = 1109;

    private static readonly HashSet<ushort> RitualSpellIds =
    [
        669, 670, 672, 673, 675, 676, 678, 691,
        693, 695, 696, 698, 699, 720, 958, 1416
    ];

    private static readonly HashSet<ushort> RitualMonsterIds =
    [
        355, 359, 363, 364, 700, 701, 703, 708,
        709, 715, 716, 717, 718, 721, 730, 1411
    ];

    // IDs alone are not sufficient: the local DAT class must match the type.
    // NormalSummonRules.Category does not identify Ritual monsters by class.
    public static bool IsRitualSpell(Card card) =>
        card.ClassCode == 22 && RitualSpellIds.Contains(card.Id);

    public static bool IsRitualMonster(Card card) =>
        card.IsMonster && RitualMonsterIds.Contains(card.Id);

    public static CardSelectionFilter? OnNormalOrFlipSummon(ushort resourceId) =>
        resourceId switch
        {
            SonicBird => CardSelectionFilter.RitualSpell,
            SenjuOfTheThousandHands => CardSelectionFilter.RitualMonster,
            _ => null
        };
}
