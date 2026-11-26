namespace Joey.Core;

public sealed partial class DemoDuel
{
    private sealed partial class PrimitiveActionExecutor
    {
        private int[]? cardDestructionDiscards;
        private bool resolvingCardDestructionDraws;

        private ActionContinuationStep PlanCardDestruction(int side)
        {
            var discards = new List<DuelCommand>();
            foreach (int player in new[] { side, 1 - side })
            for (int index = 0; index < duel.hands[player].Count; index++)
            {
                if (!duel.TryCaptureCardTarget(player, DuelZone.Hand, index, out var target))
                    throw new InvalidOperationException("Card Destruction requires tracked hand occurrences.");
                discards.Add(new DiscardCommand(player, target));
            }

            cardDestructionDiscards = new int[2];
            return new(discards, _ => PlanCardDestructionDraws(side));
        }

        private ActionContinuationStep PlanCardDestructionDraws(int side)
        {
            var discarded = cardDestructionDiscards ??
                throw new InvalidOperationException("Card Destruction has no completed discard batch.");
            cardDestructionDiscards = null;
            int opponent = 1 - side;
            int opponentAvailable = duel.DeckCount(opponent);
            var context = new EffectContext();
            context.Draw(side, discarded[side]);
            context.Draw(opponent, Math.Min(discarded[opponent], opponentAvailable));
            resolvingCardDestructionDraws = true;
            return new([.. context.Commands, new FinishCardDestructionCommand(side, discarded[opponent] > opponentAvailable)]);
        }

        private CommandResult FinishCardDestruction(FinishCardDestructionCommand finish)
        {
            if (!resolvingCardDestructionDraws)
                return new(CommandOutcome.Rejected, "Card Destruction has no completed draw batch.");
            resolvingCardDestructionDraws = false;
            if (finish.OpponentDeckOut) duel.Winner = finish.Side;
            else if (ExodiaRules.CompleteHand(duel.hands[0]) is not null &&
                     ExodiaRules.CompleteHand(duel.hands[1]) is not null) duel.IsDraw = true;
            else
            {
                duel.CheckExodia(0);
                duel.CheckExodia(1);
            }
            return new(CommandOutcome.Applied);
        }
    }
}
