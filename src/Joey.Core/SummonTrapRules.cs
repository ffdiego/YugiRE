namespace Joey.Core;

/// <summary>
/// Bounded one-response-per-summon trap window for the hot-seat sandbox.
/// Full priority, chains, Trap immunity and Damage Step windows are not modeled.
/// </summary>
public enum DemoSummonKind { Normal, Flip }

public sealed record DemoSummonEvent(int Player, int Zone, Card Monster, DemoSummonKind Kind);

public sealed record DemoSummonTrapOption(int Owner, int Zone, Card Trap);

public sealed record DemoSummonResponse(
    DemoSummonEvent Summon, IReadOnlyList<DemoSummonTrapOption> EligibleTraps);

public sealed record DemoSummonTrapResult(
    ushort TrapResourceId, int Responder, int SummoningPlayer,
    DemoSummonKind SummonKind, int DestroyedMonsters);

public static class SummonTrapRules
{
    public const ushort TrapHole = 1002;
    public const ushort TorrentialTribute = 1425;

    public static bool Supports(ushort id) => id is TrapHole or TorrentialTribute;

    /// <summary>
    /// Trap Hole is exclusive to an opponent's Normal/Flip Summon.
    /// Torrential can respond to either player's Normal/Flip Summon in this
    /// bounded slice; Special Summon response is deliberately not supported yet.
    /// Current ATK (including available modifiers) is used at activation.
    /// </summary>
    public static bool IsEligible(
        Card trap, int responder, DemoSummonEvent summon, int currentAttack)
    {
        if (trap.ClassCode != 21 || responder is < 0 or > 1) return false;
        return trap.Id switch
        {
            TrapHole => responder != summon.Player &&
                summon.Kind is DemoSummonKind.Normal or DemoSummonKind.Flip &&
                currentAttack >= 1000,
            TorrentialTribute => summon.Kind is DemoSummonKind.Normal or DemoSummonKind.Flip,
            _ => false
        };
    }
}
