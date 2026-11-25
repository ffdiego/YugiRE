namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingQueuedFusion;

    public bool UsesQueuedFusionSelection(PendingFusionSummon request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingFusionSummon, request);

    private bool CanAdmitQueuedFusion() =>
        !GameOver && Phase == DemoPhase.Main && PendingFusionSummon is { Owner: var owner } &&
        owner == ActivePlayer && PendingEndDiscards == 0 &&
        PendingFlipTarget is null && PendingFlipSpellTrapTargets is null &&
        PendingCardSelection is null && PendingSummonResponse is null &&
        PendingAttackResponse is null && PendingEffectDecision is null &&
        PendingSpellTrapTarget is null && !deferringFieldRemovalTriggers;

    public bool TryQueueFusionSummon(PendingFusionSummon request, int deckIndex,
        IReadOnlyList<FusionMaterial> materials, out string message, bool defense = false,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a current Fusion result and distinct tracked materials.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanAdmitQueuedFusion() ||
            !TryPlanQueuedFusion(request, deckIndex, materials, defense, out var prepare, out _, out message))
            return false;
        admittingQueuedFusion = true;
        try { queue.Enqueue(prepare, origin); }
        finally { admittingQueuedFusion = false; }
        message = "Fusion Summon admitted.";
        return true;
    }

    private bool TryPlanQueuedFusion(PendingFusionSummon request, int deckIndex,
        IReadOnlyList<FusionMaterial> materials, bool defense, out PrepareFusionSummonCommand prepare,
        out Dictionary<CardTarget, DuelCommand[]> links, out string message)
    {
        prepare = null!;
        links = new();
        if (!TryValidateFusionChoice(request, deckIndex, materials, out var choice, out int slot, out message))
            return false;
        message = "Fusion source, result or materials are not tracked.";
        int side = request.Owner;
        bool gate = request.FieldOwner is not null;
        var sourceZone = gate ? DuelZone.FieldSpell : request.FromSet ? DuelZone.SpellTrap : DuelZone.Hand;
        if (!TryCaptureCardTarget(request.FieldOwner ?? side, sourceZone, gate ? 0 : request.SourceIndex, out var source) ||
            !TryCaptureCardTarget(side, DuelZone.FusionDeck, deckIndex, out var result) ||
            !TryResolveCardTarget(result, out _, out var resultCard) || !ReferenceEquals(resultCard, choice.Result))
            return false;
        var payments = new List<CardTarget>();
        foreach (var material in materials)
        {
            var zone = material.Zone == FusionMaterialZone.Hand ? DuelZone.Hand : DuelZone.Monster;
            if (!TryCaptureCardTarget(side, zone, material.Index, out var target) ||
                !TryResolveCardTarget(target, out _, out var card) || !ReferenceEquals(card, material.Card) ||
                payments.Contains(target) || target == source) return false;
            payments.Add(target);
            if (zone == DuelZone.Monster)
            {
                if (!TryPlanRemovalLinks(side, material.Index, out var planned, toGraveyard: !gate))
                    return false;
                links.Add(target, planned);
            }
        }
        prepare = new(side, request, deckIndex, Array.AsReadOnly(materials.ToArray()), source, result,
            payments.AsReadOnly(), slot, defense);
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private sealed class FusionBatch(PrepareFusionSummonCommand plan, IEnumerable<DuelCommand> commands,
            Dictionary<CardTarget, DuelCommand[]> links)
        {
            public PrepareFusionSummonCommand Plan { get; } = plan;
            public Queue<DuelCommand> Remaining { get; } = new(commands);
            public Dictionary<CardTarget, DuelCommand[]> Links { get; } = links;
        }

        private FusionBatch? fusionBatch;

        private CommandResult PrepareFusionSummon(PrepareFusionSummonCommand prepare, IActionCommandSink followups)
        {
            if (fusionBatch is not null || !duel.CanAdmitQueuedFusion() ||
                followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedFusion(prepare.Request, prepare.DeckIndex, prepare.Materials, prepare.Defense,
                    out var current, out var links, out _) ||
                current.Source != prepare.Source || current.Result != prepare.Result ||
                current.Slot != prepare.Slot || !current.Payments.SequenceEqual(prepare.Payments))
                return new(CommandOutcome.Rejected, "Fusion reservation changed before execution.");

            bool gate = prepare.Request.FieldOwner is not null;
            var commands = new List<DuelCommand>();
            if (!gate) commands.Add(new SendToGraveyardCommand(prepare.Side, prepare.Source));
            foreach (var payment in prepare.Payments)
            {
                commands.Add(gate ? new BanishCommand(prepare.Side, payment) :
                    new SendToGraveyardCommand(prepare.Side, payment));
                if (links.TryGetValue(payment, out var linked)) commands.AddRange(linked);
            }
            commands.Add(new SpecialSummonCommand(prepare.Side, prepare.Result, prepare.Slot, FaceUpDefense: prepare.Defense));
            queue.EnqueueBatch(commands);
            fusionBatch = new(prepare, commands, links);
            duel.PendingFusionSummon = null;
            queue.ScheduleContinuation(_ => new([new FinishFusionSummonCommand(prepare.Side)]));
            return new(CommandOutcome.Applied);
        }

        // This reservation is closed to external admission. Reuse normal
        // movement command types, but do not grant unrestricted Fusion Deck
        // summons or field banishment to callers outside the paid batch.
        private CommandResult ExecuteFusionStep(DuelCommand command)
        {
            var batch = fusionBatch!;
            if (!batch.Remaining.TryPeek(out var expected) || expected != command)
                throw new InvalidOperationException("Unexpected command in reserved Fusion batch.");
            var plan = batch.Plan;
            bool gate = plan.Request.FieldOwner is not null;
            if (gate && (!duel.TryResolveCardTarget(plan.Source, out _, out _) ||
                !duel.IsActiveFusionGate(plan.Source.Side)))
                throw new InvalidOperationException("Reserved Fusion Gate changed.");
            CommandResult result;
            switch (command)
            {
                case SendToGraveyardCommand spend when spend.Target == plan.Source:
                    if (!duel.TryResolveCardTarget(spend.Target, out int sourceIndex, out var spell) ||
                        !FusionRules.Supports(spell))
                        throw new InvalidOperationException("Reserved Fusion spell changed.");
                    if (spend.Target.Zone == DuelZone.Hand)
                    {
                        if (duel.FirstFreeSpellZone() < 0)
                            throw new InvalidOperationException("Fusion activation slot changed.");
                        result = SendHandToGraveyard(spend.Target, discard: false);
                    }
                    else
                    {
                        if (!duel.SpellFaceDown[spend.Side, sourceIndex] ||
                            duel.equipment.Any(e => e.Owner == spend.Side && e.SourceZone == sourceIndex))
                            throw new InvalidOperationException("Reserved Set Fusion spell changed.");
                        duel.ClearSpellTrapSlot(spend.Side, sourceIndex);
                        duel.graves[spend.Side].Add(spell);
                        result = new(CommandOutcome.Applied);
                    }
                    break;
                case SendToGraveyardCommand send:
                    result = MoveFusionMaterial(batch, send.Target, false);
                    break;
                case BanishCommand banish:
                    result = MoveFusionMaterial(batch, banish.Target, true);
                    break;
                case DestroyCommand destroy:
                    result = DestroyQueuedSpellTrap(destroy.Target);
                    break;
                case DamageCommand damage:
                    duel.ChangeLife(damage.Side, -damage.Amount);
                    result = new(CommandOutcome.Applied);
                    break;
                case SpecialSummonCommand summon:
                    if (summon.Source != plan.Result || batch.Remaining.Count != 1 ||
                        !duel.TryResolveCardTarget(summon.Source, out int index, out var monster) ||
                        !FusionRules.IsFusionMonster(monster) || duel.Monsters[summon.Side, summon.Slot] is not null)
                        throw new InvalidOperationException("Reserved Fusion result or destination changed.");
                    duel.fusionDecks[summon.Side].RemoveAt(index);
                    duel.PlaceSelectedMonster(summon.Side, summon.Slot, monster, false, summon.FaceUpDefense);
                    result = new(CommandOutcome.Applied);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported reserved Fusion step.");
            }
            if (result.Outcome != CommandOutcome.Applied)
                throw new InvalidOperationException("Reserved Fusion step could not be applied: " + result.Detail);
            batch.Remaining.Dequeue();
            return result;
        }

        private CommandResult MoveFusionMaterial(FusionBatch batch, CardTarget target, bool banish)
        {
            if (!batch.Plan.Payments.Contains(target) ||
                !duel.TryResolveCardTarget(target, out int index, out var card))
                throw new InvalidOperationException("Reserved Fusion material changed.");
            if (target.Zone == DuelZone.Hand)
            {
                if (!banish) return SendHandToGraveyard(target, discard: false);
                return BanishFromSource(target);
            }
            if (!batch.Links.TryGetValue(target, out var expected) ||
                !duel.TryPlanRemovalLinks(target.Side, index, out var current, toGraveyard: !banish) ||
                !current.SequenceEqual(expected))
                throw new InvalidOperationException("Reserved Fusion material links changed.");

            if (banish)
            {
                duel.banished[target.Side].Add(card);
                duel.ClearMonsterSlot(target.Side, index, removeEquipment: false);
            }
            else
            {
                duel.deferringFieldRemovalTriggers = true;
                try { duel.RemoveMonster(target.Side, index, isFusionMaterial: true, queueLinkedConsequences: true); }
                finally { duel.deferringFieldRemovalTriggers = false; }
            }
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishFusionSummon(FinishFusionSummonCommand finish)
        {
            if (fusionBatch is not { } batch || batch.Plan.Side != finish.Side || batch.Remaining.Count != 0)
                return new(CommandOutcome.Rejected, "No completed Fusion batch to finalize.");
            fusionBatch = null;
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }
    }
}
