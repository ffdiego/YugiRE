namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingQueuedDeckRecruitment;

    private bool CanAdmitQueuedDeckRecruitment() =>
        !GameOver && deferredBattle is null &&
        PendingCardSelection is { Source: CardSelectionSource.Deck,
            Destination: CardSelectionDestination.Field, MinSelections: 0, MaxSelections: 1,
            Filter: CardSelectionFilter.MonsterAttributeAttackAtMost1500,
            SummonPosition: CardSelectionPosition.FaceUpAttack } pending &&
        CardSelectionRules.BattleRecruitAttribute(pending.EffectResourceId) is byte attribute &&
        pending.RequiredAttributeCode == attribute &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    /// <summary>Authorized owner-only candidate projection for P164.</summary>
    public IReadOnlyList<QueuedDeckSearchCandidate> QueuedDeckRecruitmentCandidates(PendingCardSelection request)
    {
        if (primitiveActionQueue?.IsBusy == true || !CanAdmitQueuedDeckRecruitment() ||
            !ReferenceEquals(PendingCardSelection, request) ||
            !Enumerable.Range(0, 5).Any(z => Monsters[request.Owner, z] is null))
            return [];
        var result = new List<QueuedDeckSearchCandidate>();
        var deck = decks[request.Owner].ToArray();
        for (int i = 0; i < deck.Length; i++)
        {
            var card = deck[i];
            if (request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
                CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode) &&
                TryCaptureCardTarget(request.Owner, DuelZone.Deck, i, out var target))
                result.Add(new(target, i, card));
        }
        return result.AsReadOnly();
    }

    /// <summary>Accept one occurrence and a free monster slot, or decline
    /// with both arguments null. The completed legacy battle is the producer.</summary>
    public bool TryQueueDeckRecruitment(PendingCardSelection request, CardTarget? target, int? slot,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible recruit and free slot, or decline.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !TryPlanQueuedDeckRecruitment(request, target, slot)) return false;
        admittingQueuedDeckRecruitment = true;
        try { queue.Enqueue(new ResolveDeckRecruitmentCommand(request.Owner, request, target, slot), origin); }
        finally { admittingQueuedDeckRecruitment = false; }
        message = "Recruitment answer admitted.";
        return true;
    }

    private bool TryPlanQueuedDeckRecruitment(PendingCardSelection request, CardTarget? target, int? slot)
    {
        if (!CanAdmitQueuedDeckRecruitment() || !ReferenceEquals(PendingCardSelection, request))
            return false;
        if (target is null) return slot is null;
        return slot is int z && z is >= 0 and < 5 && Monsters[request.Owner, z] is null &&
            target.Value.Side == request.Owner && target.Value.Zone == DuelZone.Deck &&
            TryResolveCardTarget(target.Value, out _, out var card) &&
            request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
            CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode);
    }
}
