namespace Joey.Core;

/// <summary>
/// Small explicit summon-response window for hot-seat Joey.Core tests.
/// This is deliberately not a chain implementation or native response-order claim.
/// </summary>
public enum SummonTriggerKind { Normal, Flip, Special }

public sealed record DemoSummonEvent(
    int Summoner, int Zone, Card Monster, SummonTriggerKind Kind);

public sealed record SummonTrapOption(int Owner, int SpellZone, Card Card);

public sealed record PendingSummonResponse(
    DemoSummonEvent Summon, IReadOnlyList<SummonTrapOption> Options);

public static class SummonTrapRules
{
    public const ushort TrapHole = 1002;
    public const ushort TorrentialTribute = 1425;

    public static bool IsSupported(ushort id) => id is TrapHole or TorrentialTribute;

    /// <summary>
    /// Trap Hole targets the one Normal/Flip Summoned opposing monster whose
    /// current visible ATK is at least 1000. Torrential is non-targeting
    /// and can respond to either player's summon. In this first sandbox
    /// slice only Normal and Flip summons publish windows.
    /// </summary>
    public static bool CanActivate(
        Card trap, int trapOwner, DemoSummonEvent summon, int visibleSummonedAttack)
    {
        if (trap.ClassCode != 21 || trapOwner is < 0 or > 1 ||
            summon.Summoner is < 0 or > 1) return false;
        return trap.Id switch
        {
            TrapHole => trapOwner != summon.Summoner &&
                summon.Kind is SummonTriggerKind.Normal or SummonTriggerKind.Flip &&
                visibleSummonedAttack >= 1000,
            TorrentialTribute => true,
            _ => false
        };
    }
}
