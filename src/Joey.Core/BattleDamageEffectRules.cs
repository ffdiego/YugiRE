namespace Joey.Core;

// Intent only. This is not a second scheduler and does not move any card.
// The command adapter consumes the intent after the damage command resolves.
public enum BattleDamageEffectKind { Draw, RandomDiscard, SearchParasite, BanishOpponentGraveyard }

public readonly record struct BattleDamageEffectIntent(
    ushort SourceResourceId, int Controller, int AffectedSide,
    BattleDamageEffectKind Kind, int MaximumCount);

public static class BattleDamageEffectRules
{
    public const ushort WhiteMagicalHat = 113;
    public const ushort MaskedSorcerer = 219;
    public const ushort BistroButcher = 522;
    public const ushort DrillBug = 1328;
    public const ushort Kycoo = 1511;

    public static bool SupportsDraw(ushort resourceId) => resourceId is MaskedSorcerer or BistroButcher;

    // Called only for actual monster-inflicted battle damage, not effect
    // damage, an attempted attack, or a generic LP change.
    // The caller supplies the controller at the damage event, not the
    // owner or the current occupant of the original monster slot.
    // Game-over precedence follows the existing sandbox policy.
    public static BattleDamageEffectIntent? AfterBattleDamage(
        ushort resourceId, int controller, int damagedSide, int amount, bool gameOver)
    {
        if (controller is not (0 or 1) || damagedSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(controller), "Invalid side.");
        if (amount <= 0 || controller == damagedSide || gameOver)
            return null;

        return resourceId switch
        {
            WhiteMagicalHat => new(resourceId, controller, damagedSide, BattleDamageEffectKind.RandomDiscard, 1),
            MaskedSorcerer => new(resourceId, controller, controller, BattleDamageEffectKind.Draw, 1),
            BistroButcher => new(resourceId, controller, damagedSide, BattleDamageEffectKind.Draw, 2),
            DrillBug => new(resourceId, controller, controller, BattleDamageEffectKind.SearchParasite, 1),
            Kycoo => new(resourceId, controller, damagedSide, BattleDamageEffectKind.BanishOpponentGraveyard, 2),
            _ => null
        };
    }

    // At resolution, choose one index from the CURRENT hand snapshot using
    // the duel RNG. The adapter must immediately capture the occurrence at
    // this index and submit a DiscardCommand, never queue the index itself.
    // No RNG consumption for an empty hand; no reselection on stale target.
    public static int? ChooseRandomDiscardIndex(int handCount, Func<int, int> nextIndex)
    {
        if (handCount < 0) throw new ArgumentOutOfRangeException(nameof(handCount));
        ArgumentNullException.ThrowIfNull(nextIndex);
        if (handCount == 0) return null;
        int index = nextIndex(handCount);
        if (index < 0 || index >= handCount)
            throw new InvalidOperationException("RNG returned an out-of-range hand index.");
        return index;
    }
}
