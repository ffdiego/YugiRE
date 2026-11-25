namespace Joey.Core;

public sealed partial class DemoDuel
{
    // During C12 the queue may coexist with a legacy deferred battle.
    // Do not route that battle's choices to manual-FLIP/search executors.
    // Ownership is independent of queue busy state: rejection must never
    // make a producer fall back to immediate mutation.
    public bool UsesQueuedHandSelection(PendingCardSelection request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingCardSelection, request) &&
        deferredBattle is null &&
        request is { Destination: CardSelectionDestination.Hand, MaxSelections: 1 } &&
        (request.Source == CardSelectionSource.Deck &&
            ((request.MinSelections == 1 &&
                (CardSelectionRules.OnFieldToGraveyardFilter(request.EffectResourceId) == request.Filter ||
                 DeckSearchSpellRules.Filter(request.EffectResourceId) == request.Filter)) ||
             (request.MinSelections == 0 &&
                RitualSearchRules.OnNormalOrFlipSummon(request.EffectResourceId) == request.Filter)) ||
         request.Source == CardSelectionSource.Graveyard && Phase == DemoPhase.Main &&
            request.MinSelections == 1 &&
            CardSelectionRules.FlipGraveyardFilter(request.EffectResourceId) == request.Filter);

    /// <summary>Shared UI/CPU producer for supported hand-selection prompts.
    /// The old list index must still identify the same eligible occurrence.
    /// No mutation or legacy fallback on rejected or stale answers.</summary>
    public bool TrySubmitQueuedHandSelection(PendingCardSelection request, int? sourceIndex,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a current eligible queued hand selection.";
        if (!UsesQueuedHandSelection(request) || primitiveActionQueue is not { IsBusy: false })
            return false;
        if (request.Source == CardSelectionSource.Graveyard && FlipEffectsNegated)
            return TryQueueGraveyardRecovery(request, null, out message, origin);
        if (sourceIndex is null)
            return request.Source == CardSelectionSource.Deck &&
                TryQueueDeckSearch(request, null, out message, origin);

        var option = request.EligibleCards.FirstOrDefault(c => c.SourceIndex == sourceIndex.Value);
        if (option is null) return false;
        DuelZone zone = request.Source == CardSelectionSource.Deck ? DuelZone.Deck : DuelZone.Graveyard;
        if (!TryCaptureCardTarget(request.Owner, zone, sourceIndex.Value, out var target) ||
            !TryResolveCardTarget(target, out int currentIndex, out var card) ||
            currentIndex != sourceIndex.Value || !ReferenceEquals(option.Card, card))
            return false;

        return zone == DuelZone.Deck
            ? TryQueueDeckSearch(request, target, out message, origin)
            : TryQueueGraveyardRecovery(request, target, out message, origin);
    }
}
