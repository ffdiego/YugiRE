namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Includes P164 requests from a completed queued or legacy battle.
    // A still-deferred legacy Damage Step remains outside this producer.
    public bool UsesQueuedFieldSelection(PendingCardSelection request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingCardSelection, request) &&
        deferredBattle is null && request.Destination == CardSelectionDestination.Field &&
        (request.Source == CardSelectionSource.Deck && request.MinSelections == 0 &&
            (IsElementalRecruitment(request) || IsSameNameRecruitment(request)) ||
         IsSpearCretinRevival(request) || IsRevivalSpellRequest(request));

    /// <summary>Capture a displayed occurrence, without staging game state.</summary>
    public bool TryCaptureQueuedFieldSelection(PendingCardSelection request, int sourceIndex,
        out CardTarget target)
    {
        target = default;
        if (!UsesQueuedFieldSelection(request) || primitiveActionQueue is not { IsBusy: false })
            return false;
        var option = request.EligibleCards.FirstOrDefault(c => c.SourceIndex == sourceIndex);
        DuelZone zone = request.Source == CardSelectionSource.Deck ? DuelZone.Deck : DuelZone.Graveyard;
        if (option is null || !TryCaptureCardTarget(request.Owner, zone, sourceIndex, out var captured) ||
            !TryResolveCardTarget(captured, out int index, out var card) ||
            index != sourceIndex || !ReferenceEquals(card, option.Card) ||
            !CardSelectionRules.IsEligible(card, request.Filter, request.RequiredAttributeCode, request.RequiredResourceId))
            return false;
        target = captured;
        return true;
    }

    /// <summary>Submit a complete choice using occurrence IDs; staging is
    /// private to the producer. No legacy batch mutation or fallback.</summary>
    public bool TrySubmitQueuedFieldSelection(PendingCardSelection request, IReadOnlyList<CardTarget> targets,
        bool faceDownDefense, out string message, ActionOrigin origin = ActionOrigin.Player,
        int? destinationZone = null)
    {
        message = "Choose eligible occurrences and a free monster zone.";
        if (!UsesQueuedFieldSelection(request) || primitiveActionQueue is not { IsBusy: false } ||
            targets is null || targets.Count < request.MinSelections || targets.Count > request.MaxSelections ||
            targets.Distinct().Count() != targets.Count ||
            (faceDownDefense && !request.MaySelectFaceDownDefense) ||
            (destinationZone is not null && (targets.Count != 1 || request.MaxSelections != 1)))
            return false;
        int slot = destinationZone ?? Enumerable.Range(0, 5)
            .FirstOrDefault(z => Monsters[request.Owner, z] is null, -1);

        if (request.Source == CardSelectionSource.Graveyard)
            return targets.Count == 1 && TryQueueGraveyardRevival(request, targets[0], slot,
                faceDownDefense, out message, origin);
        if (IsSameNameRecruitment(request))
            return TryQueueSameNameRecruitment(request, targets, out message,
                request.MaxSelections == 1 && targets.Count == 1 ? slot : null, origin);
        return TryQueueDeckRecruitment(request, targets.Count == 1 ? targets[0] : null,
            targets.Count == 1 ? slot : null, out message, origin);
    }
}
