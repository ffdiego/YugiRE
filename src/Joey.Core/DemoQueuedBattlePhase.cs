namespace Joey.Core;

public sealed partial class DemoDuel
{
    public bool TryQueueBattlePhaseChange(bool enterBattle, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = enterBattle ? "Battle Phase is unavailable." : "Battle Phase must be active and all decisions resolved.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked || !AllowsBattlePhaseChange(enterBattle))
            return false;

        queue.Enqueue(new ChangeBattlePhaseCommand(ActivePlayer, Turn, enterBattle), origin);
        message = "Battle Phase change admitted.";
        return true;
    }

    private CommandResult ExecuteQueuedBattlePhaseChange(ChangeBattlePhaseCommand command)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || !AllowsBattlePhaseChange(command.EnterBattle))
            return new(CommandOutcome.Rejected, "Battle Phase change no longer legal for this side and turn.");

        ApplyBattlePhaseChange(command.EnterBattle);
        return new(CommandOutcome.Applied, command.EnterBattle ? "Battle Phase." : "Main Phase 2.");
    }
}
