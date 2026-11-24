namespace Joey.Core;

/// <summary>
/// A rule request is independent of how it is shown (raylib, AI, or replay).
/// Optional costs and effects share the same decision lifecycle.
/// </summary>
public enum EffectDecisionKind { PayLifePointsForRevival, DrawOneOnFlip }
public enum EffectDecisionAnswer { Accept, Decline }
public enum EffectPromptMode { Ask, Automatic, Off }

public sealed record PendingEffectDecision(
    long Id, int Owner, Card Source, EffectDecisionKind Kind, int LifePointCost, int CreatedTurn);

/// <summary>A promise to try to revive this exact Card occurrence, not any card with its ID.</summary>
public sealed record PendingDelayedRevival(long DecisionId, int Owner, Card Source, int DueTurn);

public sealed record EffectDecisionResolution(
    long Id, int Owner, EffectDecisionKind Kind, EffectDecisionAnswer Answer,
    int Turn, int LifeBefore, int LifeAfter);

/// <summary>
/// Presentation policy: Ask returns null. Off declines OPTIONAL effects; it never
/// silently pays a cost. Automatic requires an explicit decision strategy; absent
/// one, it conservatively declines instead of pretending to be native AI.
/// </summary>
public static class EffectDecisionPolicies
{
    public static EffectDecisionAnswer? Choose(
        EffectPromptMode mode, PendingEffectDecision request,
        Func<PendingEffectDecision, EffectDecisionAnswer>? automatic = null) => mode switch
    {
        EffectPromptMode.Ask => null,
        EffectPromptMode.Off => EffectDecisionAnswer.Decline,
        EffectPromptMode.Automatic => automatic?.Invoke(request) ?? EffectDecisionAnswer.Decline,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

public static class RevivalJamRules
{
    public const ushort ResourceId = 1405;
    public const int LifePointCost = 1000;

    // Paying exactly the remaining 1000 LP would reduce LP to 0; not an
    // eligible payment in this bounded experimental sandbox.
    public static bool MayPay(int lifePoints) => lifePoints > LifePointCost;
}
