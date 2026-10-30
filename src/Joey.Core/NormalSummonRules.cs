namespace Joey.Core;

// Static gates: 0x00402740 (derived category) and 0x00567A00 (special-only monsters).
// This does not implement any of the corresponding special summon procedures.
public static class NormalSummonRules
{
    private static readonly HashSet<ushort> specialOnly =
    [
        0x37, 0x38, 0x3e, 0x42, 0x170, 0x175, 0x187,
        0x2d6, 0x2d7, 0x2d8, 0x2e5, 0x2fe, 0x34d, 0x4b2, 0x4e9,
        0x5ea, 0x5eb, 0x5ec, 0x5ed, 0x5ee, 0x5ef
    ];
    public static int Category(Card card) => card.Id switch
    {
        0x776 => 3,
        0x777 or 0x778 => 1,
        _ => card.ClassCode switch
        {
            21 => 8,
            22 => 7,
            23 => 9,
            _ => (int)(card.Properties >> 18 & 3)
        }
    };
    public static bool RequiresSpecialSummon(Card card)
    {
        int category = Category(card);
        return card.IsMonster && category != 0 && (category is 2 or 3 || specialOnly.Contains(card.Id));
    }
}
