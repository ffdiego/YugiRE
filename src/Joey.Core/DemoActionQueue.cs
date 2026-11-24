namespace Joey.Core;

public sealed partial class DemoDuel
{
    private ActionQueue? primitiveActionQueue;

    /// <summary>
    /// One queue per duel. Effect-level operations, not a general legal input API.
    /// Existing App/CPU entrypoints remain legacy; do not mix their decisions
    /// with admitted queue work. Direct array writes are fixture-only.
    /// </summary>
    public ActionQueue CreatePrimitiveActionQueue(IPresentationGate? gate = null)
    {
        if (primitiveActionQueue is not null)
            throw new InvalidOperationException("A duel already owns an action queue.");
        return primitiveActionQueue = new(new PrimitiveActionExecutor(this), gate,
            () => (!LegacyActionsBlocked || admittingQueuedMonsterFlipAnswer && CanAdmitQueuedMonsterFlipAnswer() ||
                admittingQueuedGraveyardRecovery && CanAdmitQueuedGraveyardRecovery() ||
                admittingQueuedDeckSearch && CanAdmitQueuedDeckSearch() ||
                admittingQueuedDeckRecruitment && CanAdmitQueuedDeckRecruitment()) && PendingEndDiscards == 0);
    }

    /// <summary>
    /// Opt-in legal hand activation for Raigeki only. No mutation at admission.
    /// The source and live target snapshot are revalidated in the executor.
    /// </summary>
    public bool TryQueueRaigekiFromHand(int index, out string message)
    {
        message = "Choose Raigeki in hand.";
        if (index < 0 || index >= Hand.Count || Hand[index].Id != MonsterWipeRules.Raigeki)
            return false;
        return TryQueueMonsterWipe(DuelZone.Hand, index, out message);
    }

