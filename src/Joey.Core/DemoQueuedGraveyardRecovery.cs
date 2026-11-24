namespace Joey.Core;

public sealed record QueuedGraveyardCandidate(CardTarget Target, int SourceIndex, Card Card);

public sealed partial class DemoDuel
{
    private bool admittingQueuedGraveyardRecovery;

    private bool CanAdmitQueuedGraveyardRecovery() =>
        !GameOver && Phase == DemoPhase.Main && deferredBattle is null &&
        PendingCardSelection is { Source: CardSelectionSource.Graveyard,
            Destination: CardSelectionDestination.Hand, MinSelections: 1, MaxSelections: 1 } pending &&
        CardSelectionRules.FlipGraveyardFilter(pending.EffectResourceId) == pending.Filter &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    /// <summary>Only offers the original request's eligible physical copies,
    /// resolving their current indices rather than trusting the old slot.</summary>
    public IReadOnlyList<QueuedGraveyardCandidate> QueuedGraveyardRecoveryCandidates(PendingCardSelection request)
    {
        if (primitiveActionQueue?.IsBusy == true || FlipEffectsNegated ||
            !CanAdmitQueuedGraveyardRecovery() || !ReferenceEquals(PendingCardSelection, request))
            return [];
        var result = new List<QueuedGraveyardCandidate>();
        int owner = request.Owner;
        for (int i = 0; i < graves[owner].Count; i++)
        {
            Card card = graves[owner][i];
            if (!request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) ||
                !CardSelectionRules.IsEligible(card, request.Filter) ||
                !TryCaptureCardTarget(owner, DuelZone.Graveyard, i, out var target)) continue;
            result.Add(new(target, i, card));
        }
        return result.AsReadOnly();
    }

    /// <summary>Answers the exact pending request; null is permitted only
    /// for a FLIP suppressed by Royal Command. No mutations at admission.</summary>
    public bool TryQueueGraveyardRecovery(PendingCardSelection request, CardTarget? target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible occurrence for the current manual FLIP recovery.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !TryPlanQueuedGraveyardRecovery(request, target, out _)) return false;
        admittingQueuedGraveyardRecovery = true;
        try { queue.Enqueue(new ResolveGraveyardRecoveryCommand(request.Owner, request, target), origin); }
        finally { admittingQueuedGraveyardRecovery = false; }
        message = "Graveyard recovery admitted.";
        return true;
    }

    private bool TryPlanQueuedGraveyardRecovery(PendingCardSelection request, CardTarget? target,
        out MoveToHandCommand? movement)
    {
        movement = null;
        if (!CanAdmitQueuedGraveyardRecovery() || !ReferenceEquals(PendingCardSelection, request))
            return false;
        // If suppression appears after admission, the selected card is not
        // moved; the pending FLIP resolves without effect.
        if (FlipEffectsNegated) return true;
        if (target is not { } selected || selected.Side != request.Owner ||
            selected.Zone != DuelZone.Graveyard ||
            !TryResolveCardTarget(selected, out _, out var card) ||
            !request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) ||
            !CardSelectionRules.IsEligible(card, request.Filter))
            return false;
        movement = new MoveToHandCommand(request.Owner, selected);
        return true;
    }
}
