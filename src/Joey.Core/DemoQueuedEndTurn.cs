namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool queuedEndTurn;
    private bool admittingQueuedEndDiscard;

    public bool HasQueuedEndDiscard => queuedEndTurn && PendingEndDiscards != 0;

    public bool TryQueueEndTurn(out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Turn end requires an idle duel and supported End Phase effects.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            HasLegacyEndPhaseEffect() || Phase == DemoPhase.End && (!queuedEndTurn || PendingEndDiscards != 0))
            return false;

        DuelCommand command = queuedEndTurn
            ? new AdvanceTurnCommand(ActivePlayer, Turn)
            : new EndTurnCommand(ActivePlayer, Turn);
        queue.Enqueue(command, origin);
        message = queuedEndTurn ? "Turn advance admitted." : "End Phase admitted.";
        return true;
    }

    public bool TryQueueEndTurnDiscard(int index, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a tracked card for the pending end-turn discard.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanAdmitQueuedEndDiscard() ||
            !TryCaptureCardTarget(ActivePlayer, DuelZone.Hand, index, out var target))
            return false;

        admittingQueuedEndDiscard = true;
        try { queue.Enqueue(new EndTurnDiscardCommand(ActivePlayer, Turn, target), origin); }
        finally { admittingQueuedEndDiscard = false; }
        message = "End-turn discard admitted.";
        return true;
    }

    private bool CanAdmitQueuedEndDiscard() => !LegacyActionsBlocked && HasQueuedEndDiscard;

    private bool CanExecuteQueuedEndDiscard(EndTurnDiscardCommand command) =>
        CanAdmitQueuedEndDiscard() && command.Side == ActivePlayer && command.ExpectedTurn == Turn;

    private bool HasLegacyEndPhaseEffect() =>
        DestinyBoardRules.NextMessage(1 - ActivePlayer, Spells, SpellFaceDown) is not null;

    private CommandResult ExecuteQueuedEndTurn(EndTurnCommand command, IActionCommandSink followups)
    {
        if (primitiveActionQueue is not { PendingCount: 0 } || command.Side != ActivePlayer ||
            command.ExpectedTurn != Turn || Phase == DemoPhase.End ||
            HasLegacyEndPhaseEffect())
            return new(CommandOutcome.Rejected, "End Phase is no longer legal for this side and turn.");

        queuedEndTurn = true;
        Phase = DemoPhase.End;
        if (PendingEndDiscards == 0)
            followups.Enqueue(new AdvanceTurnCommand(command.Side, command.ExpectedTurn));
        return new(CommandOutcome.Applied, "End Phase.");
    }

    private CommandResult ExecuteQueuedTurnAdvance(AdvanceTurnCommand command)
    {
        if (!queuedEndTurn || Phase != DemoPhase.End || PendingEndDiscards != 0 ||
            command.Side != ActivePlayer || command.ExpectedTurn != Turn)
            return new(CommandOutcome.Rejected, "Turn advance is no longer legal.");

        queuedEndTurn = false;
        if (HasLegacyEndPhaseEffect())
            return new(CommandOutcome.Rejected, "An End Phase effect requires the existing resolver.");

        AdvanceTurn();
        return new(CommandOutcome.Applied, "Turn changed.");
    }
}
