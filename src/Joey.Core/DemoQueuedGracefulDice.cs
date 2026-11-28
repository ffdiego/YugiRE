using Joey.Cards;

namespace Joey.Core;

public sealed partial class DemoDuel
{
    public event Action<DiceRollPresentation>? DiceRolled;

    private static bool IsGracefulDice(Card card) =>
        card is { Id: DiceSpellEffects.GracefulDice, ClassCode: 22 };

    private IEnumerable<Card> GracefulDiceMonsters(int side)
    {
        for (int slot = 0; slot < 5; slot++)
            if (TryCaptureCardTarget(side, DuelZone.Monster, slot, out var target) &&
                TryResolveTemporaryStatTarget(target, 22, out var monster))
                yield return monster;
    }

    private bool CanResolveGracefulDice(CardTarget source, out int spellSlot)
    {
        spellSlot = -1;
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || ActivePlayer != source.Side ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source, out int index, out var card) ||
            !IsGracefulDice(card) || !GracefulDiceMonsters(source.Side).Any())
            return false;

        if (source.Zone == DuelZone.Hand)
            spellSlot = FirstFreeSpellZone();
        else if (source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side, index] &&
                 spellSetTurn[source.Side, index] < Turn)
            spellSlot = index;
        return spellSlot >= 0;
    }

    private bool TryQueueGracefulDice(CardTarget source, out string message, ActionOrigin origin)
    {
        message = "Graceful Dice requires an eligible face-up monster and an available activation.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanResolveGracefulDice(source, out _))
            return false;
        queue.Enqueue(new ActivateGracefulDiceCommand(source.Side, source), origin);
        message = "Graceful Dice admitted.";
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private CardTarget? gracefulDiceSource;
        private bool gracefulDiceSourceSpent;

        private CommandResult ActivateGracefulDice(ActivateGracefulDiceCommand activate, IActionCommandSink followups)
        {
            if (gracefulDiceSource is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.CanResolveGracefulDice(activate.Source, out int spellSlot))
                return new(CommandOutcome.Rejected, "Graceful Dice activation changed.");

            gracefulDiceSource = activate.Source;
            gracefulDiceSourceSpent = false;
            queue.EnqueueBatch([new SendToGraveyardCommand(activate.Side, activate.Source),
                new RollGracefulDiceCommand(activate.Side, activate.Source, spellSlot + 5)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult RollGracefulDice(RollGracefulDiceCommand roll)
        {
            if (gracefulDiceSource != roll.Source)
                return new(CommandOutcome.Rejected, "Graceful Dice activation is missing.");
            gracefulDiceSource = null;
            if (!gracefulDiceSourceSpent)
                return new(CommandOutcome.Skipped, "Graceful Dice source was not spent.");

            int face = duel.effectRandom.Next(1, 7);
            int bonus = DiceSpellEffects.GracefulDiceBonus(face);
            foreach (var monster in duel.GracefulDiceMonsters(roll.Side))
            {
                var prior = duel.temporaryStats.GetValueOrDefault(monster);
                duel.temporaryStats[monster] = new(prior.Attack + bonus, prior.Defense + bonus);
            }
            duel.DiceRolled?.Invoke(new(roll.Side, roll.Region, face));
            return new(CommandOutcome.Applied, $"Graceful Dice rolled {face}.");
        }
    }
}
