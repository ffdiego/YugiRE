namespace Joey.Core;

public sealed partial class DemoDuel
{
    public bool UsesQueuedMonsterFlipChoice(DemoFlipTarget request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingFlipTarget, request) &&
        deferredBattle is null && IsQueuedMonsterFlip(request) &&
        (Phase == DemoPhase.Main || HasQueuedBattleTargetChoice);

    public bool UsesQueuedFlipSpellTrapChoice(PendingFlipSpellTrapTargets request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingFlipSpellTrapTargets, request) &&
        deferredBattle is null && Phase == DemoPhase.Main &&
        FlipSpellTrapSelectionRules.Effect(request.Source.Id) == request.Operation;

    public bool TrySubmitQueuedMonsterFlipChoice(DemoFlipTarget request,
        IReadOnlyList<CardTarget> targets, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose targets for the current supported FLIP.";
        if (!UsesQueuedMonsterFlipChoice(request) || primitiveActionQueue is not { IsBusy: false } ||
            !TryCaptureCardTarget(request.Owner, DuelZone.Monster, request.SourceZone, out var source))
            return false;
        return TryQueueMonsterFlipAnswer(source, FlipEffectsNegated ? [] : targets, out message, origin);
    }

    public bool TrySubmitQueuedFlipSpellTrapChoice(PendingFlipSpellTrapTargets request,
        IReadOnlyList<CardTarget> targets, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose targets for the current supported Spell/Trap FLIP.";
        if (!UsesQueuedFlipSpellTrapChoice(request) || primitiveActionQueue is not { IsBusy: false })
            return false;
        return TryQueueFlipSpellTrapPair(request, FlipEffectsNegated ? [] : targets, out message, origin);
    }
}
