namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>
    /// A02 pilot for effect-level Draw(1), Damage and Heal, reusing the legacy
    /// state helpers. Not a legal player-action interface: no normal-draw
    /// allowance or Standby processing. Do not mix this queue with legacy
    /// input/CPU calls while it is busy. Other commands deliberately reject
    /// until the occurrence/trigger adapters are migrated in A02/A06/C12.
    /// </summary>
    public ActionQueue CreatePrimitiveActionQueue(IPresentationGate? gate = null) =>
        new(new PrimitiveActionExecutor(this), gate);

    private sealed class PrimitiveActionExecutor(DemoDuel duel) : IDuelCommandExecutor
    {
        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            if (duel.ActionsBlocked) return new(CommandOutcome.Rejected, "Duel has ended or a legacy decision is pending.");
            switch (command)
            {
                case DrawCommand draw:
                    if (duel.decks[draw.Side].Count == 0)
                    {
                        duel.Winner = 1 - draw.Side;
                        return new(CommandOutcome.Applied, "Empty deck: player loses (sandbox policy).");
                    }
                    duel.DrawToHand(draw.Side);
                    return new(CommandOutcome.Applied);
                case DamageCommand damage:
                    duel.ChangeLife(damage.Side, -damage.Amount);
                    return new(CommandOutcome.Applied);
                case HealCommand heal:
                    duel.ChangeLife(heal.Side, heal.Amount);
                    return new(CommandOutcome.Applied);
                default:
                    return new(CommandOutcome.Rejected, "Command not migrated to DemoDuel.");
            }
        }
    }
}
