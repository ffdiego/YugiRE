namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingQueuedGraveyardRevival;

    private bool CanAdmitQueuedGraveyardRevival() =>
        !GameOver && deferredBattle is null &&
        PendingCardSelection is { Source: CardSelectionSource.Graveyard,
            Destination: CardSelectionDestination.Field, MinSelections: 1, MaxSelections: 1,
            Filter: CardSelectionFilter.AnyMonster, EffectResourceId: CardSelectionRules.SpearCretin,
            MaySelectFaceDownDefense: true } &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    /// <summary>Original request excludes the exact triggering Spear Cretin
    /// occurrence, but does not exclude other copies of the same resource.</summary>
    public IReadOnlyList<QueuedGraveyardCandidate> QueuedGraveyardRevivalCandidates(PendingCardSelection request)
    {
        if (primitiveActionQueue?.IsBusy == true || !CanAdmitQueuedGraveyardRevival() ||
            !ReferenceEquals(PendingCardSelection, request) ||
            !Enumerable.Range(0, 5).Any(z => Monsters[request.Owner,z] is null))
            return [];
        var result = new List<QueuedGraveyardCandidate>();
        int side = request.Owner;
        for (int i = 0; i < graves[side].Count; i++)
        {
            Card card = graves[side][i];
            if (request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
                CardSelectionRules.IsEligible(card, request.Filter) &&
                TryCaptureCardTarget(side, DuelZone.Graveyard, i, out var target))
                result.Add(new(target, i, card));
        }
        return result.AsReadOnly();
    }

    /// <summary>Spear Cretin's mandatory answer, from the owner's own GY.
    /// The source's FLIP history was already checked by RemoveMonster.</summary>
    public bool TryQueueGraveyardRevival(PendingCardSelection request, CardTarget target, int slot,
        bool faceDownDefense, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible monster occurrence and free destination for the pending revival.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !TryPlanGraveyardRevival(request, target, slot, faceDownDefense, out _)) return false;
        admittingQueuedGraveyardRevival = true;
        try { queue.Enqueue(new ResolveGraveyardRevivalCommand(request.Owner, request, target, slot, faceDownDefense), origin); }
        finally { admittingQueuedGraveyardRevival = false; }
        message = "Revival answer admitted.";
        return true;
    }

    private bool TryPlanGraveyardRevival(PendingCardSelection request, CardTarget target, int slot,
        bool faceDownDefense, out SpecialSummonCommand? movement)
    {
        movement = null;
        if (!CanAdmitQueuedGraveyardRevival() || !ReferenceEquals(PendingCardSelection, request) ||
            target.Side != request.Owner || target.Zone != DuelZone.Graveyard ||
            slot is < 0 or >= 5 || Monsters[request.Owner,slot] is not null ||
            !TryResolveCardTarget(target, out _, out var card) ||
            !request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) ||
            !CardSelectionRules.IsEligible(card, request.Filter))
            return false;
        movement = new SpecialSummonCommand(request.Owner, target, slot, faceDownDefense);
        return true;
    }
}
