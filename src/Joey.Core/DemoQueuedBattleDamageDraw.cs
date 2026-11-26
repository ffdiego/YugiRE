namespace Joey.Core;

public sealed partial class DemoDuel
{
    private static ResolveBattleDamageDrawCommand? BattleDrawAfterDamage(Card source, int controller, int amount) =>
        BattleDamageEffectRules.AfterBattleDamage(source.Id, controller, 1 - controller, amount, false) is
            { Kind: BattleDamageEffectKind.Draw } intent ? new(controller, intent) : null;

    private static void EnqueueDirectBattleDamage(Card source, int controller, int damage, IActionBatchSink queue)
    {
        if (damage <= 0) return;
        var hit = new DamageCommand(1 - controller, damage);
        if (BattleDrawAfterDamage(source, controller, damage) is { } draw)
            queue.EnqueueBatch([hit, draw]);
        else
            queue.Enqueue(hit);
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private FinishBattleDamageDrawCommand? pendingBattleDrawFinish;

        private CommandResult ResolveBattleDamageDraw(ResolveBattleDamageDrawCommand command, IActionCommandSink followups)
        {
            if (duel.GameOver) return new(CommandOutcome.Skipped, "Battle damage ended the duel.");
            if (pendingBattleDrawFinish is not null || reservedMandatoryDrawEffects.Count != 0 ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Battle draws require an isolated damage result.");

            var intent = command.Intent;
            int count = Math.Min(intent.MaximumCount, duel.DeckCount(intent.AffectedSide));
            var draws = Enumerable.Range(0, count).Select(_ => new DrawCommand(intent.AffectedSide)).ToArray();
            var finish = new FinishBattleDamageDrawCommand(command.Side, intent, count);
            queue.EnqueueBatch(draws.Cast<DuelCommand>().Append(finish));
            foreach (var draw in draws) reservedMandatoryDrawEffects.Enqueue(draw);
            pendingBattleDrawFinish = finish;
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishBattleDamageDraw(FinishBattleDamageDrawCommand command)
        {
            if (!ReferenceEquals(command, pendingBattleDrawFinish) || reservedMandatoryDrawEffects.Count != 0)
                return new(CommandOutcome.Rejected, "No matching battle draw batch to finish.");

            pendingBattleDrawFinish = null;
            var intent = command.Intent;
            bool shortfall = command.Drawn < intent.MaximumCount;
            if (shortfall && !duel.GameOver) duel.Winner = 1 - intent.AffectedSide;
            duel.LastDeckFlow = new(intent.SourceResourceId, command.Side, intent.AffectedSide,
                intent.MaximumCount, command.Drawn, command.Drawn, 0, 0, shortfall);
            return new(CommandOutcome.Applied);
        }
    }
}
