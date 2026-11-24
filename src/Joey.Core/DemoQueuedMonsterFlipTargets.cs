namespace Joey.Core;

/// <summary>Read-only FLIP target view. Opponent hidden identity is for
/// authorized simulation; the presentation must not expose it.</summary>
public sealed record QueuedMonsterFlipCandidate(CardTarget Target, int Zone, Card Card, bool FaceDown);

public sealed partial class DemoDuel
{
    // A narrowly scoped admission exception to the existing pending-choice
    // gate. Only this method can set it, only while enqueueing the answer.
    private bool admittingQueuedMonsterFlipAnswer;

    private static bool IsQueuedMonsterFlip(DemoFlipTarget pending) =>
        pending.Trigger == DemoMonsterEffectTrigger.Flip &&
        pending.Effect is DemoFlipEffect.DestroyMonster or DemoFlipEffect.ReturnOne or DemoFlipEffect.ReturnUpToTwo;

    private bool CanAdmitQueuedMonsterFlipAnswer() =>
        !GameOver && deferredBattle is null && Phase == DemoPhase.Main &&
        PendingFlipTarget is { } pending && IsQueuedMonsterFlip(pending) &&
        PendingFlipSpellTrapTargets is null && PendingCardSelection is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    public IReadOnlyList<QueuedMonsterFlipCandidate> QueuedMonsterFlipCandidates()
    {
        if (primitiveActionQueue?.IsBusy == true || GameOver || FlipEffectsNegated ||
            PendingFlipTarget is not { } pending || !IsQueuedMonsterFlip(pending)) return [];
        bool bounce = pending.Effect != DemoFlipEffect.DestroyMonster;
        var result = new List<QueuedMonsterFlipCandidate>();
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
        {
            if (Monsters[p,z] is not Card card ||
                bounce && IsMarkedForBattleDestruction(p,z) ||
                !TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) ||
                !TryPlanRemovalLinks(p,z,out _,toGraveyard: !bounce)) continue;
            result.Add(new(target,z,card,MonsterFaceDown[p,z]));
        }
        return result.AsReadOnly();
    }

    /// <summary>Resolve a manual FLIP selection through the queue. Battle
    /// FLIPs retain the old response path until queued Damage Step exists.</summary>
    public bool TryQueueMonsterFlipAnswer(CardTarget source, IReadOnlyList<CardTarget> targets,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported pending manual FLIP and valid distinct occurrences.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !CanAdmitQueuedMonsterFlipAnswer() || targets is null ||
            !TryPlanQueuedMonsterFlipAnswer(source, targets, out _, out _))
            return false;
        // The caller's list is copied into immutable command values.
        var answer = new ResolveMonsterFlipTargetsCommand(source.Side, source,
            targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null);
        admittingQueuedMonsterFlipAnswer = true;
        try { queue.Enqueue(answer, origin); }
        finally { admittingQueuedMonsterFlipAnswer = false; }
        message = "FLIP answer admitted.";
        return true;
    }

    private bool TryPlanQueuedMonsterFlipAnswer(CardTarget source, IReadOnlyList<CardTarget> targets,
        out DuelCommand[] batch, out Dictionary<CardTarget,DuelCommand[]> plans)
    {
        batch = [];
        plans = new();
        if (!CanAdmitQueuedMonsterFlipAnswer() || PendingFlipTarget is not { } pending ||
            source.Zone != DuelZone.Monster || source.Side != pending.Owner ||
            !TryResolveCardTarget(source,out int sourceSlot,out var sourceCard) ||
            sourceSlot != pending.SourceZone || !ReferenceEquals(sourceCard,pending.SourceCard))
            return false;
        // Royal Command suppresses the pending effect. Accept only an empty
        // answer in that case; no target movement or legacy battle resolver.
        if (FlipEffectsNegated) return targets.Count == 0;
        bool bounce = pending.Effect != DemoFlipEffect.DestroyMonster;
        if (targets.Count > 2 || (pending.Effect != DemoFlipEffect.ReturnUpToTwo && targets.Count != 1) ||
            targets.Distinct().Count() != targets.Count) return false;
        var result = new List<DuelCommand>();
        foreach (var target in targets)
        {
            if (target.Zone != DuelZone.Monster || !TryResolveCardTarget(target,out int slot,out _) ||
                bounce && IsMarkedForBattleDestruction(target.Side,slot) ||
                !TryPlanRemovalLinks(target.Side,slot,out var links,toGraveyard: !bounce))
                return false;
            plans.Add(target,links);
            result.Add(bounce ? new ReturnToHandCommand(target.Side,target) : new DestroyCommand(target.Side,target));
            result.AddRange(links);
        }
        batch = result.ToArray();
        return true;
    }
}
