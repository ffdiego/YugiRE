namespace Joey.Core;

public sealed partial class DemoDuel
{
    private ActionQueue? primitiveActionQueue;

    /// <summary>
    /// One queue per duel. Effect-level operations, not a general legal input API.
    /// Existing App/CPU entrypoints remain legacy; do not mix their decisions
    /// with admitted queue work. Direct array writes are fixture-only.
    /// </summary>
    public ActionQueue CreatePrimitiveActionQueue(IPresentationGate? gate = null, bool allowLabEdits = false)
    {
        if (primitiveActionQueue is not null)
            throw new InvalidOperationException("A duel already owns an action queue.");
        return primitiveActionQueue = new(new PrimitiveActionExecutor(this), gate,
            () => (!LegacyActionsBlocked || admittingQueuedMonsterFlipAnswer && CanAdmitQueuedMonsterFlipAnswer() ||
                admittingQueuedFlipSpellTrapPair && CanAdmitQueuedFlipSpellTrapPair() ||
                admittingQueuedGraveyardRecovery && CanAdmitQueuedGraveyardRecovery() ||
                admittingQueuedGraveyardRevival && CanAdmitQueuedGraveyardRevival() ||
                admittingQueuedDeckSearch && CanAdmitQueuedDeckSearch() ||
                admittingQueuedDeckRecruitment && CanAdmitQueuedDeckRecruitment()) &&
                (PendingEndDiscards == 0 || admittingQueuedEndDiscard && CanAdmitQueuedEndDiscard()), allowLabEdits);
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

    /// <summary>Opt-in activation of supported non-targeting monster wipe spells.</summary>
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
            !MonsterWipeRules.SupportsQueued(spell.Id))
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
        targets = spell.Id == MonsterWipeRules.EternalRest
            ? MonsterWipeRules.EquippedTargets(source.Side, Monsters, MonsterFaceDown,
                (player, slot, monster) => equipment.Any(e => e.TargetPlayer == player &&
                    e.TargetZone == slot && ReferenceEquals(e.TargetCard, monster) &&
                    ReferenceEquals(Spells[e.Owner, e.SourceZone], e.Card) &&
                    !SpellFaceDown[e.Owner, e.SourceZone]))
            : MonsterWipeRules.Targets(spell.Id, source.Side, Monsters, MonsterFaceDown);
        return targets.Count != 0;
    }

    /// <summary>Dispatch to the single C12 producer for each supported activation family.</summary>
    public bool TryQueueActivation(int index, bool fromSet, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal, migrated spell activation.";
        if (primitiveActionQueue is not { IsBusy: false } || ActionsBlocked || Phase != DemoPhase.Main)
            return false;
        DuelZone zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
        if (!TryCaptureCardTarget(ActivePlayer, zone, index, out var source) ||
            !TryResolveCardTarget(source, out _, out var card) || card.ClassCode != 22)
            return false;
        if (DrawSpellRules.Effect(card) is not null)
            return TryQueueDrawSpell(zone, index, out message, origin);
        if (DeckSearchSpellRules.Filter(card.Id) is not null)
            return TryQueueDeckSearchSpell(zone, index, out message, origin);
        if (LifePointSpellRules.SupportsQueued(card.Id))
            return TryQueueLifePointSpell(zone, index, out message, origin);
        if (MonsterWipeRules.SupportsQueued(card.Id))
            return TryQueueMonsterWipe(zone, index, out message, origin);
        if (SpellTrapWipeRules.SupportsQueued(card.Id))
            return TryQueueSpellTrapWipe(zone, index, out message, origin);
        return false;
    }

    /// <summary>Opt-in normal Summon or monster Set; all costs validated before admission.</summary>
    public bool TryQueueNormalPlacement(int index, int zone, bool faceDown, out string message,
        IReadOnlyList<int>? tributeZones = null, ActionOrigin origin = ActionOrigin.Player)
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
            targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null), origin);
        message = "Normal placement admitted.";
        return true;
    }

    /// <summary>Opt-in Set of a non-monster in its usual leftmost/dedicated zone.</summary>
    public bool TryQueueSpellTrapSet(int index, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal Spell/Trap Set and an idle queue.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanSetFromHand(index) ||
            Hand[index].IsMonster || !TryCaptureCardTarget(ActivePlayer, DuelZone.Hand, index, out var source)) return false;
        bool field = FieldSpellRules.Supports(Hand[index].Id);
        queue.Enqueue(new SetCommand(ActivePlayer, source, field ? DuelZone.FieldSpell : DuelZone.SpellTrap,
            field ? 0 : FirstFreeSpellZone()), origin);
        message = "Set admitted.";
        return true;
    }

    /// <summary>Opt-in manual position change or Flip Summon. Battle and
    /// effect-driven reveals are distinct legacy paths, not this command.</summary>
    public bool TryQueueChangePosition(int zone, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal position change supported by the queue pilot.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanChangePosition(zone) ||
            !TryCaptureCardTarget(ActivePlayer, DuelZone.Monster, zone, out var target)) return false;
        int side = ActivePlayer;
        if (MonsterFaceDown[side, zone] && !CanQueueFlip(Monsters[side, zone]!)) return false;
        DuelCommand command = MonsterFaceDown[side, zone] ? new FlipCommand(side, target)
            : new ChangePositionCommand(side, target, !DefensePosition[side, zone]);
        queue.Enqueue(command, origin);
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
        DemoFlipEffect.RecruitSameNameFaceDown or DemoFlipEffect.MillOpponentDeck or
        DemoFlipEffect.DrawAndDiscardOpponentSpells or DemoFlipEffect.DestroyOpponentFaceUpLevelFour or
        DemoFlipEffect.BanishThreeDeckBurn800 or DemoFlipEffect.BanishTwoRandomHandBurn800 or
        DemoFlipEffect.ShuffleAndExcavate;

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
            public bool PrimaryReturned { get; set; }
            public HashSet<CardTarget>? FlipPair { get; init; }
            public HashSet<int> BoardReturned { get; } = [];
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
        private CardTarget? searchSpellSource;
        private bool searchSpellSourceSpent;
        private bool resolvingFlipBatch;
        private sealed class FlipDeckFlow(ushort effect, int controller, int requested, IEnumerable<CardTarget> expected)
        {
            public ushort Effect { get; } = effect;
            public int Controller { get; } = controller;
            public int Side => 1 - Controller;
            public int Requested { get; } = requested;
            public bool IsMill => Effect == FlipEffectRules.NeedleWorm;
            public Queue<CardTarget> Expected { get; } = new(expected);
            public List<CardTarget> NewSpells { get; } = [];
            public int Moved { get; set; }
            public int Discarded { get; set; }
        }
        private FlipDeckFlow? flipDeckFlow;
        private sealed class FlipBanishBatch(ushort effect, int owner, FlipBanishCostSource source, int required, IEnumerable<CardTarget> expected)
        {
            public ushort Effect { get; } = effect;
            public int Owner { get; } = owner;
            public FlipBanishCostSource Source { get; } = source;
            public int Required { get; } = required;
            public Queue<CardTarget> Expected { get; } = new(expected);
            public List<Card> Removed { get; } = [];
            public bool DamageApplied { get; set; }
        }
        private FlipBanishBatch? flipBanish;
        private sealed class Jar2Batch(DemoMonsterPositionChange change)
        {
            public DemoMonsterPositionChange Change { get; } = change;
            public Dictionary<CardTarget, DuelCommand[]> Returns { get; } = new();
            public int[] Returned { get; } = [0, 0];
            public int[] Found { get; } = [0, 0];
            public int[] Summoned { get; } = [0, 0];
            public int[] Sent { get; } = [0, 0];
        }
        private Jar2Batch? jar2;
        private PrepareGraveyardBanishSummonCommand? banishSummon;
        private readonly Queue<CardTarget> banishSummonCosts = new();
        private int banishSummonPaid;
        private readonly Dictionary<CardTarget, DuelCommand[]> plannedLinks = new();
        private bool linkedRemovalBatch;
        private PendingEffectDecision? queueDrawDecision;
        private bool resolvingGraveyardRecovery;
        private readonly HashSet<CardTarget> reservedDeckSearch = [];
        private int? reservedDeckSearchOwner;
        private bool deckSearchMoved;

        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            // The legacy public input gate also blocks when the queue is busy.
            // The executor itself must use only the legacy rule blockers.
            if (command is FinishGraveyardBanishSummonCommand finishBanishSummon)
                return FinishGraveyardBanishSummon(finishBanishSummon.Side);
            if (command is ResolveSameNameRecruitmentCommand sameName)
                return ResolveSameNameRecruitment(sameName, followups);
            if (command is ResolveDeckRecruitmentCommand recruit)
                return ResolveDeckRecruitment(recruit, followups);
            if (command is ResolveDeckSearchCommand search)
                return ResolveDeckSearch(search, followups);
            if (command is FinishDeckSearchCommand finishSearch)
                return FinishDeckSearch(finishSearch.Side);
            if (command is ResolveGraveyardRevivalCommand revival)
                return ResolveGraveyardRevival(revival, followups);
            if (command is ResolveGraveyardRecoveryCommand recovery)
                return ResolveGraveyardRecovery(recovery, followups);
            if (command is FinishGraveyardRecoveryCommand)
                return FinishGraveyardRecovery();
            if (command is ResolveFlipSpellTrapPairCommand pair)
                return ResolveFlipSpellTrapPair(pair, followups);
            if (command is ResolveMonsterFlipTargetsCommand flipTargets)
                return ResolveMonsterFlipTargets(flipTargets, followups);
            // Only a reserved battle snapshot may finish after lethal LP.
            if (command is FinishBattleRemovalCommand battleFinish)
                return duel.FinishQueuedBattleRemoval(battleFinish);
            if (command is FinishFieldRemovalCommand)
                return FinishRemoval();
            if (command is FinishNormalPlacementCommand finish)
                return FinishPlacement(finish);
            if (command is FinishFlipBatchCommand jar) return FinishFlipBatch(jar);
            if (command is RecordFlipDrawDecisionCommand decision) return RecordFlipDrawDecision(decision, followups);
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
                ((resolvingJar && command is DrawCommand) || finishDrawSpell || (command is ShuffleDeckCommand reservedShuffle && reservedDeckSearchOwner == reservedShuffle.Side && deckSearchMoved) || wipe is not null || placement is not null || linkedRemovalBatch)))
                return new(CommandOutcome.Rejected, "Duel has ended or a legacy decision is pending.");

            if (duel.HasQueuedEndDiscard && command is not EndTurnDiscardCommand)
                return new(CommandOutcome.Rejected, "Resolve the queued end-turn discard first.");

            switch (command)
            {
                case LabSpawnCommand spawn: return duel.ExecuteLabSpawn(spawn);
                case LabMoveCommand move: return duel.ExecuteLabMove(move);
                case DeclareDirectAttackCommand attack:
                    return duel.ExecuteQueuedDirectAttack(attack, followups);
                case DeclareTargetedAttackCommand attack:
                    return duel.ExecuteQueuedTargetedAttack(attack, followups);
                case SnapshotBattleRemovalCommand snapshot:
                    return duel.SnapshotQueuedBattleRemoval(snapshot);
                case ChangeBattlePhaseCommand phase:
                    return duel.ExecuteQueuedBattlePhaseChange(phase);
                case EndTurnCommand endTurn:
                    return duel.ExecuteQueuedEndTurn(endTurn, followups);
                case EndTurnDiscardCommand endDiscard:
                    if (!duel.CanExecuteQueuedEndDiscard(endDiscard))
                        return new(CommandOutcome.Rejected, "End-turn discard is no longer legal.");
                    var endResult = SendHandToGraveyard(endDiscard.Target, discard: true);
                    if (endResult.Outcome == CommandOutcome.Applied && duel.PendingEndDiscards == 0)
                        followups.Enqueue(duel.PlanQueuedEndResolution(endDiscard.Side, endDiscard.ExpectedTurn));
                    return endResult;
                case PlaceEndPhaseMessageCommand endMessage:
                    return duel.ExecuteQueuedEndMessage(endMessage, followups);
                case AdvanceTurnCommand advanceTurn:
                    return duel.ExecuteQueuedTurnAdvance(advanceTurn);
                case TurnDrawCommand turnDraw:
                    return duel.ExecuteQueuedTurnDraw(turnDraw, followups);
                case ResolveStandbyRevivalCommand standby:
                    return duel.ExecuteQueuedStandbyRevival(standby, followups);
                case DrawCommand draw:
                    if (flipDeckFlow is { IsMill: false } flow && flow.Side == draw.Side)
                        return DrawForShadowScout(flow);
                    if (duel.decks[draw.Side].Count == 0)
                    {
                        duel.Winner = 1 - draw.Side;
                        return new(CommandOutcome.Applied, "Empty deck: player loses (sandbox policy).");
                    }
                    duel.DrawToHand(draw.Side);
                    return new(CommandOutcome.Applied);
                case DamageCommand damage:
                    duel.ChangeLife(damage.Side, -damage.Amount);
                    if (flipBanish is { } paid && paid.Removed.Count == paid.Required &&
                        damage.Side == 1 - paid.Owner && damage.Amount == FlipBanishBurnRules.BurnDamage)
                        paid.DamageApplied = true;
                    return new(CommandOutcome.Applied);
                case HealCommand heal:
                    duel.ChangeLife(heal.Side, heal.Amount);
                    return new(CommandOutcome.Applied);
                case EquipCommand equip:
                    return duel.ApplyQueuedEquipment(equip);
                case ActivateDrawSpellCommand drawSpell:
                    return ActivateDrawSpell(drawSpell, followups);
                case ActivateDeckSearchSpellCommand searchSpell:
                    return ActivateDeckSearchSpell(searchSpell, followups);
                case OpenDeckSearchSpellCommand openSearch:
                    return OpenDeckSearchSpell(openSearch);
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
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Deck:
                    return SendDeckCardToGraveyard(send.Target);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Hand:
                    return ObserveFixedSpellSpend(send.Target, SendHandToGraveyard(send.Target, discard: false));
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.SpellTrap:
                    return ObserveFixedSpellSpend(send.Target, SendSetFixedSpellToGraveyard(send.Target));
                case PrepareGraveyardBanishSummonCommand prepareBanish:
                    return PrepareGraveyardBanishSummon(prepareBanish, followups);
                case BanishCommand banish:
                    return BanishFromSource(banish.Target);
                case DiscardCommand discard:
                    var discarded = SendHandToGraveyard(discard.Target, discard: true);
                    if (discarded.Outcome == CommandOutcome.Applied && flipDeckFlow is { IsMill: false } scout &&
                        scout.NewSpells.Contains(discard.Target)) scout.Discarded++;
                    return discarded;
                case SpecialSummonCommand specialSummon:
                    return SpecialSummonFromSource(specialSummon);
                case MoveToHandCommand move:
                    return MoveSelectedCardToHand(move.Source);
                case ShuffleDeckCommand shuffle:
                    return ShuffleDeck(shuffle.Side);
                case ReturnToDeckCommand returnDeck:
                    return ReturnMonsterToDeck(returnDeck.Target);
                case ReturnToHandCommand bounce:
                    return bounce.Target.Zone == DuelZone.Monster ? ReturnQueuedMonster(bounce.Target, followups) : ReturnQueuedSpellTrap(bounce.Target);
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
            if (searchSpellSource == target) searchSpellSourceSpent = result.Outcome == CommandOutcome.Applied;
            if (wipe is { } current && current.Source == target)
                current.SourceSpent = result.Outcome == CommandOutcome.Applied;
            return result;
        }

        private CommandResult ActivateDeckSearchSpell(ActivateDeckSearchSpellCommand activate, IActionCommandSink followups)
        {
            if (searchSpellSource is not null || reservedDeckSearchOwner is not null ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Search spell requires isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out var card))
                return new(CommandOutcome.Skipped, "Search spell occurrence gone.");
            if (!duel.CanResolveDeckSearchSpell(activate.Source, out _))
                return new(CommandOutcome.Rejected, "Search spell no longer legal.");
            searchSpellSource = activate.Source;
            searchSpellSourceSpent = false;
            queue.EnqueueBatch([new SendToGraveyardCommand(activate.Side, activate.Source),
                new OpenDeckSearchSpellCommand(activate.Side, activate.Source, card.Id)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult OpenDeckSearchSpell(OpenDeckSearchSpellCommand open)
        {
            if (searchSpellSource != open.Source)
                return new(CommandOutcome.Rejected, "No matching reserved search spell.");
            bool spent = searchSpellSourceSpent;
            searchSpellSource = null;
            searchSpellSourceSpent = false;
            if (!spent) return new(CommandOutcome.Skipped, "Search spell was not spent.");
            if (DeckSearchSpellRules.Filter(open.ResourceId) is not { } filter)
                return new(CommandOutcome.Rejected, "Unknown search spell.");
            var candidates = CardSelectionRules.Candidates(duel.decks[open.Side].ToArray(), filter);
            if (candidates.Count == 0)
                return new(CommandOutcome.Skipped, "No eligible cards remain.");
            duel.selectedBatchIndices.Clear();
            duel.PendingCardSelection = new(open.Side, CardSelectionSource.Deck,
                CardSelectionDestination.Hand, filter, candidates, 1, 1, open.ResourceId);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateDrawSpell(ActivateDrawSpellCommand activate, IActionCommandSink followups)
        {
            if (reservedDrawSpell.Count != 0 || drawSpellSource is not null ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
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
                var context = new EffectContext();
                context.Draw(activate.Side, effect.Cards);
                context.ChangeLifePoints(1 - activate.Side, effect.OpponentLifeGain);
                foreach (var next in context.Commands) reservedDrawSpell.Enqueue(next);
                return new(context.Commands);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateLifePointSpell(ActivateLifePointSpellCommand activate, IActionCommandSink followups)
        {
            if (followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "LP spell requires an isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out var card))
                return new(CommandOutcome.Skipped, "LP spell occurrence gone.");
            if (!duel.CanResolveQueuedLifePointSpell(activate.Source, out var change))
                return new(CommandOutcome.Rejected, "LP spell no longer legal.");
            // Legacy order: spend source, then caster LP, then opposing LP.
            // No mutation in the producer. Each operation has its own gate.
            var context = new EffectContext();
            context.SendToGraveyard(activate.Source);
            context.ChangeLifePoints(activate.Side, change.CasterDelta);
            context.ChangeLifePoints(1 - activate.Side, change.OpponentDelta);
            queue.EnqueueBatch(context.Commands);
            return new(CommandOutcome.Applied);
        }

        private CommandResult SendSetFixedSpellToGraveyard(CardTarget target)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Set spell occurrence gone.");
            if (card.ClassCode != 22 || !(LifePointSpellRules.SupportsQueued(card.Id) || DrawSpellRules.Effect(card) is not null ||
                MonsterWipeRules.SupportsQueued(card.Id) || DeckSearchSpellRules.Filter(card.Id) is not null || SpellTrapWipeRules.SupportsQueued(card.Id) ||
                SpellTrapTargetRules.SupportsQueued(card.Id)) ||
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
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Targeted Spell/Trap needs isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out _) ||
                !duel.TryResolveCardTarget(activate.Target, out _, out _))
                return new(CommandOutcome.Skipped, "Source or selected occurrence gone.");
            if (!duel.TryPlanTargetedSpellTrap(activate.Source, activate.Target, out var spell, out var targets))
                return new(CommandOutcome.Rejected, "Targeted Spell/Trap no longer legal.");

            var context = new EffectContext();
            context.SendToGraveyard(activate.Source);
            if (SpellTrapTargetRules.ReturnsToHand(spell.Id))
            {
                queue.EnqueueBatch([.. context.Commands,
                    new ReturnToHandCommand(activate.Target.Side, activate.Target),
                    .. targets.Where(t => t != activate.Target).Select(t => (DuelCommand)new DestroyCommand(t.Side, t))]);
            }
            else
            {
                foreach (var target in targets) context.Destroy(target);
                queue.EnqueueBatch(context.Commands);
            }
            wipe = new(activate.Side, spell.Id, activate.Source, targets, monsterWipe: false)
                { PrimaryTarget = activate.Target };
            ScheduleFinish(activate.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateSpellTrapWipe(CardTarget source, IActionCommandSink followups)
        {
            if (wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Spell/Trap wipe requires an isolated activation.");
            if (!duel.TryResolveCardTarget(source, out _, out _))
                return new(CommandOutcome.Skipped, "Wipe source occurrence gone.");
            if (!duel.TryPlanSpellTrapWipe(source, out var spell, out var targets))
                return new(CommandOutcome.Rejected, "Spell/Trap wipe no longer legal.");

            var context = new EffectContext();
            context.SendToGraveyard(source);
            if (SpellTrapWipeRules.ReturnsToHand(spell.Id))
                queue.EnqueueBatch([.. context.Commands, .. targets.Select(t => (DuelCommand)new ReturnToHandCommand(t.Side, t))]);
            else
            {
                foreach (var target in targets) context.Destroy(target);
                queue.EnqueueBatch(context.Commands);
            }
            wipe = new(source.Side, spell.Id, source, targets, monsterWipe: false);
            ScheduleFinish(source.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateMonsterWipe(CardTarget source, IActionCommandSink followups, bool requireRaigeki = false)
        {
            if (wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Monster wipe requires an isolated activation.");
            int side = source.Side;
            if (!duel.TryResolveCardTarget(source, out _, out _))
                return new(CommandOutcome.Skipped, "Wipe source occurrence gone.");
            if (!duel.CanResolveQueuedMonsterWipe(source, out var spell, out var snapshot) ||
                (requireRaigeki && spell.Id != MonsterWipeRules.Raigeki))
                return new(CommandOutcome.Rejected, "Monster wipe no longer legal.");

            var targets = new List<CardTarget>();
            var context = new EffectContext();
            context.SendToGraveyard(source);
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            foreach (var item in snapshot)
            {
                if (!duel.TryPlanRemovalLinks(item.Player, item.Zone, out var links) ||
                    !duel.TryCaptureCardTarget(item.Player, DuelZone.Monster, item.Zone, out var target))
                    return new(CommandOutcome.Rejected, "Untracked occurrence or linked equipment.");
                targets.Add(target);
                plans.Add(target, links);
                context.Destroy(target, links);
            }
            // Use the existing sandbox target order, not an invented visual order.
            // Each actual movement receives its own command. No spell/monster
            // movement happens in this activation producer.
            queue.EnqueueBatch(context.Commands);
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
                followups is not IActionBatchSink { PendingCount: 0 } queue)
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
            var observation = new PlacementObservation(source, card.Id, index, DuelZone.Monster,
                slot, faceDown, faceDown, paidTributes.Count);
            duel.CommitNormalPlacement(index, source.Side, slot, faceDown);
            placement = null;
            paidTributes.Clear();
            var target = source with { Zone = DuelZone.Monster };
            followups.ScheduleContinuation(_ => new([new FinishNormalPlacementCommand(source.Side, target, faceDown)]));
            return new(CommandOutcome.Applied) { Placement = observation };
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
            return new(CommandOutcome.Applied) { Placement = new(set.Source, card.Id, index,
                set.Destination, set.Slot, true, false, 0) };
        }

        private CommandResult MovePosition(CardTarget target, bool flip, bool? defense, IActionCommandSink followups)
        {
            if (placement is not null || wipe is not null || finishScheduled || resolvingFlipBatch ||
                followups is not IActionBatchSink { PendingCount: 0 })
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

            if (flip && !duel.FlipEffectsNegated &&
                card.Id is FlipEffectRules.NeedleWorm or FlipEffectRules.HirosShadowScout)
            {
                int requested = card.Id == FlipEffectRules.NeedleWorm ? 5 : 3;
                for (int i = 0; i < Math.Min(requested, duel.decks[1 - side].Count); i++)
                    if (!duel.TryCaptureCardTarget(1 - side, DuelZone.Deck, i, out _))
                        return new(CommandOutcome.Rejected, "Untracked deck-flow occurrence.");
            }

            if (flip && !duel.FlipEffectsNegated && FlipBanishBurnRules.Cost(card.Id) is { } cost)
            {
                DuelZone zone = cost == FlipBanishCostSource.Deck ? DuelZone.Deck : DuelZone.Hand;
                int available = zone == DuelZone.Deck ? duel.decks[side].Count : duel.hands[side].Count;
                int required = FlipBanishBurnRules.RequiredCount(card.Id);
                if (available >= required)
                    for (int i = 0; i < (zone == DuelZone.Deck ? required : available); i++)
                        if (!duel.TryCaptureCardTarget(side, zone, i, out _))
                            return new(CommandOutcome.Rejected, "Untracked banishment cost occurrence.");
            }

            if (flip && !duel.FlipEffectsNegated && card.Id == FlipEffectRules.MorphingJar2)
            {
                for (int p = 0; p < 2; p++)
                {
                    for (int z = 0; z < 5; z++)
                        if (duel.Monsters[p,z] is not null && !duel.IsMarkedForBattleDestruction(p,z) &&
                            (!duel.TryCaptureCardTarget(p,DuelZone.Monster,z,out _) ||
                             !duel.TryPlanRemovalLinks(p,z,out _,toGraveyard:false)))
                            return new(CommandOutcome.Rejected,"Untracked Jar #2 return.");
                    for (int i = 0; i < duel.decks[p].Count; i++)
                        if (!duel.TryCaptureCardTarget(p,DuelZone.Deck,i,out _))
                            return new(CommandOutcome.Rejected,"Untracked Jar #2 deck.");
                }
            }

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
                    return new([new RecordFlipDrawDecisionCommand(side, request.Id, selected)]);
                }, request));
                return new(CommandOutcome.Applied);
            }
            if (card.Id is FlipEffectRules.NeedleWorm or FlipEffectRules.HirosShadowScout)
                return ResolveFlipDeckFlow(position, card, followups);
            if (card.Id == FlipEffectRules.FourStarredLadybugOfDoom)
                return ResolveLadybug(position, followups);
            if (card.Id == FlipEffectRules.MorphingJar2)
                return ResolveJar2(position, change, followups);
            if (FlipBanishBurnRules.Cost(card.Id) is not null)
                return ResolveFlipBanish(position, card, followups);
            if (card.Id == FlipEffectRules.MorphingJar)
            {
                if (followups is not IActionBatchSink { PendingCount: 0 } queue)
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

        private CommandResult ResolveJar2(ResolvePositionCommand position, DemoMonsterPositionChange change, IActionCommandSink followups)
        {
            if (jar2 is not null || resolvingFlipBatch || wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Morphing Jar #2 requires isolated resolution.");
            var batch = new Jar2Batch(change);
            List<DuelCommand>[] returns = [[], []];
            for (int p = 0; p < 2; p++)
            for (int z = 0; z < 5; z++)
            {
                if (duel.Monsters[p,z] is null || duel.IsMarkedForBattleDestruction(p,z)) continue;
                if (!duel.TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) ||
                    !duel.TryPlanRemovalLinks(p,z,out var links,toGraveyard:false))
                    return new(CommandOutcome.Rejected, "Untracked Jar #2 target or equipment.");
                batch.Returns.Add(target, links);
                returns[p].Add(new ReturnToDeckCommand(p,target));
                returns[p].AddRange(links);
            }
            jar2 = batch;
            resolvingFlipBatch = true;
            duel.LastMorphingJar2 = [];
            queue.EnqueueBatch(returns[0]);
            // Same order as the legacy sandbox: return side 0, shuffle if
            // needed, return side 1, shuffle if needed, then excavate.
            queue.ScheduleContinuation(_ => new(
                batch.Returned[0] > 0 ? [new ShuffleDeckCommand(0), .. returns[1]] : returns[1],
                _ => new(batch.Returned[1] > 0 ? [new ShuffleDeckCommand(1)] : [],
                    _ => PlanJar2Excavation(batch, position))));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ReturnMonsterToDeck(CardTarget target)
        {
            if (jar2 is not { } batch || !batch.Returns.TryGetValue(target,out var expected))
                return new(CommandOutcome.Rejected, "Only reserved Morphing Jar #2 return is migrated.");
            if (!duel.TryResolveCardTarget(target,out int slot,out var card) ||
                duel.IsMarkedForBattleDestruction(target.Side,slot) ||
                !duel.TryPlanRemovalLinks(target.Side,slot,out var links,toGraveyard:false) ||
                !links.SequenceEqual(expected))
                throw new InvalidOperationException("Reserved Jar #2 return or equipment changed.");
            batch.Returns.Remove(target);
            duel.ClearMonsterSlot(target.Side,slot,removeEquipment:false);
            if (FusionRules.IsFusionMonster(card)) duel.fusionDecks[target.Side].Add(card);
            else
            {
                duel.decks[target.Side].Enqueue(card);
                batch.Returned[target.Side]++;
            }
            return new(CommandOutcome.Applied);
        }

        private static bool Jar2CanSummon(Card card) =>
            card.IsMonster && card.Level is >= 1 and <= 4 && !NormalSummonRules.RequiresSpecialSummon(card);

        private ActionContinuationStep PlanJar2Excavation(Jar2Batch batch, ResolvePositionCommand position)
        {
            // Pure plan over the shuffled snapshots. Count every monster,
            // even those that cannot be placed. No drawing or temporary hand.
            var commands = new List<DuelCommand>();
            for (int p = 0; p < 2; p++)
            {
                int found = 0;
                var free = new Queue<int>(Enumerable.Range(0,5).Where(z => duel.Monsters[p,z] is null));
                var deck = duel.decks[p].ToArray();
                for (int i = 0; i < deck.Length && found < batch.Returned[p]; i++)
                {
                    Card card = deck[i];
                    if (!duel.TryCaptureCardTarget(p,DuelZone.Deck,i,out var target))
                        throw new InvalidOperationException("Untracked Jar #2 excavation occurrence.");
                    if (card.IsMonster) found++;
                    if (Jar2CanSummon(card) && free.TryDequeue(out int slot))
                        commands.Add(new SpecialSummonCommand(p,target,slot,true));
                    else commands.Add(new SendToGraveyardCommand(p,target));
                }
            }
            commands.Add(new FinishFlipBatchCommand(position.Side,position.Target,position.WasInDefense,false,false));
            return new(commands);
        }

        private CommandResult ResolveFlipBanish(ResolvePositionCommand position, Card card, IActionCommandSink followups)
        {
            if (resolvingFlipBatch || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                FlipBanishBurnRules.Cost(card.Id) is not { } cost)
                return new(CommandOutcome.Rejected, "Banish burn requires isolated resolution.");
            int side = position.Side, required = FlipBanishBurnRules.RequiredCount(card.Id);
            DuelZone zone = cost == FlipBanishCostSource.Deck ? DuelZone.Deck : DuelZone.Hand;
            int available = zone == DuelZone.Deck ? duel.decks[side].Count : duel.hands[side].Count;
            var picks = new List<CardTarget>();
            if (available >= required)
            {
                // Capture before sampling. The seeded RNG is shared with the
                // existing legacy helper and is never consumed on shortfall.
                int count = zone == DuelZone.Deck ? required : available;
                var candidates = new CardTarget[count];
                for (int i = 0; i < count; i++)
                    if (!duel.TryCaptureCardTarget(side, zone, i, out candidates[i]))
                        return new(CommandOutcome.Rejected, "Untracked banishment cost occurrence.");
                if (zone == DuelZone.Deck) picks.AddRange(candidates);
                else foreach (int i in FlipBanishBurnRules.SampleHandIndices(available, required, duel.effectRandom))
                    picks.Add(candidates[i]);
            }
            flipBanish = new(card.Id, side, cost, required, picks);
            resolvingFlipBatch = true;
            queue.EnqueueBatch(picks.Select(t => new BanishCommand(side, t)));
            queue.ScheduleContinuation(_ =>
            {
                var finish = new FinishFlipBatchCommand(side, position.Target, position.WasInDefense, false, false);
                // Damage is admitted only once every required occurrence has
                // actually been banished and its presentation has completed.
                return new(flipBanish!.Removed.Count == required
                    ? [new DamageCommand(1 - side, FlipBanishBurnRules.BurnDamage), finish]
                    : [finish]);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult PrepareGraveyardBanishSummon(PrepareGraveyardBanishSummonCommand prepare, IActionCommandSink followups)
        {
            var cost = new CardTarget?[] { prepare.First, prepare.Second, prepare.Third }.OfType<CardTarget>().ToArray();
            if (banishSummon is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanGraveyardBanishSummon(prepare.Source, prepare.Slot, cost))
                return new(CommandOutcome.Rejected, "Inherent summon source, cost or destination changed.");
            banishSummon = prepare;
            banishSummonCosts.Clear();
            foreach (var target in cost) banishSummonCosts.Enqueue(target);
            banishSummonPaid = 0;
            queue.EnqueueBatch(cost.Select(t => new BanishCommand(prepare.Side,t)));
            queue.ScheduleContinuation(_ =>
            {
                var finish = new FinishGraveyardBanishSummonCommand(prepare.Side);
                return new(banishSummonPaid == cost.Length
                    ? [new SpecialSummonCommand(prepare.Side,prepare.Source,prepare.Slot,false,prepare.Defense), finish]
                    : [finish]);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishGraveyardBanishSummon(int side)
        {
            if (banishSummon is not { } current || current.Side != side)
                return new(CommandOutcome.Rejected, "No reserved inherent summon.");
            banishSummon = null;
            banishSummonCosts.Clear();
            banishSummonPaid = 0;
            return new(CommandOutcome.Applied);
        }

        private CommandResult BanishFromSource(CardTarget target)
        {
            if (!duel.TryResolveCardTarget(target, out int index, out var card))
                return new(CommandOutcome.Skipped, "Banishment occurrence gone.");
            if (target.Zone == DuelZone.Deck && index != 0)
                return new(CommandOutcome.Skipped, "Only the current top occurrence may be banished from Deck.");
            if (target.Zone is not (DuelZone.Deck or DuelZone.Hand or DuelZone.Graveyard))
                return new(CommandOutcome.Rejected, "Unsupported banishment zone.");
            if (flipBanish is { } batch && (!batch.Expected.TryPeek(out var expected) || expected != target))
                return new(CommandOutcome.Rejected, "Unexpected reserved banishment occurrence.");
            if (banishSummon is not null && (!banishSummonCosts.TryPeek(out var expectedCost) || expectedCost != target))
                return new(CommandOutcome.Rejected, "Unexpected inherent summon cost occurrence.");
            if (target.Zone == DuelZone.Deck) duel.decks[target.Side].Dequeue();
            else if (target.Zone == DuelZone.Graveyard) duel.graves[target.Side].RemoveAt(index);
            else duel.hands[target.Side].RemoveAt(index);
            duel.banished[target.Side].Add(card);
            if (flipBanish is { } current)
            {
                current.Expected.Dequeue();
                current.Removed.Add(card);
            }
            if (banishSummon is not null)
            {
                banishSummonCosts.Dequeue();
                banishSummonPaid++;
            }
            // Neither discard, draw nor GY arrival: no corresponding event.
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveLadybug(ResolvePositionCommand position, IActionCommandSink followups)
        {
            if (wipe is not null || finishScheduled || resolvingFlipBatch ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Ladybug requires isolated resolution.");
            var targets = new List<CardTarget>();
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            var batch = new List<DuelCommand>();
            foreach (var item in MonsterWipeRules.LevelFourOpponentTargets(position.Side, duel.Monsters, duel.MonsterFaceDown))
            {
                if (duel.IsMarkedForBattleDestruction(item.Player, item.Zone)) continue;
                if (!duel.TryCaptureCardTarget(item.Player, DuelZone.Monster, item.Zone, out var target) ||
                    !duel.TryPlanRemovalLinks(item.Player, item.Zone, out var links))
                    return new(CommandOutcome.Rejected, "Untracked Ladybug target or equipment.");
                targets.Add(target);
                plans.Add(target, links);
                batch.Add(new DestroyCommand(target.Side, target));
                batch.AddRange(links);
            }
            queue.EnqueueBatch(batch);
            foreach (var plan in plans) plannedLinks.Add(plan.Key, plan.Value);
            wipe = new(position.Side, FlipEffectRules.FourStarredLadybugOfDoom, position.Target, targets) { SourceSpent = true };
            resolvingFlipBatch = true;
            ScheduleFinish(position.Side, followups);
            followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(position.Side,
                position.Target, position.WasInDefense, false, false)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveFlipSpellTrapPair(ResolveFlipSpellTrapPairCommand answer, IActionCommandSink followups)
        {
            CardTarget[] targets = new CardTarget?[] { answer.First, answer.Second }.OfType<CardTarget>().ToArray();
            if (wipe is not null || finishScheduled || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanFlipSpellTrapPair(answer.Request, targets, out var source, out var batch, out var cascade))
                return new(CommandOutcome.Rejected, "FLIP pair request or occurrence changed.");
            duel.PendingFlipSpellTrapTargets = null;
            duel.flipSpellTrapSelections.Clear();
            queue.EnqueueBatch(batch);
            wipe = new(answer.Side, answer.Request.Source.Id, source, targets.Concat(cascade), monsterWipe: false)
                { SourceSpent = true, FlipPair = new(targets) };
            ScheduleFinish(answer.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ReturnQueuedSpellTrap(CardTarget target)
        {
            if (wipe is not { MonsterWipe: false, SourceSpent: true } current ||
                !current.Targets.Contains(target))
                return new(CommandOutcome.Rejected, "Only reserved Spell/Trap return is migrated.");
            bool massReturn = SpellTrapWipeRules.ReturnsToHand(current.ResourceId);
            bool singleReturn = SpellTrapTargetRules.ReturnsToHand(current.ResourceId) &&
                current.PrimaryTarget == target && SpellTrapTargetRules.AcceptsSide(current.ResourceId, current.Caster, target.Side);
            if (!massReturn && !singleReturn && (current.ResourceId != FlipEffectRules.TornadoBird ||
                current.FlipPair is not { } pair || !pair.Contains(target)))
                return new(CommandOutcome.Rejected, "Not a reserved return target.");
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Spell/Trap occurrence gone.");
            int zone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
            bool eligible = massReturn || singleReturn ? card.ClassCode is 21 or 22 :
                FlipSpellTrapSelectionRules.IsEligible(current.ResourceId, card, duel.IsSpellTrapFaceDown(target.Side, zone));
            if (!eligible || !duel.SpellTrapRemovalLinkMatches(target.Side, target.Zone, slot, card))
                return new(CommandOutcome.Rejected, "Spell/Trap return no longer eligible.");
            if (card.Id == DestinyBoardRules.BoardResourceId && !duel.IsSpellTrapFaceDown(target.Side, zone) &&
                DestinyBoardRules.HasActiveBoard(target.Side, duel.Spells, duel.SpellFaceDown))
                current.BoardReturned.Add(target.Side);
            duel.ReturnSpellTrapCardToOwnerHand(target.Side, zone);
            if (singleReturn) current.PrimaryReturned = true;
            current.Targets.Remove(target);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveFlipDeckFlow(ResolvePositionCommand position, Card card, IActionCommandSink followups)
        {
            if (resolvingFlipBatch || followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Deck flow needs isolated resolution.");
            bool mill = card.Id == FlipEffectRules.NeedleWorm;
            int side = 1 - position.Side, requested = mill ? 5 : 3;
            var expected = new List<CardTarget>();
            for (int i = 0; i < Math.Min(requested, duel.decks[side].Count); i++)
            {
                if (!duel.TryCaptureCardTarget(side, DuelZone.Deck, i, out var target))
                    return new(CommandOutcome.Rejected, "Untracked deck occurrence.");
                expected.Add(target);
            }
            flipDeckFlow = new(card.Id, position.Side, requested, expected);
            resolvingFlipBatch = true;
            queue.EnqueueBatch(expected.Select(target => mill
                ? (DuelCommand)new SendToGraveyardCommand(side, target)
                : new DrawCommand(side)));
            queue.ScheduleContinuation(_ =>
            {
                var finish = new FinishFlipBatchCommand(position.Side, position.Target, position.WasInDefense, false, false);
                // Read-only continuation: only successfully drawn Spell copies
                // participate; never select matching resource IDs in old hand.
                return new([.. flipDeckFlow!.NewSpells.Select(t => (DuelCommand)new DiscardCommand(side, t)), finish]);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult DrawForShadowScout(FlipDeckFlow flow)
        {
            if (!flow.Expected.TryDequeue(out var expected) ||
                !duel.TryCaptureCardTarget(flow.Side, DuelZone.Deck, 0, out var top) || top != expected)
                return new(CommandOutcome.Skipped, "Reserved draw occurrence changed.");
            var card = duel.decks[flow.Side].Dequeue();
            int index = duel.hands[flow.Side].Count;
            duel.hands[flow.Side].Add(card);
            flow.Moved++;
            if (card.ClassCode == 22)
            {
                if (!duel.TryCaptureCardTarget(flow.Side, DuelZone.Hand, index, out var selected))
                    throw new InvalidOperationException("Newly drawn occurrence is not tracked.");
                flow.NewSpells.Add(selected);
            }
            // Emit the logical draw for presentation, but defer Exodia
            // and shortfall until all draws and discards have finished.
            duel.HandTransferred?.Invoke(new(flow.Side, index, card, FromDeck: true));
            return new(CommandOutcome.Applied);
        }

        private CommandResult SendDeckCardToGraveyard(CardTarget target)
        {
            // This primitive deliberately sends only the top occurrence.
            // Arbitrary Deck search-to-GY remains a separate producer.
            if (!duel.TryCaptureCardTarget(target.Side, DuelZone.Deck, 0, out var top) || top != target)
            {
                if (jar2 is not null) throw new InvalidOperationException("Reserved Jar #2 excavation occurrence changed.");
                return new(CommandOutcome.Skipped, "Expected deck top occurrence gone.");
            }
            if (flipDeckFlow is { IsMill: true } mill &&
                (!mill.Expected.TryDequeue(out var expected) || expected != target))
                return new(CommandOutcome.Rejected, "Unexpected reserved mill occurrence.");
            Card sent = duel.decks[target.Side].Dequeue();
            duel.graves[target.Side].Add(sent);
            if (jar2 is { } excavation)
            {
                excavation.Sent[target.Side]++;
                if (sent.IsMonster) excavation.Found[target.Side]++;
            }
            if (flipDeckFlow is { IsMill: true } current && current.Side == target.Side) current.Moved++;
            return new(CommandOutcome.Applied);
        }

        private CommandResult RecordFlipDrawDecision(RecordFlipDrawDecisionCommand decision, IActionCommandSink followups)
        {
            if (queueDrawDecision is not { } request || request.Id != decision.DecisionId || request.Owner != decision.Side)
                return new(CommandOutcome.Rejected, "No matching queued draw decision.");

            queueDrawDecision = null;
            if (duel.FlipEffectsNegated)
                return new(CommandOutcome.Skipped, "Optional draw effect is now negated.");
            if (duel.LegacyActionsBlocked || request.CreatedTurn != duel.Turn ||
                (decision.Answer == EffectDecisionAnswer.Accept && duel.DeckCount(decision.Side) == 0))
                return new(CommandOutcome.Rejected, "Optional draw is no longer legal.");

            if (decision.Answer == EffectDecisionAnswer.Accept)
                followups.Enqueue(new DrawCommand(decision.Side));
            int life = duel.Life[decision.Side];
            duel.effectDecisionHistory.Add(new(request.Id, request.Owner, request.Kind, decision.Answer, duel.Turn, life, life));
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishFlipBatch(FinishFlipBatchCommand finish)
        {
            if (!resolvingFlipBatch) return new(CommandOutcome.Rejected, "No pending FLIP batch.");
            resolvingJar = false;
            resolvingFlipBatch = false;
            if (jar2 is { } excavation)
            {
                jar2 = null;
                duel.LastMorphingJar2 = Array.AsReadOnly(Enumerable.Range(0,2).Select(p => new DemoExcavationResult(
                    p,excavation.Returned[p],excavation.Summoned[p]+excavation.Sent[p],excavation.Found[p],
                    excavation.Summoned[p],excavation.Sent[p],excavation.Found[p]<excavation.Returned[p])).ToArray());
                // The original FLIP source may now be in another zone or
                // face-down again. Preserve the original position event.
                duel.FinishManualPosition(excavation.Change,resolveFlip:false,out var jar2Detail);
                return new(CommandOutcome.Applied,jar2Detail);
            }
            if (flipBanish is { } burn)
            {
                flipBanish = null;
                duel.LastFlipBanishBurn = new(burn.Effect, burn.Owner, burn.Source, burn.Required,
                    Array.AsReadOnly(burn.Removed.ToArray()), burn.DamageApplied ? FlipBanishBurnRules.BurnDamage : 0, burn.DamageApplied);
            }
            if (flipDeckFlow is { } flow)
            {
                flipDeckFlow = null;
                int sent = flow.IsMill ? flow.Moved : flow.Discarded;
                bool shortfall = !flow.IsMill && flow.Moved < flow.Requested;
                duel.LastDeckFlow = new(flow.Effect, flow.Controller, flow.Side, flow.Requested,
                    flow.Moved, flow.IsMill ? 0 : flow.Moved - flow.Discarded, sent, flow.Discarded, shortfall);
                if (!flow.IsMill)
                {
                    duel.CheckExodia(flow.Side);
                    if (shortfall && duel.Winner is null) duel.Winner = flow.Controller;
                }
            }
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
            if (reservedDeckSearchOwner is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedDeckSearch(answer.Request, answer.Target))
                return new(CommandOutcome.Rejected, "Deck search request or target changed.");

            ReserveDeckTransfer(answer.Side, answer.Target is { } chosen
                ? [new MoveToHandCommand(answer.Side, chosen)] : [], queue);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveDeckRecruitment(ResolveDeckRecruitmentCommand answer, IActionCommandSink followups)
        {
            if (reservedDeckSearchOwner is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedDeckRecruitment(answer.Request, answer.Target, answer.Slot))
                return new(CommandOutcome.Rejected, "Recruitment request, target or slot changed.");
            DuelCommand? movement = answer.Target is { } target && answer.Slot is int slot
                ? new SpecialSummonCommand(answer.Side, target, slot) : null;
            ReserveDeckTransfer(answer.Side, movement is null ? [] : [movement], queue);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveSameNameRecruitment(ResolveSameNameRecruitmentCommand answer, IActionCommandSink followups)
        {
            CardTarget[] targets = new CardTarget?[] { answer.First, answer.Second }.OfType<CardTarget>().ToArray();
            if (reservedDeckSearchOwner is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanSameNameRecruitment(answer.Request, targets, answer.SingleSlot, out var movements))
                return new(CommandOutcome.Rejected, "Same-name recruitment request, copies or slots changed.");
            ReserveDeckTransfer(answer.Side, movements, queue);
            return new(CommandOutcome.Applied);
        }

        private void ReserveDeckTransfer(int side, IReadOnlyList<DuelCommand> movements, IActionBatchSink queue)
        {
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
            reservedDeckSearchOwner = side;
            reservedDeckSearch.Clear();
            foreach (var movement in movements)
                reservedDeckSearch.Add(movement switch
                {
                    MoveToHandCommand toHand => toHand.Source,
                    SpecialSummonCommand summon => summon.Source,
                    _ => throw new InvalidOperationException("Unsupported deck transfer.")
                });
            deckSearchMoved = false;
            if (movements.Count != 0) queue.EnqueueBatch(movements);
            // Exactly one shuffle after the complete reserved batch if at
            // least one transfer succeeded; none on decline or total failure.
            queue.ScheduleContinuation(_ => new(deckSearchMoved
                ? [new ShuffleDeckCommand(side), new FinishDeckSearchCommand(side)]
                : [new FinishDeckSearchCommand(side)]));
        }

        private CommandResult SpecialSummonFromSource(SpecialSummonCommand summon)
        {
            if (!duel.TryResolveCardTarget(summon.Source, out int index, out var card))
            {
                if (jar2 is not null) throw new InvalidOperationException("Reserved Jar #2 summon occurrence changed.");
                return new(CommandOutcome.Skipped, "Special Summon source occurrence gone.");
            }
            if (jar2 is not null && (summon.Source.Zone != DuelZone.Deck || index != 0 ||
                !Jar2CanSummon(card) || !summon.FaceDownDefense || duel.Monsters[summon.Side,summon.Slot] is not null))
                throw new InvalidOperationException("Reserved Jar #2 summon conditions changed.");
            if (!card.IsMonster || duel.Monsters[summon.Side, summon.Slot] is not null)
                return new(CommandOutcome.Rejected, "Special Summon requires a monster and a free slot.");
            if (summon.Source.Zone == DuelZone.Hand)
            {
                if (banishSummon is not { } paidSummon || paidSummon.Source != summon.Source ||
                    paidSummon.Slot != summon.Slot || paidSummon.Defense != summon.FaceUpDefense || summon.FaceDownDefense ||
                    banishSummonPaid != GraveyardBanishSummonRules.QueuedRequiredCount(card) || banishSummonCosts.Count != 0)
                    return new(CommandOutcome.Rejected, "Hand Special Summon requires the reserved fully paid procedure.");
                duel.hands[summon.Side].RemoveAt(index);
            }
            else if (summon.Source.Zone == DuelZone.Graveyard)
                duel.graves[summon.Side].RemoveAt(index);
            else if (summon.Source.Zone == DuelZone.Deck)
            {
                Card[] remaining = duel.decks[summon.Side].Where((_, i) => i != index).ToArray();
                duel.decks[summon.Side].Clear();
                foreach (var other in remaining) duel.decks[summon.Side].Enqueue(other);
            }
            else return new(CommandOutcome.Rejected, "Unsupported Special Summon source.");

            // Position is supplied by the admitted effect. No generic summon response is introduced here.
            duel.PlaceSelectedMonster(summon.Side, summon.Slot, card, summon.FaceDownDefense, summon.FaceUpDefense);
            if (jar2 is { } excavation)
            {
                excavation.Found[summon.Side]++;
                excavation.Summoned[summon.Side]++;
            }
            if (reservedDeckSearch.Contains(summon.Source)) deckSearchMoved = true;
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
            reservedDeckSearch.Clear();
            deckSearchMoved = false;
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveGraveyardRevival(ResolveGraveyardRevivalCommand answer, IActionCommandSink followups)
        {
            if (resolvingGraveyardRecovery || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanGraveyardRevival(answer.Request, answer.Target, answer.Slot, answer.FaceDownDefense, out var movement))
                return new(CommandOutcome.Rejected, "Revival request, occurrence or destination changed.");
            ReserveGraveyardTransfer(answer.Side, movement, queue);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveGraveyardRecovery(ResolveGraveyardRecoveryCommand answer, IActionCommandSink followups)
        {
            if (resolvingGraveyardRecovery || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanQueuedGraveyardRecovery(answer.Request, answer.Target, out var movement))
                return new(CommandOutcome.Rejected, "Graveyard recovery request or target changed.");

            ReserveGraveyardTransfer(answer.Side, movement, queue);
            return new(CommandOutcome.Applied);
        }

        private void ReserveGraveyardTransfer(int side, DuelCommand? movement, IActionBatchSink queue)
        {
            // Consume the request separately from moving the occurrence.
            duel.PendingCardSelection = null;
            duel.selectedBatchIndices.Clear();
            resolvingGraveyardRecovery = true;
            if (movement is not null) queue.Enqueue(movement);
            queue.ScheduleContinuation(_ => new([new FinishGraveyardRecoveryCommand(side)]));
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
            if (reservedDeckSearch.Contains(source)) deckSearchMoved = true;
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
            if (followups is not IActionBatchSink { PendingCount: 0 } queue ||
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
                if (followups is not IActionBatchSink { PendingCount: 0 } queue || finishScheduled)
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
                if (followups is not IActionBatchSink { PendingCount: 0 } queue || finishScheduled)
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

            if (reserved && wipe is { FlipPair: { } pair } flipPair)
            {
                int zone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
                bool hidden = duel.IsSpellTrapFaceDown(target.Side, zone);
                if (pair.Contains(target))
                {
                    if (flipPair.ResourceId != FlipEffectRules.Greenkappa ||
                        !FlipSpellTrapSelectionRules.IsEligible(flipPair.ResourceId, card, hidden))
                        return new(CommandOutcome.Skipped, "Selected Set target no longer eligible.");
                }
                else if (!flipPair.BoardReturned.Contains(target.Side) || hidden || !IsDestinyBoardPiece(card))
                    return new(CommandOutcome.Skipped, "No active Board returned for cascade.");
            }

            if (reserved && wipe is { PrimaryTarget: { } primary } targeted)
            {
                int zone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
                if (target == primary)
                {
                    if (SpellTrapTargetRules.ReturnsToHand(targeted.ResourceId) ||
                        !SpellTrapTargetRules.Accepts(targeted.ResourceId, card,
                        duel.IsSpellTrapFaceDown(target.Side, zone)))
                        return new(CommandOutcome.Skipped, "Selected target no longer satisfies the effect.");
                }
                else if (!(targeted.PrimaryDestroyed || targeted.PrimaryReturned) ||
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
