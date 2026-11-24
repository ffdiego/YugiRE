namespace Joey.Core;

public sealed record QueuedDeckSearchCandidate(CardTarget Target, int SourceIndex, Card Card);

public sealed partial class DemoDuel
{
    private bool admittingQueuedDeckSearch;

    private bool CanAdmitQueuedDeckSearch() =>
        !GameOver && deferredBattle is null &&
        PendingCardSelection is { Source: CardSelectionSource.Deck,
            Destination: CardSelectionDestination.Hand, MaxSelections: 1 } pending &&
        ((pending.MinSelections == 1 && CardSelectionRules.OnFieldToGraveyardFilter(pending.EffectResourceId) == pending.Filter) ||
         (pending.MinSelections == 0 && RitualSearchRules.OnNormalOrFlipSummon(pending.EffectResourceId) == pending.Filter) ||
         (pending.MinSelections == 1 && DeckSearchSpellRules.Filter(pending.EffectResourceId) == pending.Filter)) &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    /// <summary>Authorized search view of the original request. Never reveal
    /// the raw candidate list to the other player.</summary>
    public IReadOnlyList<QueuedDeckSearchCandidate> QueuedDeckSearchCandidates(PendingCardSelection request)
    {
        if (primitiveActionQueue?.IsBusy == true || !CanAdmitQueuedDeckSearch() ||
            !ReferenceEquals(PendingCardSelection, request)) return [];
        var result = new List<QueuedDeckSearchCandidate>();
        var deck = decks[request.Owner].ToArray();
        for (int i = 0; i < deck.Length; i++)
        {
            var card = deck[i];
            if (!request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) ||
                !CardSelectionRules.IsEligible(card, request.Filter) ||
                !TryCaptureCardTarget(request.Owner, DuelZone.Deck, i, out var target))
                continue;
            result.Add(new(target, i, card));
        }
        return result.AsReadOnly();
    }

    public bool TryQueueDeckSearch(PendingCardSelection request, CardTarget? target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible occurrence, or decline an optional search.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !TryPlanQueuedDeckSearch(request, target)) return false;
        admittingQueuedDeckSearch = true;
        try { queue.Enqueue(new ResolveDeckSearchCommand(request.Owner, request, target), origin); }
        finally { admittingQueuedDeckSearch = false; }
        message = "Deck search admitted.";
        return true;
    }

    private bool TryPlanQueuedDeckSearch(PendingCardSelection request, CardTarget? target)
    {
        if (!CanAdmitQueuedDeckSearch() || !ReferenceEquals(PendingCardSelection, request))
            return false;
        if (target is not { } chosen) return request.MinSelections == 0;
        return chosen.Side == request.Owner && chosen.Zone == DuelZone.Deck &&
            TryResolveCardTarget(chosen, out _, out var card) &&
            request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
            CardSelectionRules.IsEligible(card, request.Filter);
    }
}
