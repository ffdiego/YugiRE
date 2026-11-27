using Joey.Cards;

namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingHandManipulationAnswer;

    private static bool IsHandManipulationSpell(Card card) =>
        card.ClassCode == 22 && HandManipulationEffects.For(card.Id) is not null;

    private static bool IsHandManipulationRequest(PendingCardSelection request) =>
        request.Source is CardSelectionSource.Hand or CardSelectionSource.Graveyard &&
        request.Destination is CardSelectionDestination.Graveyard or CardSelectionDestination.Deck or CardSelectionDestination.Banished &&
        request.MinSelections is 0 or 1 && request.MaxSelections == 1 &&
        HandManipulationEffects.For(request.EffectResourceId) is not null;

    private static bool IsGraveBanish(HandManipulationKind kind) =>
        kind is HandManipulationKind.OpponentGraveBanish or HandManipulationKind.EitherGraveBanish;

    private static bool UsesOwnHand(HandManipulationKind kind) =>
        kind is HandManipulationKind.DrawThenDiscard or HandManipulationKind.OwnMonsterDiscard;

    private static CardSelectionFilter ManipulationFilter(HandManipulationKind kind) =>
        kind is HandManipulationKind.OwnMonsterDiscard or HandManipulationKind.OpponentGraveBanish
            ? CardSelectionFilter.AnyMonster : CardSelectionFilter.AnyCard;

    private IReadOnlyList<CardSelectionCandidate> ManipulationCandidates(int caster, HandManipulationKind kind)
    {
        bool grave = IsGraveBanish(kind);
        int[] sides = kind == HandManipulationKind.EitherGraveBanish ? [caster,1-caster] :
            [UsesOwnHand(kind) ? caster : 1-caster];
        var result = new List<CardSelectionCandidate>();
        foreach (int side in sides)
        {
            var cards = grave ? graves[side] : hands[side];
            for (int i = 0; i < cards.Count; i++)
            {
                if (!CardSelectionRules.IsEligible(cards[i],ManipulationFilter(kind))) continue;
                if (!TryCaptureCardTarget(side,grave ? DuelZone.Graveyard : DuelZone.Hand,i,out _))
                    return [];
                result.Add(new(i,cards[i]) { SourceSide = side,
                    ChoiceKey = kind == HandManipulationKind.EitherGraveBanish ? result.Count : null });
            }
        }
        return result.AsReadOnly();
    }

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

        if (effect.DrawCount > 0)
            return DeckCount(source.Side) >= effect.DrawCount &&
                Enumerable.Range(0,hands[source.Side].Count).All(i =>
                    TryCaptureCardTarget(source.Side,DuelZone.Hand,i,out _));
        return ManipulationCandidates(source.Side,effect.Kind).Count > 0;
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
        !GameOver && Phase == DemoPhase.Main && deferredBattle is null &&
        PendingCardSelection is { } request && IsHandManipulationRequest(request) &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    private bool TryGetHandManipulationTarget(PendingCardSelection request, int index, out CardTarget target)
    {
        target = default;
        var option = request.EligibleCards.FirstOrDefault(c => c.AnswerIndex == index);
        return CanAdmitHandManipulationAnswer() && ReferenceEquals(request,PendingCardSelection) &&
            option is not null && TryCaptureCardTarget(option.SourceSide ?? request.SourceSide,
                request.Source == CardSelectionSource.Graveyard ? DuelZone.Graveyard : DuelZone.Hand,
                option.SourceIndex,out target) &&
            TryResolveCardTarget(target,out _,out var card) && ReferenceEquals(card,option.Card);
    }

    private bool TrySubmitHandManipulationAnswer(PendingCardSelection request, int? index,
        out string message, ActionOrigin origin)
    {
        message = "Choose one current hand occurrence.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !CanAdmitHandManipulationAnswer() || !ReferenceEquals(request,PendingCardSelection)) return false;
        CardTarget? target = null;
        if (index is int pick)
        {
            if (!TryGetHandManipulationTarget(request,pick,out var found)) return false;
            target = found;
        }
        else if (request.MinSelections != 0) return false;
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
            public int Affected => UsesOwnHand(Effect.Kind) ? Source.Side : 1-Source.Side;
            public bool CanStop => Effect.OptionalAfterFirst && Remaining < Effect.ChosenCount;
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
            var candidates = duel.ManipulationCandidates(batch.Source.Side,batch.Effect.Kind);
            if (batch.Remaining == 0 || candidates.Count == 0)
            {
                handManipulation = null;
                if (batch.Effect.Kind == HandManipulationKind.DrawThenDiscard) duel.CheckExodia(batch.Source.Side);
                return new(CommandOutcome.Applied);
            }
            bool grave = IsGraveBanish(batch.Effect.Kind);
            duel.selectedBatchIndices.Clear();
            duel.PendingCardSelection = new(batch.Chooser,grave ? CardSelectionSource.Graveyard : CardSelectionSource.Hand,
                grave ? CardSelectionDestination.Banished :
                batch.Effect.Kind == HandManipulationKind.OpponentChosenReturn
                    ? CardSelectionDestination.Deck : CardSelectionDestination.Graveyard,
                ManipulationFilter(batch.Effect.Kind),candidates,batch.CanStop ? 0 : 1,1,
                batch.ResourceId,HandSourceSide:batch.Affected);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveHandManipulation(ResolveHandManipulationCommand command, IActionCommandSink followups)
        {
            if (handManipulation is not { } batch || batch.Chooser != command.Side ||
                batch.ResourceId != command.Request.EffectResourceId ||
                batch.Remaining <= 0 || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.CanAdmitHandManipulationAnswer() ||
                !ReferenceEquals(command.Request,duel.PendingCardSelection))
                return new(CommandOutcome.Rejected,"Selection changed.");
            if (command.Target is { } chosen)
            {
                if (!command.Request.EligibleCards.Any(c =>
                    duel.TryGetHandManipulationTarget(command.Request,c.AnswerIndex,out var current) &&
                    current == chosen))
                    return new(CommandOutcome.Rejected,"Selection occurrence changed.");
                batch.Remaining--;
                if (IsGraveBanish(batch.Effect.Kind))
                    queue.Enqueue(new BanishCommand(chosen.Side,chosen));
                else if (batch.Effect.Kind == HandManipulationKind.OpponentChosenReturn)
                    queue.EnqueueBatch([new ReturnToDeckCommand(chosen.Side,chosen),new ShuffleDeckCommand(chosen.Side)]);
                else queue.Enqueue(new DiscardCommand(chosen.Side,chosen));
            }
            else
            {
                if (!batch.CanStop || command.Request.MinSelections != 0)
                    return new(CommandOutcome.Rejected,"Mandatory selection cannot be declined.");
                batch.Remaining = 0;
            }
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
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
