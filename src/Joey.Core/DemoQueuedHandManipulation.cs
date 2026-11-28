using Joey.Cards;

namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingHandManipulationAnswer;

    private static bool IsHandManipulationSpell(Card card) =>
        card.ClassCode == 22 && HandManipulationEffects.For(card.Id) is not null;

    private static bool IsHandManipulationRequest(PendingCardSelection request) =>
        request.Source == CardSelectionSource.Hand &&
        request.Destination is CardSelectionDestination.Graveyard or CardSelectionDestination.Deck &&
        request.Filter == CardSelectionFilter.AnyCard && request.MinSelections == 1 &&
        request.MaxSelections == 1 && HandManipulationEffects.For(request.EffectResourceId) is not null;

    private bool CanResolveHandManipulationSpell(CardTarget source)
    {
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || source.Side != ActivePlayer ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source, out int index, out var card) ||
            !IsHandManipulationSpell(card) || HandManipulationEffects.For(card.Id) is not { } effect ||
            Life[source.Side] <= effect.LifeCost) return false;
        if (source.Zone == DuelZone.Hand)
        {
            if (FirstFreeSpellZone() < 0) return false;
        }
        else if (source.Zone != DuelZone.SpellTrap || !SpellFaceDown[source.Side,index] ||
                 equipment.Any(e => e.Owner == source.Side && e.SourceZone == index)) return false;

        int affected = effect.Kind == HandManipulationKind.DrawThenDiscard ? source.Side : 1-source.Side;
        if (effect.DrawCount > 0 && DeckCount(source.Side) < effect.DrawCount) return false;
        if (effect.DrawCount == 0 && hands[affected].Count == 0) return false;
        return Enumerable.Range(0,hands[affected].Count).All(i =>
            TryCaptureCardTarget(affected,DuelZone.Hand,i,out _));
    }

    private bool TryQueueHandManipulationSpell(CardTarget source, out string message, ActionOrigin origin)
    {
        message = "Choose a hand manipulation spell with legal cost and affected hand.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            !CanResolveHandManipulationSpell(source)) return false;
        queue.Enqueue(new ActivateHandManipulationSpellCommand(source.Side,source),origin);
        message = "Hand manipulation admitted.";
        return true;
    }

    private bool CanAdmitHandManipulationAnswer() =>
        !GameOver && Phase == DemoPhase.Main &&
        PendingCardSelection is { } request && IsHandManipulationRequest(request) &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    private bool TryGetHandManipulationTarget(PendingCardSelection request, int index, out CardTarget target)
    {
        target = default;
        var option = request.EligibleCards.FirstOrDefault(c => c.SourceIndex == index);
        return CanAdmitHandManipulationAnswer() && ReferenceEquals(request,PendingCardSelection) &&
            option is not null && TryCaptureCardTarget(request.SourceSide,DuelZone.Hand,index,out target) &&
            TryResolveCardTarget(target,out _,out var card) && ReferenceEquals(card,option.Card);
    }

    private bool TrySubmitHandManipulationAnswer(PendingCardSelection request, int? index,
        out string message, ActionOrigin origin)
    {
        message = "Choose one current hand occurrence.";
        if (primitiveActionQueue is not { IsBusy: false } queue || index is not int pick ||
            !TryGetHandManipulationTarget(request,pick,out var target)) return false;
        admittingHandManipulationAnswer = true;
        try { queue.Enqueue(new ResolveHandManipulationCommand(request.Owner,request,target),origin); }
        finally { admittingHandManipulationAnswer = false; }
        message = "Hand selection admitted.";
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private sealed class HandManipulationBatch(CardTarget source, ushort id, HandManipulationEffect effect)
        {
            public CardTarget Source { get; } = source;
            public ushort ResourceId { get; } = id;
            public HandManipulationEffect Effect { get; } = effect;
            public int Remaining { get; set; } = effect.ChosenCount;
            public bool SourceSpent { get; set; }
            public bool InitialEffectsIssued { get; set; }
            public int Affected => Effect.Kind == HandManipulationKind.DrawThenDiscard ? Source.Side : 1-Source.Side;
            public int Chooser => Effect.Kind == HandManipulationKind.RandomThenChosenDiscard ? Affected : Source.Side;
        }
        private HandManipulationBatch? handManipulation;
        private bool DeferHandManipulationExodia =>
            handManipulation?.Effect.Kind == HandManipulationKind.DrawThenDiscard;

        private CommandResult ActivateHandManipulation(ActivateHandManipulationSpellCommand command, IActionCommandSink followups)
        {
            if (handManipulation is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.CanResolveHandManipulationSpell(command.Source) ||
                !duel.TryResolveCardTarget(command.Source,out _,out var card) ||
                HandManipulationEffects.For(card.Id) is not { } effect)
                return new(CommandOutcome.Rejected,"Hand spell no longer legal.");
            handManipulation = new(command.Source,card.Id,effect);
            var context = new EffectContext();
            // Existing sandbox LP primitive, with cost eligibility checked before admission.
            context.ChangeLifePoints(command.Side,-effect.LifeCost);
            context.SendToGraveyard(command.Source);
            queue.EnqueueBatch(context.Commands);
            queue.ScheduleContinuation(_ => new([new AdvanceHandManipulationCommand(command.Side,command.Source)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult AdvanceHandManipulation(AdvanceHandManipulationCommand command, IActionCommandSink followups)
        {
            if (handManipulation is not { } batch || batch.Source != command.Source ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected,"No matching hand spell.");
            if (!batch.SourceSpent || duel.GameOver)
            {
                handManipulation = null;
                return new(CommandOutcome.Skipped,"Hand spell not spent or duel ended.");
            }
            if (!batch.InitialEffectsIssued)
            {
                batch.InitialEffectsIssued = true;
                if (batch.Effect.DrawCount > 0)
                {
                    var context = new EffectContext();
                    context.Draw(batch.Source.Side,batch.Effect.DrawCount);
                    queue.EnqueueBatch(context.Commands);
                    queue.ScheduleContinuation(_ => new([command]));
                    return new(CommandOutcome.Applied);
                }
                if (batch.Effect.Kind == HandManipulationKind.RandomThenChosenDiscard &&
                    duel.hands[batch.Affected].Count > 0)
                {
                    int index = duel.effectRandom.Next(duel.hands[batch.Affected].Count);
                    if (!duel.TryCaptureCardTarget(batch.Affected,DuelZone.Hand,index,out var random))
                        throw new InvalidOperationException("Random discard occurrence changed.");
                    queue.Enqueue(new DiscardCommand(batch.Affected,random));
                    queue.ScheduleContinuation(_ => new([command]));
                    return new(CommandOutcome.Applied);
                }
            }
            if (batch.Remaining == 0 || duel.hands[batch.Affected].Count == 0)
            {
                handManipulation = null;
                if (batch.Effect.Kind == HandManipulationKind.DrawThenDiscard) duel.CheckExodia(batch.Source.Side);
                return new(CommandOutcome.Applied);
            }
            var candidates = CardSelectionRules.Candidates(duel.hands[batch.Affected],CardSelectionFilter.AnyCard);
            duel.selectedBatchIndices.Clear();
            duel.PendingCardSelection = new(batch.Chooser,CardSelectionSource.Hand,
                batch.Effect.Kind == HandManipulationKind.OpponentChosenReturn
                    ? CardSelectionDestination.Deck : CardSelectionDestination.Graveyard,
                CardSelectionFilter.AnyCard,candidates,1,1,batch.ResourceId,HandSourceSide:batch.Affected);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveHandManipulation(ResolveHandManipulationCommand command, IActionCommandSink followups)
        {
            if (handManipulation is not { } batch || batch.Chooser != command.Side ||
                batch.ResourceId != command.Request.EffectResourceId || command.Target.Side != batch.Affected ||
                batch.Remaining <= 0 || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryResolveCardTarget(command.Target,out int index,out _) ||
                !duel.TryGetHandManipulationTarget(command.Request,index,out var target) || target != command.Target)
                return new(CommandOutcome.Rejected,"Hand choice changed.");
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
            batch.Remaining--;
            if (batch.Effect.Kind == HandManipulationKind.OpponentChosenReturn)
                queue.EnqueueBatch([new ReturnToDeckCommand(batch.Affected,target),new ShuffleDeckCommand(batch.Affected)]);
            else queue.Enqueue(new DiscardCommand(batch.Affected,target));
            queue.ScheduleContinuation(_ => new([new AdvanceHandManipulationCommand(batch.Source.Side,batch.Source)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ReturnHandToDeck(CardTarget target)
        {
            if (handManipulation is not { Effect.Kind: HandManipulationKind.OpponentChosenReturn } batch ||
                batch.Affected != target.Side || target.Zone != DuelZone.Hand ||
                !duel.TryResolveCardTarget(target,out int index,out var card))
                return new(CommandOutcome.Rejected,"No reserved hand return.");
            duel.hands[target.Side].RemoveAt(index);
            duel.decks[target.Side].Enqueue(card);
            return new(CommandOutcome.Applied);
        }
    }
}
