namespace Joey.Core;

public sealed partial class DemoDuel
{
    public bool TryQueueTurnDraw(out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Turn draw requires an idle duel and an unused draw for the active player.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanDraw)
            return false;

        queue.Enqueue(new TurnDrawCommand(ActivePlayer, Turn), origin);
        message = "Turn draw admitted.";
        return true;
    }

    private CommandResult ExecuteQueuedTurnDraw(TurnDrawCommand command, IActionCommandSink followups)
    {
        if (primitiveActionQueue is not { PendingCount: 0 } || command.Side != ActivePlayer ||
            command.ExpectedTurn != Turn || !AllowsTurnDraw)
            return new(CommandOutcome.Rejected, "Turn draw is no longer legal for this side and turn.");

        if (!TakeTurnDraw(out bool skipped))
            return new(CommandOutcome.Applied, "Empty deck: active player loses (demo policy).");

        if (!GameOver && FindDueStandbyRevival() >= 0)
            followups.Enqueue(new ResolveStandbyRevivalCommand(command.Side, command.ExpectedTurn));
        return new(CommandOutcome.Applied, skipped ? "Draw Phase skipped." : "");
    }

    private int FindDueStandbyRevival() =>
        delayedRevivals.FindLastIndex(effect => effect.Owner == ActivePlayer && effect.DueTurn <= Turn);

    private CommandResult ExecuteQueuedStandbyRevival(ResolveStandbyRevivalCommand command, IActionCommandSink followups)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || Phase != DemoPhase.Main || !drawn)
            return new(CommandOutcome.Rejected, "Standby revival is no longer due for this turn.");

        int index = FindDueStandbyRevival();
        if (index < 0)
            return new(CommandOutcome.Skipped, "No delayed revival remains.");

        var revival = delayedRevivals[index];
        delayedRevivals.RemoveAt(index);
        int graveIndex = graves[command.Side].FindIndex(card => ReferenceEquals(card, revival.Source));
        int slot = Enumerable.Range(0, 5).FirstOrDefault(z => Monsters[command.Side, z] is null, -1);
        if (slot >= 0 && graveIndex >= 0 && TryCaptureCardTarget(command.Side, DuelZone.Graveyard, graveIndex, out var target))
            followups.Enqueue(new SpecialSummonCommand(command.Side, target, slot, FaceUpDefense: true));
        if (FindDueStandbyRevival() >= 0)
            followups.Enqueue(new ResolveStandbyRevivalCommand(command.Side, command.ExpectedTurn));
        return new(CommandOutcome.Applied);
    }
}
