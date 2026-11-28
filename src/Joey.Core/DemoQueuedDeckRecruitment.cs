namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingQueuedDeckRecruitment;

    private bool CanAdmitQueuedDeckRecruitment() =>
        !GameOver &&
        PendingCardSelection is { Source: CardSelectionSource.Deck,
            Destination: CardSelectionDestination.Field, MinSelections: 0 } pending &&
        (IsElementalRecruitment(pending) || IsSameNameRecruitment(pending)) &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    private static bool IsElementalRecruitment(PendingCardSelection pending) =>
        pending is { MaxSelections: 1, Filter: CardSelectionFilter.MonsterAttributeAttackAtMost1500,
            SummonPosition: CardSelectionPosition.FaceUpAttack } &&
        CardSelectionRules.BattleRecruitAttribute(pending.EffectResourceId) is byte attribute &&
        pending.RequiredAttributeCode == attribute;

    private static bool IsSameNameRecruitment(PendingCardSelection pending) =>
        pending.Filter == CardSelectionFilter.SameNameMonster &&
        pending.RequiredResourceId == pending.EffectResourceId &&
        (pending.EffectResourceId == FlipEffectRules.BubonicVermin
            ? pending.MaxSelections == 1 && pending.SummonPosition == CardSelectionPosition.FaceDownDefense
            : CardSelectionRules.BattleSameNameRecruiter(pending.EffectResourceId) &&
              pending.MaxSelections is 1 or 2 &&
              pending.SummonPosition == (pending.EffectResourceId == CardSelectionRules.NimbleMomonga
                  ? CardSelectionPosition.FaceDownDefense : CardSelectionPosition.FaceUpAttack));

    private bool IsSuppressedRecruitment(PendingCardSelection request) =>
        request.EffectResourceId == FlipEffectRules.BubonicVermin && FlipEffectsNegated;

    /// <summary>Authorized owner-only candidate projection for supported Deck→Field requests.</summary>
    public IReadOnlyList<QueuedDeckSearchCandidate> QueuedDeckRecruitmentCandidates(PendingCardSelection request)
    {
        if (primitiveActionQueue?.IsBusy == true || !CanAdmitQueuedDeckRecruitment() ||
            !ReferenceEquals(PendingCardSelection, request) || IsSuppressedRecruitment(request) ||
            !Enumerable.Range(0, 5).Any(z => Monsters[request.Owner, z] is null))
            return [];
        var result = new List<QueuedDeckSearchCandidate>();
        var deck = decks[request.Owner].ToArray();
        for (int i = 0; i < deck.Length; i++)
        {
            var card = deck[i];
            if (request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
                CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode, request.RequiredResourceId) &&
                TryCaptureCardTarget(request.Owner, DuelZone.Deck, i, out var target))
                result.Add(new(target, i, card));
        }
        return result.AsReadOnly();
    }

    /// <summary>Accept one occurrence and a free monster slot, or decline
    /// with both arguments null. Requests may originate from completed queued battle removal.</summary>
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
        if (!CanAdmitQueuedDeckRecruitment() || !ReferenceEquals(PendingCardSelection, request) || !IsElementalRecruitment(request))
            return false;
        if (target is null) return slot is null;
        return slot is int z && z is >= 0 and < 5 && Monsters[request.Owner, z] is null &&
            target.Value.Side == request.Owner && target.Value.Zone == DuelZone.Deck &&
            TryResolveCardTarget(target.Value, out _, out var card) &&
            request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) &&
            CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode, request.RequiredResourceId);
    }

    /// <summary>Reserve zero, one or two same-name copies. Multiple selections
    /// use the legacy leftmost free slots; a single-choice request can specify
    /// its slot. Do not mix this API with partially selected legacy batches.</summary>
    public bool TryQueueSameNameRecruitment(PendingCardSelection request, IReadOnlyList<CardTarget> targets,
        out string message, int? singleSlot = null, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose distinct eligible copies and free slots, or decline.";
        if (primitiveActionQueue is not { IsBusy: false } queue || targets is null ||
            !TryPlanSameNameRecruitment(request, targets, singleSlot, out _)) return false;
        var answer = new ResolveSameNameRecruitmentCommand(request.Owner, request,
            targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null, singleSlot);
        admittingQueuedDeckRecruitment = true;
        try { queue.Enqueue(answer, origin); }
        finally { admittingQueuedDeckRecruitment = false; }
        message = "Same-name recruitment answer admitted.";
        return true;
    }

    private bool TryPlanSameNameRecruitment(PendingCardSelection request, IReadOnlyList<CardTarget> targets,
        int? singleSlot, out DuelCommand[] movements)
    {
        movements = [];
        if (!CanAdmitQueuedDeckRecruitment() || !ReferenceEquals(PendingCardSelection, request) ||
            !IsSameNameRecruitment(request) || selectedBatchIndices.Count != 0 ||
            targets.Count > request.MaxSelections || targets.Distinct().Count() != targets.Count ||
            singleSlot is not null && (request.MaxSelections != 1 || targets.Count != 1 ||
                singleSlot is < 0 or >= 5)) return false;
        if (IsSuppressedRecruitment(request)) return true;
        int[] slots = singleSlot is int slot ? [slot] :
            Enumerable.Range(0, 5).Where(z => Monsters[request.Owner, z] is null).Take(targets.Count).ToArray();
        if (slots.Length != targets.Count || slots.Any(z => Monsters[request.Owner, z] is not null))
            return false;
        var result = new List<DuelCommand>();
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target.Side != request.Owner || target.Zone != DuelZone.Deck ||
                !TryResolveCardTarget(target, out _, out var card) ||
                !request.EligibleCards.Any(c => ReferenceEquals(c.Card, card)) ||
                !CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode, request.RequiredResourceId))
                return false;
            result.Add(new SpecialSummonCommand(request.Owner, target, slots[i],
                request.SummonPosition == CardSelectionPosition.FaceDownDefense));
        }
        movements = result.ToArray();
        return true;
    }
}