    /// <summary>Opt-in activation of the nine existing non-targeting monster wipe spells.</summary>
    public bool TryQueueMonsterWipe(DuelZone sourceZone, int index, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported monster wipe with eligible tracked targets and an idle Main Phase.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !CanResolveQueuedMonsterWipe(source, out _, out var targets))
            return false;
        if (targets.Any(t => !CanQueueMonsterRemoval(t.Player, t.Zone) ||
            !TryCaptureCardTarget(t.Player, DuelZone.Monster, t.Zone, out _)))
            return false;
        queue.Enqueue(new ActivateMonsterWipeCommand(ActivePlayer, source), origin);
        message = "Monster wipe admitted.";
        return true;
    }

    private bool CanResolveQueuedMonsterWipe(CardTarget source, out Card spell,
        out IReadOnlyList<MonsterWipeTarget> targets)
    {
        spell = null!;
        targets = [];
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out int index, out spell) || spell.ClassCode != 22 ||
            MonsterWipeRules.Scope(spell.Id) is null)
            return false;
        if (source.Zone == DuelZone.Hand)
        {
            if (FirstFreeSpellZone() < 0) return false;
        }
        else if (source.Zone == DuelZone.SpellTrap)
        {
            if (!SpellFaceDown[source.Side, index] ||
                equipment.Any(e => e.Owner == source.Side && e.SourceZone == index)) return false;
        }
        else return false;
        targets = MonsterWipeRules.Targets(spell.Id, source.Side, Monsters, MonsterFaceDown);
        return targets.Count != 0;
    }

    /// <summary>Opt-in normal Summon or monster Set; all costs validated before admission.</summary>
    public bool TryQueueNormalPlacement(int index, int zone, bool faceDown, out string message,
        IReadOnlyList<int>? tributeZones = null)
    {
        message = "Choose a legal Normal Summon/Set and distinct tracked tributes.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanSummonFromHand(index) ||
            !TryCaptureCardTarget(ActivePlayer, DuelZone.Hand, index, out var source)) return false;
        int side = ActivePlayer;
        int[] slots = tributeZones?.ToArray() ?? [];
        if (zone is < 0 or >= 5 || slots.Length != NormalTributes(Hand[index]) ||
            slots.Distinct().Count() != slots.Length ||
            (Monsters[side, zone] is not null && !slots.Contains(zone))) return false;
        var targets = new List<CardTarget>();
        foreach (int slot in slots)
        {
            if (slot is < 0 or >= 5 || !CanQueueMonsterRemoval(side, slot) ||
                !TryCaptureCardTarget(side, DuelZone.Monster, slot, out var target)) return false;
            targets.Add(target);
        }
        queue.Enqueue(new PrepareNormalPlacementCommand(side, source, zone, faceDown,
            targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null), ActionOrigin.Player);
        message = "Normal placement admitted.";
        return true;
    }

    /// <summary>Opt-in Set of a non-monster in its usual leftmost/dedicated zone.</summary>
    public bool TryQueueSpellTrapSet(int index, out string message)
    {
        message = "Choose a legal Spell/Trap Set and an idle queue.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanSetFromHand(index) ||
            Hand[index].IsMonster || !TryCaptureCardTarget(ActivePlayer, DuelZone.Hand, index, out var source)) return false;
        bool field = FieldSpellRules.Supports(Hand[index].Id);
        queue.Enqueue(new SetCommand(ActivePlayer, source, field ? DuelZone.FieldSpell : DuelZone.SpellTrap,
            field ? 0 : FirstFreeSpellZone()), ActionOrigin.Player);
        message = "Set admitted.";
        return true;
    }

    /// <summary>Opt-in manual position change or Flip Summon. Battle and
    /// effect-driven reveals are distinct legacy paths, not this command.</summary>
    public bool TryQueueChangePosition(int zone, out string message)
    {
        message = "Choose a legal position change supported by the queue pilot.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanChangePosition(zone) ||
            !TryCaptureCardTarget(ActivePlayer, DuelZone.Monster, zone, out var target)) return false;
        int side = ActivePlayer;
        if (MonsterFaceDown[side, zone] && !CanQueueFlip(Monsters[side, zone]!)) return false;
        DuelCommand command = MonsterFaceDown[side, zone] ? new FlipCommand(side, target)
            : new ChangePositionCommand(side, target, !DefensePosition[side, zone]);
        queue.Enqueue(command, ActionOrigin.Player);
        message = "Position change admitted.";
        return true;
    }

    // These effects either emit explicit commands or only open a legacy
    // selection/prompt. Never run a legacy bulk mutation from this path.
    private bool CanQueueFlip(Card card) => FlipEffectsNegated ||
        FlipEffectRules.Effect(card.Id) is DemoFlipEffect.None or DemoFlipEffect.ReplaceHands or
        DemoFlipEffect.Gain3000LifePoints or DemoFlipEffect.DestroyMonster or
        DemoFlipEffect.ReturnUpToTwo or DemoFlipEffect.ReturnOne or
        DemoFlipEffect.RecoverFromGraveyard or DemoFlipEffect.OptionalDrawOne or
        DemoFlipEffect.DestroyTwoSetSpellTraps or DemoFlipEffect.ReturnTwoSpellTraps or
        DemoFlipEffect.RecruitSameNameFaceDown;

    // Snapshot linked consequences before the producer admits any removals.
    // Each is a normal typed command, in the legacy per-removal order.
    private bool TryPlanRemovalLinks(int player, int slot, out DuelCommand[] links, bool toGraveyard = true)
    {
        links = [];
        if (Monsters[player, slot] is not Card card) return false;
        var result = new List<DuelCommand>();
        foreach (var e in equipment.Where(e => e.TargetPlayer == player && e.TargetZone == slot))
        {
            if (!ReferenceEquals(e.TargetCard, card) ||
                !ReferenceEquals(Spells[e.Owner, e.SourceZone], e.Card) ||
                !TryCaptureCardTarget(e.Owner, DuelZone.SpellTrap, e.SourceZone, out var target))
                return false;
            result.Add(new DestroyCommand(e.Owner, target));
        }
        if (toGraveyard && ImmortalThunderRules.LosesLifeOnFieldToGraveyard(card.Id, MonsterWasFlipped[player, slot]))
            result.Add(new DamageCommand(player, ImmortalThunderRules.GraveyardLifeLoss));
        links = result.ToArray();
        return true;
    }

    private bool CanQueueMonsterRemoval(int player, int slot) =>
        TryPlanRemovalLinks(player, slot, out _);

    private sealed class PrimitiveActionExecutor(DemoDuel duel) : IDuelCommandExecutor
    {
        private sealed class Wipe(int caster, ushort resourceId, CardTarget source, IEnumerable<CardTarget> targets, bool monsterWipe = true)
        {
            public int Caster { get; } = caster;
            public ushort ResourceId { get; } = resourceId;
            public CardTarget Source { get; } = source;
            public bool SourceSpent { get; set; }
            public bool MonsterWipe { get; } = monsterWipe;
            public CardTarget? PrimaryTarget { get; init; }
            public bool PrimaryDestroyed { get; set; }
            public HashSet<CardTarget> Targets { get; } = new(targets);
            public int[] Counts { get; } = [0, 0];
            public int FaceDown { get; set; }
        }

        private Wipe? wipe;
        private bool finishScheduled;
        private PrepareNormalPlacementCommand? placement;
        private readonly HashSet<CardTarget> paidTributes = [];
        private bool resolvingJar;
        private readonly Queue<DuelCommand> reservedDrawSpell = new();
        private CardTarget? drawSpellSource;
        private bool drawSpellSourceSpent;
        private bool resolvingFlipBatch;
        private readonly Dictionary<CardTarget, DuelCommand[]> plannedLinks = new();
        private bool linkedRemovalBatch;
        private PendingEffectDecision? queueDrawDecision;
        private bool resolvingGraveyardRecovery;
        private CardTarget? reservedDeckSearch;
        private int? reservedDeckSearchOwner;
        private bool deckSearchMoved;

        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            // The legacy public input gate also blocks when the queue is busy.
            // The executor itself must use only the legacy rule blockers.
            if (command is ResolveDeckRecruitmentCommand recruit)
                return ResolveDeckRecruitment(recruit, followups);
            if (command is ResolveDeckSearchCommand search)
                return ResolveDeckSearch(search, followups);
            if (command is FinishDeckSearchCommand finishSearch)
                return FinishDeckSearch(finishSearch.Side);
            if (command is ResolveGraveyardRecoveryCommand recovery)
                return ResolveGraveyardRecovery(recovery, followups);
            if (command is FinishGraveyardRecoveryCommand)
                return FinishGraveyardRecovery();
            if (command is ResolveMonsterFlipTargetsCommand flipTargets)
                return ResolveMonsterFlipTargets(flipTargets, followups);
            if (command is FinishFieldRemovalCommand)
                return FinishRemoval();
            if (command is FinishNormalPlacementCommand finish)
                return FinishPlacement(finish);
            if (command is FinishFlipBatchCommand jar) return FinishFlipBatch(jar);
            if (command is RecordFlipDrawDecisionCommand decision) return RecordFlipDrawDecision(decision);
            // An unspent source cancels the entire reserved wipe, including linked consequences.
            if (wipe is { SourceSpent: false } pendingWipe &&
                (command is not SendToGraveyardCommand sendSource || sendSource.Target != pendingWipe.Source))
                return new(CommandOutcome.Skipped, "Wipe source was not spent.");
            bool finishDrawSpell = reservedDrawSpell.TryPeek(out var expectedDraw) && expectedDraw == command;
            if (finishDrawSpell) reservedDrawSpell.Dequeue();
            // Legacy Morphing Jar draws every available card for both players,
            // even if Exodia is found mid-batch. Only this reserved batch may
            // continue its draws after a winner has been set.
            if (duel.LegacyActionsBlocked && !(duel.GameOver &&
                ((resolvingJar && command is DrawCommand) || finishDrawSpell || (command is ShuffleDeckCommand reservedShuffle && reservedDeckSearch is { } selected && selected.Side == reservedShuffle.Side && deckSearchMoved) || wipe is not null || placement is not null || linkedRemovalBatch)))
                return new(CommandOutcome.Rejected, "Duel has ended or a legacy decision is pending.");

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
                case EquipCommand equip:
                    return duel.ApplyQueuedEquipment(equip);
                case ActivateDrawSpellCommand drawSpell:
                    return ActivateDrawSpell(drawSpell, followups);
                case ActivateLifePointSpellCommand lp:
                    return ActivateLifePointSpell(lp, followups);
                case ActivateTargetedSpellTrapCommand targeted:
                    return ActivateTargetedSpellTrap(targeted, followups);
                case ActivateSpellTrapWipeCommand spellTrapWipe:
                    return ActivateSpellTrapWipe(spellTrapWipe.Source, followups);
                case ActivateMonsterWipeCommand monsterWipe:
                    return ActivateMonsterWipe(monsterWipe.Source, followups);
                case ActivateRaigekiCommand activate:
                    return ActivateMonsterWipe(activate.Source, followups, requireRaigeki: true);
                case PrepareNormalPlacementCommand prepare:
                    return PreparePlacement(prepare, followups);
                case TributeCommand tribute:
                    return PayTribute(tribute);
                case SummonCommand summon:
                    return PlaceMonster(summon.Source, summon.Slot, false, followups);
                case SetCommand set when set.Destination == DuelZone.Monster:
                    return PlaceMonster(set.Source, set.Slot, true, followups);
                case SetCommand set:
                    return SetSpellTrap(set);
                case FlipCommand flip:
                    return MovePosition(flip.Target, true, null, followups);
                case ChangePositionCommand position:
                    return MovePosition(position.Target, false, position.Defense, followups);
                case ResolvePositionCommand resolve:
                    return ResolvePosition(resolve, followups);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Hand:
                    return ObserveFixedSpellSpend(send.Target, SendHandToGraveyard(send.Target, discard: false));
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.SpellTrap:
                    return ObserveFixedSpellSpend(send.Target, SendSetFixedSpellToGraveyard(send.Target));
                case DiscardCommand discard:
                    return SendHandToGraveyard(discard.Target, discard: true);
                case SpecialSummonCommand specialSummon:
                    return SpecialSummonFromDeck(specialSummon);
                case MoveToHandCommand move:
                    return MoveSelectedCardToHand(move.Source);
                case ShuffleDeckCommand shuffle:
                    return ShuffleDeck(shuffle.Side);
                case ReturnToHandCommand bounce:
                    return ReturnQueuedMonster(bounce.Target, followups);
                case DestroyCommand destroy when destroy.Target.Zone == DuelZone.Monster:
                    return DestroyMonster(destroy.Target, followups);
                case DestroyCommand destroy when destroy.Target.Zone is DuelZone.SpellTrap or DuelZone.FieldSpell:
                    return DestroyQueuedSpellTrap(destroy.Target);
                default:
                    return new(CommandOutcome.Rejected, "Command not migrated to DemoDuel.");
            }
        }

        private CommandResult ObserveFixedSpellSpend(CardTarget target, CommandResult result)
        {
            if (drawSpellSource == target) drawSpellSourceSpent = result.Outcome == CommandOutcome.Applied;
            if (wipe is { } current && current.Source == target)
                current.SourceSpent = result.Outcome == CommandOutcome.Applied;
            return result;
        }

        private CommandResult ActivateDrawSpell(ActivateDrawSpellCommand activate, IActionCommandSink followups)
        {
            if (reservedDrawSpell.Count != 0 || drawSpellSource is not null ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Draw spell requires isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out _))
                return new(CommandOutcome.Skipped, "Draw spell occurrence gone.");
            if (!duel.CanResolveQueuedDrawSpell(activate.Source, out var effect))
                return new(CommandOutcome.Rejected, "Draw spell no longer legal.");
            // Spending is a separate command; the continuation below is pure
            // with respect to duel state and emits effects only after a successful spend.
            drawSpellSource = activate.Source;
            drawSpellSourceSpent = false;
            queue.Enqueue(new SendToGraveyardCommand(activate.Side, activate.Source));
            queue.ScheduleContinuation(_ =>
            {
                bool spent = drawSpellSourceSpent;
                drawSpellSource = null;
                drawSpellSourceSpent = false;
                if (!spent || duel.GameOver) return new([]);
                // Recheck availability before issuing the reserved batch.
                // No other admitted action can run between these draws.
                if (duel.DeckCount(activate.Side) < effect.Cards)
                    throw new InvalidOperationException("Reserved draw spell deck changed during activation.");
                var batch = new List<DuelCommand>();
                for (int i = 0; i < effect.Cards; i++) batch.Add(new DrawCommand(activate.Side));
                if (effect.OpponentLifeGain > 0)
                    batch.Add(new HealCommand(1 - activate.Side, effect.OpponentLifeGain));
                foreach (var next in batch) reservedDrawSpell.Enqueue(next);
                return new(batch);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateLifePointSpell(ActivateLifePointSpellCommand activate, IActionCommandSink followups)
        {
            if (followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "LP spell requires an isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out var card))
                return new(CommandOutcome.Skipped, "LP spell occurrence gone.");
            if (!duel.CanResolveQueuedLifePointSpell(activate.Source, out var change))
                return new(CommandOutcome.Rejected, "LP spell no longer legal.");
            // Legacy order: spend source, then caster LP, then opposing LP.
            // No mutation in the producer. Each operation has its own gate.
            var batch = new List<DuelCommand> { new SendToGraveyardCommand(activate.Side, activate.Source) };
            AddDelta(batch, activate.Side, change.CasterDelta);
            AddDelta(batch, 1 - activate.Side, change.OpponentDelta);
            queue.EnqueueBatch(batch);
            return new(CommandOutcome.Applied);
        }

        private static void AddDelta(List<DuelCommand> batch, int side, int delta)
        {
            if (delta > 0) batch.Add(new HealCommand(side, delta));
            else if (delta < 0) batch.Add(new DamageCommand(side, -delta));
        }

        private CommandResult SendSetFixedSpellToGraveyard(CardTarget target)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Set spell occurrence gone.");
            if (card.ClassCode != 22 || !(LifePointSpellRules.Supports(card.Id) || DrawSpellRules.Effect(card) is not null ||
                MonsterWipeRules.Scope(card.Id) is not null || SpellTrapWipeRules.Supports(card.Id) ||
                SpellTrapTargetRules.Supports(card.Id)) ||
                !duel.SpellFaceDown[target.Side, slot] ||
                duel.equipment.Any(e => e.Owner == target.Side && e.SourceZone == slot))
                return new(CommandOutcome.Rejected, "Only unlinked set fixed LP/draw/wipe spells are migrated here.");
            duel.ClearSpellTrapSlot(target.Side, slot);
            duel.graves[target.Side].Add(card);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateTargetedSpellTrap(ActivateTargetedSpellTrapCommand activate, IActionCommandSink followups)
        {
            if (wipe is not null || finishScheduled ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Targeted Spell/Trap needs isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out _) ||
                !duel.TryResolveCardTarget(activate.Target, out _, out _))
                return new(CommandOutcome.Skipped, "Source or selected occurrence gone.");
            if (!duel.TryPlanTargetedSpellTrap(activate.Source, activate.Target, out var spell, out var targets))
                return new(CommandOutcome.Rejected, "Targeted Spell/Trap no longer legal.");

            var batch = new List<DuelCommand> { new SendToGraveyardCommand(activate.Side, activate.Source) };
            batch.AddRange(targets.Select(target => new DestroyCommand(target.Side, target)));
            queue.EnqueueBatch(batch);
            wipe = new(activate.Side, spell.Id, activate.Source, targets, monsterWipe: false)
                { PrimaryTarget = activate.Target };
            ScheduleFinish(activate.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateSpellTrapWipe(CardTarget source, IActionCommandSink followups)
        {
            if (wipe is not null || finishScheduled ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Spell/Trap wipe requires an isolated activation.");
            if (!duel.TryResolveCardTarget(source, out _, out _))
                return new(CommandOutcome.Skipped, "Wipe source occurrence gone.");
            if (!duel.TryPlanSpellTrapWipe(source, out var spell, out var targets))
                return new(CommandOutcome.Rejected, "Spell/Trap wipe no longer legal.");

            var batch = new List<DuelCommand> { new SendToGraveyardCommand(source.Side, source) };
            batch.AddRange(targets.Select(target => new DestroyCommand(target.Side, target)));
            queue.EnqueueBatch(batch);
            wipe = new(source.Side, spell.Id, source, targets, monsterWipe: false);
            ScheduleFinish(source.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateMonsterWipe(CardTarget source, IActionCommandSink followups, bool requireRaigeki = false)
        {
            if (wipe is not null || finishScheduled ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Monster wipe requires an isolated activation.");
            int side = source.Side;
            if (!duel.TryResolveCardTarget(source, out _, out _))
                return new(CommandOutcome.Skipped, "Wipe source occurrence gone.");
            if (!duel.CanResolveQueuedMonsterWipe(source, out var spell, out var snapshot) ||
                (requireRaigeki && spell.Id != MonsterWipeRules.Raigeki))
                return new(CommandOutcome.Rejected, "Monster wipe no longer legal.");

            var targets = new List<CardTarget>();
            var batch = new List<DuelCommand> { new SendToGraveyardCommand(side, source) };
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            foreach (var item in snapshot)
            {
                if (!duel.TryPlanRemovalLinks(item.Player, item.Zone, out var links) ||
                    !duel.TryCaptureCardTarget(item.Player, DuelZone.Monster, item.Zone, out var target))
                    return new(CommandOutcome.Rejected, "Untracked occurrence or linked equipment.");
                targets.Add(target);
                plans.Add(target, links);
                batch.Add(new DestroyCommand(target.Side, target));
                batch.AddRange(links);
            }
            // Use the existing sandbox target order, not an invented visual order.
            // Each actual movement receives its own command. No spell/monster
            // movement happens in this activation producer.
            queue.EnqueueBatch(batch);
            foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
            wipe = new(side, spell.Id, source, targets);
            ScheduleFinish(side, followups);
            return new(CommandOutcome.Applied);
        }

        private static CardTarget[] Tributes(PrepareNormalPlacementCommand command) =>
            new CardTarget?[] { command.FirstTribute, command.SecondTribute }
                .OfType<CardTarget>().ToArray();

        private bool CanPlace(int side, Card card) =>
            duel.ActivePlayer == side && duel.Phase == DemoPhase.Main && !duel.summoned &&
            card.IsMonster && !NormalSummonRules.RequiresSpecialSummon(card);

        private CommandResult PreparePlacement(PrepareNormalPlacementCommand prepare, IActionCommandSink followups)
        {
            if (placement is not null || wipe is not null || finishScheduled ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Normal placement requires an isolated batch.");
            if (!duel.TryResolveCardTarget(prepare.Source, out _, out var card))
                return new(CommandOutcome.Skipped, "Hand occurrence gone.");
            var targets = Tributes(prepare);
            if (!CanPlace(prepare.Side, card) || targets.Length != NormalTributes(card) ||
                targets.Distinct().Count() != targets.Length)
                return new(CommandOutcome.Rejected, "Illegal normal placement or tribute count.");
            var slots = new List<int>();
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            var batch = new List<DuelCommand>();
            foreach (var target in targets)
            {
                if (!duel.TryResolveCardTarget(target, out int slot, out _) ||
                    !duel.TryPlanRemovalLinks(target.Side, slot, out var links))
                    return new(CommandOutcome.Rejected, "Tribute no longer eligible.");
                slots.Add(slot);
                plans.Add(target, links);
                batch.Add(new TributeCommand(target.Side, target));
                batch.AddRange(links);
            }
            if (duel.Monsters[prepare.Side, prepare.Slot] is not null && !slots.Contains(prepare.Slot))
                return new(CommandOutcome.Rejected, "Destination occupied.");
            DuelCommand move = prepare.FaceDown
                ? new SetCommand(prepare.Side, prepare.Source, DuelZone.Monster, prepare.Slot)
                : new SummonCommand(prepare.Side, prepare.Source, prepare.Slot);
            queue.EnqueueBatch(batch.Append(move));
            foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
            placement = prepare;
            paidTributes.Clear();
            return new(CommandOutcome.Applied);
        }

        private CommandResult PayTribute(TributeCommand tribute)
        {
            if (placement is not { } current || !Tributes(current).Contains(tribute.Target) ||
                paidTributes.Contains(tribute.Target))
                return new(CommandOutcome.Rejected, "No matching unpaid tribute.");
            if (!duel.TryResolveCardTarget(tribute.Target, out int slot, out _) ||
                !duel.TryPlanRemovalLinks(tribute.Side, slot, out var links) ||
                !plannedLinks.Remove(tribute.Target, out var expected) || !links.SequenceEqual(expected))
            {
                // External mutation during a reserved batch is unsupported.
                // Fault rather than allow a partially paid summon to proceed.
                throw new InvalidOperationException("Reserved tribute changed during placement.");
            }
            duel.deferringFieldRemovalTriggers = true;
            try { duel.RemoveMonster(tribute.Side, slot, isTribute: true, queueLinkedConsequences: true); }
            finally { duel.deferringFieldRemovalTriggers = false; }
            paidTributes.Add(tribute.Target);
            return new(CommandOutcome.Applied);
        }

        private CommandResult PlaceMonster(CardTarget source, int slot, bool faceDown, IActionCommandSink followups)
        {
            if (placement is { } current && (current.Source != source || current.Slot != slot ||
                current.FaceDown != faceDown || !paidTributes.SetEquals(Tributes(current))))
                throw new InvalidOperationException("Reserved normal placement does not match paid costs.");
            if (!duel.TryResolveCardTarget(source, out int index, out var card) ||
                !CanPlace(source.Side, card) || duel.Monsters[source.Side, slot] is not null ||
                NormalTributes(card) != paidTributes.Count)
            {
                if (placement is not null)
                    throw new InvalidOperationException("Reserved normal placement invalidated after admission.");
                return new(CommandOutcome.Rejected, "Illegal placement or unpaid cost.");
            }
            // Direct, unprepared commands may only place level 1–4 monsters.
            // A response/choice is opened on a later pass, after the summon gate.
            duel.CommitNormalPlacement(index, source.Side, slot, faceDown);
            placement = null;
            paidTributes.Clear();
            var target = source with { Zone = DuelZone.Monster };
            followups.ScheduleContinuation(_ => new([new FinishNormalPlacementCommand(source.Side, target, faceDown)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishPlacement(FinishNormalPlacementCommand finish)
        {
            if (!duel.TryResolveCardTarget(finish.Target, out int slot, out _))
                return new(CommandOutcome.Skipped, "Placed occurrence gone.");
            string message = "";
            duel.FinishNormalPlacement(finish.Side, slot, finish.FaceDown, ref message);
            return new(CommandOutcome.Applied, message);
        }

        private CommandResult SetSpellTrap(SetCommand set)
        {
            if (!duel.TryResolveCardTarget(set.Source, out int index, out var card))
                return new(CommandOutcome.Skipped, "Hand occurrence gone.");
            if (duel.ActivePlayer != set.Side || duel.Phase != DemoPhase.Main ||
                card.ClassCode is not (21 or 22 or 23) || DestinyBoardRules.IsMessage(card.Id))
                return new(CommandOutcome.Rejected, "Illegal Spell/Trap Set.");
            bool field = FieldSpellRules.Supports(card.Id);
            if (field)
            {
                if (set.Destination != DuelZone.FieldSpell || duel.FieldSpells[set.Side] is not null)
                    return new(CommandOutcome.Rejected, "Invalid Field Spell destination.");
                duel.FieldSpells[set.Side] = card;
                duel.FieldSpellFaceDown[set.Side] = true;
            }
            else
            {
                if (set.Destination != DuelZone.SpellTrap || duel.Spells[set.Side, set.Slot] is not null)
                    return new(CommandOutcome.Rejected, "Invalid Spell/Trap destination.");
                duel.Spells[set.Side, set.Slot] = card;
                duel.SpellFaceDown[set.Side, set.Slot] = true;
                duel.spellSetTurn[set.Side, set.Slot] = duel.Turn;
            }
            duel.hands[set.Side].RemoveAt(index);
            return new(CommandOutcome.Applied);
        }

        private CommandResult MovePosition(CardTarget target, bool flip, bool? defense, IActionCommandSink followups)
        {
            if (placement is not null || wipe is not null || finishScheduled || resolvingFlipBatch ||
                followups is not ActionQueue { PendingCount: 0 })
                return new(CommandOutcome.Rejected, "Position change requires an isolated action.");
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Monster occurrence gone.");
            int side = target.Side;
            if (side != duel.ActivePlayer || duel.Phase != DemoPhase.Main || duel.changedPosition[side, slot] ||
                duel.MonsterFaceDown[side, slot] != flip ||
                (!flip && defense != !duel.DefensePosition[side, slot]) ||
                (flip && !duel.CanQueueFlip(card)))
                return new(CommandOutcome.Rejected, "Illegal or unmigrated position change.");
            // Verify every hand occurrence before revealing a Morphing Jar.
            if (flip && !duel.FlipEffectsNegated && card.Id == FlipEffectRules.MorphingJar)
                for (int p = 0; p < 2; p++)
                    for (int i = 0; i < duel.hands[p].Count; i++)
                        if (!duel.TryCaptureCardTarget(p, DuelZone.Hand, i, out _))
                            return new(CommandOutcome.Rejected, "Untracked Morphing Jar hand occurrence.");

            var change = duel.CommitManualPosition(side, slot);
            followups.ScheduleContinuation(_ => new([new ResolvePositionCommand(side, target,
                change.WasFaceDown, change.WasInDefense)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolvePosition(ResolvePositionCommand position, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(position.Target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Position occurrence gone.");
            int side = position.Side;
            var change = new DemoMonsterPositionChange(side, slot, card, position.WasFaceDown,
                position.WasInDefense, duel.MonsterFaceDown[side, slot], duel.DefensePosition[side, slot]);
            if (!change.FlipSummoned || duel.FlipEffectsNegated)
            {
                duel.FinishManualPosition(change, resolveFlip: true, out var message);
                return new(CommandOutcome.Applied, message);
            }
            if (!duel.CanQueueFlip(card))
                return new(CommandOutcome.Rejected, "Unmigrated FLIP effect.");
            if (card.Id == FlipEffectRules.Skelengel && duel.decks[side].Count > 0)
            {
                var request = new PendingEffectDecision(++duel.nextEffectDecisionId, side, card,
                    EffectDecisionKind.DrawOneOnFlip, 0, duel.Turn);
                duel.FinishManualPosition(change, resolveFlip: false, out var message);
                if (duel.LegacyActionsBlocked)
                {
                    // Existing summon response has precedence. Keep the legacy
                    // request bridge for that combination until A06 migrates it.
                    duel.queuedEffectDecisions.Enqueue(request);
                    return new(CommandOutcome.Applied, message);
                }
                queueDrawDecision = request;
                followups.ScheduleContinuation(_ => new([], answer =>
                {
                    var selected = answer ?? throw new InvalidOperationException("Missing draw answer.");
                    DuelCommand record = new RecordFlipDrawDecisionCommand(side, request.Id, selected);
                    return new(selected == EffectDecisionAnswer.Accept ? [record, new DrawCommand(side)] : [record]);
                }, request));
                return new(CommandOutcome.Applied);
            }
            if (card.Id == FlipEffectRules.MorphingJar)
            {
                if (followups is not ActionQueue { PendingCount: 0 } queue)
                    return new(CommandOutcome.Rejected, "Morphing Jar needs isolated resolution.");
                var commands = new List<DuelCommand>();
                for (int p = 0; p < 2; p++)
                    for (int i = 0; i < duel.hands[p].Count; i++)
                    {
                        if (!duel.TryCaptureCardTarget(p, DuelZone.Hand, i, out var handTarget))
                            throw new InvalidOperationException("Reserved Morphing Jar hand changed.");
                        commands.Add(new DiscardCommand(p, handTarget));
                    }
                bool out0 = duel.decks[0].Count < 5, out1 = duel.decks[1].Count < 5;
                for (int p = 0; p < 2; p++)
                    for (int i = 0; i < Math.Min(5, duel.decks[p].Count); i++)
                        commands.Add(new DrawCommand(p));
                queue.EnqueueBatch(commands);
                resolvingJar = true;
                resolvingFlipBatch = true;
                followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(side,
                    position.Target, position.WasInDefense, out0, out1)]));
                return new(CommandOutcome.Applied);
            }
            if (card.Id == FlipEffectRules.ImmortalOfThunder)
            {
                followups.Enqueue(new HealCommand(side, ImmortalThunderRules.FlipLifeGain));
                followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(side,
                    position.Target, position.WasInDefense, false, false)]));
                // The common finish stage skips legacy FLIP mutations.
                resolvingFlipBatch = true;
                return new(CommandOutcome.Applied);
            }

            // Choice-only effects can open a legacy request, but cannot
            // execute its answer here. A06/C12 migrate the response path.
            duel.FinishManualPosition(change, resolveFlip: true, out var detail);
            return new(CommandOutcome.Applied, detail);
        }

        private CommandResult RecordFlipDrawDecision(RecordFlipDrawDecisionCommand decision)
        {
            if (queueDrawDecision is not { } request || request.Id != decision.DecisionId || request.Owner != decision.Side)
                return new(CommandOutcome.Rejected, "No matching queued draw decision.");
            queueDrawDecision = null;
            int life = duel.Life[decision.Side];
            duel.effectDecisionHistory.Add(new(request.Id, request.Owner, request.Kind, decision.Answer, duel.Turn, life, life));
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishFlipBatch(FinishFlipBatchCommand finish)
        {
            if (!resolvingFlipBatch) return new(CommandOutcome.Rejected, "No pending FLIP batch.");
            resolvingJar = false;
            resolvingFlipBatch = false;
            if (!duel.GameOver)
            {
                if (finish.DeckOut0 && finish.DeckOut1) duel.IsDraw = true;
                else if (finish.DeckOut0) duel.Winner = 1;
                else if (finish.DeckOut1) duel.Winner = 0;
            }
            if (!duel.TryResolveCardTarget(finish.Target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "FLIP source gone.");
            var change = new DemoMonsterPositionChange(finish.Side, slot, card, true, finish.WasInDefense,
                duel.MonsterFaceDown[finish.Side, slot], duel.DefensePosition[finish.Side, slot]);
            duel.FinishManualPosition(change, resolveFlip: false, out var detail);
            return new(CommandOutcome.Applied, detail);
        }

        private CommandResult SendHandToGraveyard(CardTarget target, bool discard)
        {
            if (!duel.TryResolveCardTarget(target, out int index, out var card))
                return new(CommandOutcome.Skipped, "Hand occurrence is no longer present.");
            duel.hands[target.Side].RemoveAt(index);
            duel.graves[target.Side].Add(card);
            // Match legacy activation (no discard notification for a spent Spell).
            if (discard) duel.HandTransferred?.Invoke(new(target.Side, index, card, FromDeck: false));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveDeckSearch(ResolveDeckSearchCommand answer, IActionCommandSink followups)
        {
            if (reservedDeckSearchOwner is not null || followups is not ActionQueue { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedDeckSearch(answer.Request, answer.Target))
                return new(CommandOutcome.Rejected, "Deck search request or target changed.");

            ReserveDeckTransfer(answer.Side, answer.Target, new MoveToHandCommand(answer.Side, answer.Target), queue);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveDeckRecruitment(ResolveDeckRecruitmentCommand answer, IActionCommandSink followups)
        {
            if (reservedDeckSearchOwner is not null || followups is not ActionQueue { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedDeckRecruitment(answer.Request, answer.Target, answer.Slot))
                return new(CommandOutcome.Rejected, "Recruitment request, target or slot changed.");
            DuelCommand? movement = answer.Target is { } target && answer.Slot is int slot
                ? new SpecialSummonCommand(answer.Side, target, slot) : null;
            ReserveDeckTransfer(answer.Side, answer.Target, movement, queue);
            return new(CommandOutcome.Applied);
        }

        private void ReserveDeckTransfer(int side, CardTarget? target, DuelCommand? movement, ActionQueue queue)
        {
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
            reservedDeckSearchOwner = side;
            reservedDeckSearch = target;
            deckSearchMoved = false;
            if (movement is not null) queue.Enqueue(movement);
            // A declined or stale transfer consumes no RNG. Both search
            // and recruitment finish only after the movement/shuffle gates.
            queue.ScheduleContinuation(_ => new(deckSearchMoved
                ? [new ShuffleDeckCommand(side), new FinishDeckSearchCommand(side)]
                : [new FinishDeckSearchCommand(side)]));
        }

        private CommandResult SpecialSummonFromDeck(SpecialSummonCommand summon)
        {
            if (!duel.TryResolveCardTarget(summon.Source, out int index, out var card))
                return new(CommandOutcome.Skipped, "Deck occurrence gone.");
            if (!card.IsMonster || duel.Monsters[summon.Side, summon.Slot] is not null)
                return new(CommandOutcome.Rejected, "Special Summon requires a monster and a free slot.");
            Card[] remaining = duel.decks[summon.Side].Where((_, i) => i != index).ToArray();
            duel.decks[summon.Side].Clear();
            foreach (var other in remaining) duel.decks[summon.Side].Enqueue(other);
            // P164 is always face-up attack. This is a movement primitive,
            // not a generic card legality or summon-response resolver.
            duel.PlaceSelectedMonster(summon.Side, summon.Slot, card, false);
            if (reservedDeckSearch == summon.Source) deckSearchMoved = true;
            return new(CommandOutcome.Applied);
        }

        private CommandResult ShuffleDeck(int side)
        {
            Card[] cards = duel.decks[side].ToArray();
            duel.effectRandom.Shuffle(cards);
            duel.decks[side].Clear();
            foreach (var card in cards) duel.decks[side].Enqueue(card);
            duel.DeckShuffled?.Invoke(side);
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishDeckSearch(int side)
        {
            if (reservedDeckSearchOwner != side)
                return new(CommandOutcome.Rejected, "No matching deck search to finalize.");
            reservedDeckSearchOwner = null;
            reservedDeckSearch = null;
            deckSearchMoved = false;
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveGraveyardRecovery(ResolveGraveyardRecoveryCommand answer, IActionCommandSink followups)
        {
            if (resolvingGraveyardRecovery || followups is not ActionQueue { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedGraveyardRecovery(answer.Request, answer.Target, out var movement))
                return new(CommandOutcome.Rejected, "Graveyard recovery request or target changed.");

            // No movement when consuming the answer; the selected occurrence
            // is moved only by the next command after the presentation gate.
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
            resolvingGraveyardRecovery = true;
            if (movement is not null) queue.Enqueue(movement);
            followups.ScheduleContinuation(_ => new([new FinishGraveyardRecoveryCommand(answer.Side)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult MoveSelectedCardToHand(CardTarget source)
        {
            if (!duel.TryResolveCardTarget(source, out int index, out var card))
                return new(CommandOutcome.Skipped, "Selected occurrence gone.");
            if (source.Zone == DuelZone.Graveyard)
                duel.graves[source.Side].RemoveAt(index);
            else if (source.Zone == DuelZone.Deck)
            {
                Card[] remaining = duel.decks[source.Side].Where((_, i) => i != index).ToArray();
                duel.decks[source.Side].Clear();
                foreach (var other in remaining) duel.decks[source.Side].Enqueue(other);
            }
            else return new(CommandOutcome.Rejected, "Unsupported source zone.");
            duel.hands[source.Side].Add(card);
            if (reservedDeckSearch == source) deckSearchMoved = true;
            // Recovery is not a draw; do not emit the deck-draw event.
            duel.CheckExodia(source.Side);
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishGraveyardRecovery()
        {
            if (!resolvingGraveyardRecovery)
                return new(CommandOutcome.Rejected, "No recovery to finalize.");
            resolvingGraveyardRecovery = false;
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveMonsterFlipTargets(ResolveMonsterFlipTargetsCommand answer, IActionCommandSink followups)
        {
            if (followups is not ActionQueue { PendingCount: 0 } queue ||
                wipe is not null || finishScheduled || !duel.CanAdmitQueuedMonsterFlipAnswer())
                return new(CommandOutcome.Rejected, "No isolated supported FLIP selection.");
            CardTarget[] targets = new CardTarget?[] { answer.First, answer.Second }.OfType<CardTarget>().ToArray();
            if (!duel.TryPlanQueuedMonsterFlipAnswer(answer.Source, targets, out var batch, out var plans))
                return new(CommandOutcome.Rejected, "FLIP source or target choice no longer valid.");

            // Clear only this pending interaction inside its command, not at
            // admission. Never invoke legacy FinishDeferredBattle here.
            duel.PendingFlipTarget = null;
            duel.flipSelections.Clear();
            queue.EnqueueBatch(batch);
            foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
            linkedRemovalBatch = true;
            ScheduleFinish(answer.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ReturnQueuedMonster(CardTarget target, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out _))
            {
                if (plannedLinks.ContainsKey(target))
                    throw new InvalidOperationException("Reserved return occurrence changed.");
                return new(CommandOutcome.Skipped, "Monster occurrence gone.");
            }
            if (duel.IsMarkedForBattleDestruction(target.Side, slot))
                return new(CommandOutcome.Rejected, "Battle-destroyed monsters cannot return.");
            if (!duel.TryPlanRemovalLinks(target.Side, slot, out var links, toGraveyard: false))
                return new(CommandOutcome.Rejected, "Untracked linked equipment.");
            if (plannedLinks.Remove(target, out var expected))
            {
                if (!links.SequenceEqual(expected))
                    throw new InvalidOperationException("Reserved return links changed.");
            }
            else if (links.Length > 0)
            {
                if (followups is not ActionQueue { PendingCount: 0 } queue || finishScheduled)
                    return new(CommandOutcome.Rejected, "Linked return requires isolated action.");
                queue.EnqueueBatch(links);
                linkedRemovalBatch = true;
            }

            // Reuse the single-occurrence legacy movement (including Fusion
            // Deck replacement). Linked equipment is handled separately.
            duel.ReturnMonsterToHand(target.Side, slot, removeEquipment: false);
            ScheduleFinish(target.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult DestroyMonster(CardTarget target, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out _))
            {
                if (plannedLinks.ContainsKey(target))
                    throw new InvalidOperationException("Reserved removal occurrence changed.");
                return new(CommandOutcome.Skipped, "Monster occurrence is no longer present.");
            }
            if (!duel.TryPlanRemovalLinks(target.Side, slot, out var links))
                return new(CommandOutcome.Rejected, "Untracked linked equipment.");
            if (plannedLinks.Remove(target, out var expected))
            {
                if (!links.SequenceEqual(expected))
                    throw new InvalidOperationException("Reserved removal links changed.");
            }
            else if (links.Length > 0)
            {
                // A standalone destroy must reserve the whole linked sequence;
                // never splice it ahead of an already admitted FIFO tail.
                if (followups is not ActionQueue { PendingCount: 0 } queue || finishScheduled)
                    return new(CommandOutcome.Rejected, "Linked destruction requires an isolated action.");
                queue.EnqueueBatch(links);
                linkedRemovalBatch = true;
            }
            bool hidden = duel.MonsterFaceDown[target.Side, slot];
            duel.deferringFieldRemovalTriggers = true;
            try { duel.RemoveMonster(target.Side, slot, queueLinkedConsequences: true); }
            finally { duel.deferringFieldRemovalTriggers = false; }
            if (wipe is { } current && current.Targets.Remove(target))
            {
                current.Counts[target.Side]++;
                if (hidden) current.FaceDown++;
            }
            ScheduleFinish(target.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult DestroyQueuedSpellTrap(CardTarget target)
        {
            bool reserved = wipe is { MonsterWipe: false, SourceSpent: true } current &&
                current.Targets.Contains(target);
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Spell/Trap occurrence gone.");

            if (reserved && wipe is { PrimaryTarget: { } primary } targeted)
            {
                int zone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
                if (target == primary)
                {
                    if (!SpellTrapTargetRules.Accepts(targeted.ResourceId, card,
                        duel.IsSpellTrapFaceDown(target.Side, zone)))
                        return new(CommandOutcome.Skipped, "Selected target no longer satisfies the effect.");
                }
                else if (!targeted.PrimaryDestroyed ||
                    duel.IsSpellTrapFaceDown(target.Side, zone) || !IsDestinyBoardPiece(card))
                    return new(CommandOutcome.Skipped, "No qualifying Destiny Board cascade.");
            }

            // Outside a reserved wipe retain the previous narrow linked-equipment
            // executor: standalone S/T destruction needs Destiny Board handling.
            if (!reserved && (target.Zone != DuelZone.SpellTrap ||
                !duel.equipment.Any(e => e.Owner == target.Side && e.SourceZone == slot &&
                    ReferenceEquals(e.Card, card))))
                return new(CommandOutcome.Rejected, "Only reserved S/T or linked equipment destruction is migrated.");
            if (!duel.SpellTrapRemovalLinkMatches(target.Side, target.Zone, slot, card))
                return new(CommandOutcome.Rejected, "Spell/Trap link changed.");

            // This low-level helper moves a single occurrence and removes its
            // equipment link. It does not run the bulk Destiny Board resolver.
            duel.DestroySpellTrapCard(target.Side,
                target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot);
            if (reserved)
            {
                wipe!.Targets.Remove(target);
                if (wipe.PrimaryTarget == target) wipe.PrimaryDestroyed = true;
            }
            return new(CommandOutcome.Applied);
        }

        private void ScheduleFinish(int side, IActionCommandSink followups)
        {
            if (finishScheduled) return;
            finishScheduled = true;
            // This continuation proposes a command, without mutating state.
            // It runs only after the FIFO and its last presentation gate drain.
            followups.ScheduleContinuation(_ => new([new FinishFieldRemovalCommand(side)]));
        }

        private CommandResult FinishRemoval()
        {
            if (!finishScheduled) return new(CommandOutcome.Rejected, "No field removal to finalize.");
            finishScheduled = false;
            linkedRemovalBatch = false;
            plannedLinks.Clear();
            if (wipe is { } current)
            {
                if (current.MonsterWipe && current.SourceSpent)
                    duel.LastMonsterWipe = new(current.ResourceId, current.Caster,
                        current.Counts[0], current.Counts[1], current.FaceDown);
                wipe = null;
            }
            // Legacy choice bridge only; selection mutations are still A06/C12.
            // No search or optional prompt opens between the destruction commands.
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }
    }
}
