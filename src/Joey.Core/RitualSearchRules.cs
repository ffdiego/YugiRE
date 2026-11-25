namespace Joey.Core;

/// <summary>
/// Exact resource IDs from the 16 ritual spell/monster bindings independently
/// extracted from the packed table at EXE VA 0x005ED060. Shared by
/// Ritual search filters and the queued Ritual Summon procedure.
/// Source: docs/evidence/ritual-rules-catalog.json.
/// </summary>
public static class RitualSearchRules
{
    public const ushort SonicBird = 1122;
    public const ushort SenjuOfTheThousandHands = 1109;

    internal readonly record struct Binding(ushort MonsterId, int RequiredLevel);

    private static readonly Dictionary<ushort, Binding> Bindings = new()
    {
        [672] = new(355, 7),
        [670] = new(359, 8),
        [669] = new(363, 8),
        [673] = new(364, 6),
        [675] = new(700, 6),
        [676] = new(701, 6),
        [678] = new(703, 7),
        [693] = new(708, 7),
        [691] = new(709, 8),
        [696] = new(715, 7),
        [695] = new(716, 8),
        [699] = new(717, 7),
        [698] = new(718, 6),
        [720] = new(721, 8),
        [958] = new(730, 1),
        [1416] = new(1411, 8)
    };

    private static readonly HashSet<ushort> RitualMonsterIds =
        Bindings.Values.Select(binding => binding.MonsterId).ToHashSet();

    internal static bool TryGetBinding(Card spell, out Binding binding)
    {
        binding = default;
        return spell.ClassCode == 22 && Bindings.TryGetValue(spell.Id, out binding);
    }

    public static bool IsRitualSpell(Card card) => TryGetBinding(card, out _);

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
