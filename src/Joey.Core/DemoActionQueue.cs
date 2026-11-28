namespace Joey.Core;

public sealed partial class DemoDuel
{
    private readonly ActionQueue primitiveActionQueue;
    public event Action<LabCoinResult>? LabCoinResultReported;
    public event Action<LabDiceResult>? LabDiceResultReported;
    public event Action<DemoBanishment>? CardBanished;
    internal bool ActionQueueBusy => primitiveActionQueue.IsBusy;

    /// <summary>One queue, owned by the duel from construction.
    /// The default is immediate presentation without laboratory privileges.
    /// Obtaining this reference seals its configuration; the host drives Tick.</summary>
    public ActionQueue Actions
    {
        get
        {
            primitiveActionQueue.SealConfiguration();
            return primitiveActionQueue;
        }
    }

    /// <summary>Explicit host configuration before first use. Returns the
    /// same owned queue; it cannot replace a gate or elevate privileges later.</summary>
    public ActionQueue ConfigureActionQueue(IPresentationGate? gate = null, bool allowLabEdits = false)
    {
        primitiveActionQueue.ConfigureBeforeUse(gate, allowLabEdits);
        return primitiveActionQueue;
    }

    // Historical caller compatibility: configure, never create another queue.
    internal ActionQueue CreatePrimitiveActionQueue(IPresentationGate? gate = null, bool allowLabEdits = false) =>
        ConfigureActionQueue(gate, allowLabEdits);

    private ActionQueue BuildActionQueue() =>
        new(new PrimitiveActionExecutor(this), externalAdmission:
            () => (!LegacyActionsBlocked || admittingQueuedMonsterFlipAnswer && CanAdmitQueuedMonsterFlipAnswer() ||
                admittingQueuedFlipSpellTrapPair && CanAdmitQueuedFlipSpellTrapPair() ||
                admittingQueuedGraveyardRecovery && CanAdmitQueuedGraveyardRecovery() ||
                admittingQueuedGraveyardRevival && CanAdmitQueuedGraveyardRevival() ||
                admittingHandManipulationAnswer && CanAdmitHandManipulationAnswer() ||
                admittingQueuedDeckSearch && CanAdmitQueuedDeckSearch() ||
                admittingQueuedDeckRecruitment && CanAdmitQueuedDeckRecruitment() ||
                admittingQueuedFusion && CanAdmitQueuedFusion() ||
                admittingQueuedSummonResponse && CanAdmitQueuedSummonResponse() ||
                admittingQueuedAttackResponse && CanAdmitQueuedAttackResponse()) &&
                (PendingEndDiscards == 0 || admittingQueuedEndDiscard && CanAdmitQueuedEndDiscard()));

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
            !CanAdmitQueuedMonsterWipe(source))
            return false;
        queue.Enqueue(new ActivateMonsterWipeCommand(ActivePlayer, source), origin);
        message = "Monster wipe admitted.";
        return true;
    }

    private bool CanAdmitQueuedMonsterWipe(CardTarget source) =>
        CanResolveQueuedMonsterWipe(source, out _, out var targets) &&
        targets.All(t => CanQueueMonsterRemoval(t.Player, t.Zone) &&
            TryCaptureCardTarget(t.Player, DuelZone.Monster, t.Zone, out _));

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

    /// <summary>Existing fixed-effect executors; no target or cost selection at admission.</summary>
    public static bool SupportsQueuedFixedSpell(Card card) => FieldSpellRules.Supports(card.Id) ||
        SupportsQueuedContinuousSpell(card) || card.ClassCode == 21 &&
        (DrawSpellRules.SupportsQueued(card) || LifePointSpellRules.SupportsQueued(card)) ||
        card.ClassCode == 22 &&
        (DrawSpellRules.SupportsQueued(card) || DeckSearchSpellRules.Filter(card.Id) is not null ||
         LifePointSpellRules.SupportsQueued(card.Id) || MonsterWipeRules.SupportsQueued(card.Id) ||
         SpellTrapWipeRules.SupportsQueued(card.Id) || IsRevivalSpell(card) || IsHandManipulationSpell(card));

    /// <summary>Read-only UI/CPU admission query. Reuses the same source and target
    /// validators as the producer; does not reserve, spend or enqueue a card.</summary>
    public bool CanQueueFixedSpellActivation(DuelZone sourceZone, int index)
    {
        if (primitiveActionQueue is not { IsBusy: false } || ActionsBlocked ||
            PendingEndDiscards != 0 || Phase != DemoPhase.Main ||
            sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap or DuelZone.FieldSpell) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !TryResolveCardTarget(source, out _, out var card) || !SupportsQueuedFixedSpell(card))
            return false;
        if (FieldSpellRules.Supports(card.Id)) return TryPlanFieldActivation(source, out _);
        if (SupportsQueuedContinuousSpell(card)) return CanActivateContinuousSpell(source, out _);
        if (IsRevivalSpell(card)) return CanResolveRevivalSpell(source);
        if (IsHandManipulationSpell(card)) return CanResolveHandManipulationSpell(source);
        if (DrawSpellRules.SupportsQueued(card))
            return CanResolveQueuedDrawSpell(source, out _);
        if (DeckSearchSpellRules.Filter(card.Id) is not null)
            return CanResolveDeckSearchSpell(source, out _);
        if (LifePointSpellRules.SupportsQueued(card))
            return CanResolveQueuedLifePointSpell(source, out _);
        if (MonsterWipeRules.SupportsQueued(card.Id))
            return CanAdmitQueuedMonsterWipe(source);
        return TryPlanSpellTrapWipe(source, out _, out _);
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
            !TryResolveCardTarget(source, out _, out var card))
            return false;
        if (SupportsQueuedContinuousSpell(card)) return TryQueueContinuousSpell(source,out message,origin);
        if (FieldSpellRules.Supports(card.Id))
            return TryQueueFieldSpellActivation(zone,index,out message,origin);
        if (card.ClassCode != 22 && !(card.ClassCode == 21 &&
            (DrawSpellRules.SupportsQueued(card) || LifePointSpellRules.SupportsQueued(card)))) return false;
        if (IsRevivalSpell(card)) return TryQueueRevivalSpell(source, out message, origin);
        if (IsHandManipulationSpell(card)) return TryQueueHandManipulationSpell(source,out message,origin);
        if (DrawSpellRules.SupportsQueued(card))
            return TryQueueDrawSpell(zone, index, out message, origin);
        if (DeckSearchSpellRules.Filter(card.Id) is not null)
            return TryQueueDeckSearchSpell(zone, index, out message, origin);
        if (LifePointSpellRules.SupportsQueued(card))
            return TryQueueLifePointSpell(zone, index, out message, origin);
        if (MonsterWipeRules.SupportsQueued(card.Id))
            return TryQueueMonsterWipe(zone, index, out message, origin);
        if (SpellTrapWipeRules.SupportsQueued(card.Id))
            return TryQueueSpellTrapWipe(zone, index, out message, origin);
        if (card.Id == TargetedMonsterSpellRules.Fissure)
        {
            // For a unique legal minimum no caller choice is necessary.
            // A tie requires the explicit occurrence API; never use slot order.
            var choices = QueuedTargetedMonsterSpellCandidates(source);
            if (choices.Count == 1)
            {
                int enemy = 1-source.Side;
                int minimum = AttackValue(enemy,choices[0].Zone);
                int tied = Enumerable.Range(0,5).Count(z => Monsters[enemy,z] is not null &&
                    !MonsterFaceDown[enemy,z] && AttackValue(enemy,z) == minimum);
                if (tied == 1)
                    return TryQueueTargetedMonsterSpell(source,choices[0].Target,out message,origin);
            }
            message = choices.Count == 0 ? "No eligible face-up opponent monster." :
                "Choose one of the tied minimum-ATK monsters explicitly.";
        }
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
        DemoFlipEffect.DamagePerOpposingSpellTrap or DemoFlipEffect.RaiseSelfStats or DemoFlipEffect.ChangeFaceUpPosition or
        DemoFlipEffect.PreventMonsterAttack or
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
            result.Add(new DestroyCommand(e.Owner, target) { Cause = RemovalCause.LinkedCleanup });
        }
        if (toGraveyard && ImmortalThunderRules.LosesLifeOnFieldToGraveyard(card.Id, MonsterWasFlipped[player, slot]))
            result.Add(new DamageCommand(player, ImmortalThunderRules.GraveyardLifeLoss));
        links = result.ToArray();
        return true;
    }

    private bool CanQueueMonsterRemoval(int player, int slot) =>
        TryPlanRemovalLinks(player, slot, out _);

    private sealed partial class PrimitiveActionExecutor(DemoDuel duel) : IDuelCommandExecutor
    {
        private sealed class Wipe(int caster, ushort resourceId, CardTarget source, IEnumerable<CardTarget> targets, bool monsterWipe = true)
        {
            public int Caster { get; } = caster;
            public ushort ResourceId { get; } = resourceId;
            public CardTarget Source { get; } = source;
            public bool SourceSpent { get; set; }
            public bool MonsterWipe { get; } = monsterWipe;
            public DemoSummonEvent? Summon { get; init; }
            public DemoAttackDeclaration? Attack { get; init; }
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
        private readonly Queue<DuelCommand> reservedMandatoryDrawEffects = new();
        private CardTarget? drawSpellSource;
        private bool drawSpellSourceSpent;
        private CardTarget? searchSpellSource;
        private bool searchSpellSourceSpent;
        private sealed record TargetedMonsterSpellReservation(CardTarget Source, CardTarget? Cost, CardTarget Target, ushort ResourceId);
        private TargetedMonsterSpellReservation? targetedMonsterSpell;
        private bool targetedMonsterSpellPaid;
        private bool targetedMonsterSpellSpent;
        private ActivateFinalDestinyCommand? finalDestinyPayment;
        private int finalDestinyPaid;
        private bool finalDestinyFailed;
        private bool finalDestinySpent;
        private CardTarget? fieldActivationSource;
        private readonly Queue<CardTarget> fieldReplacementTargets=new();
        private bool fieldActivationFailed;
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
            if (command is PrepareFusionSummonCommand prepareFusion)
                return PrepareFusionSummon(prepareFusion, followups);
            if (command is FinishFusionSummonCommand finishFusion)
                return FinishFusionSummon(finishFusion);
            if (fusionBatch is not null) return ExecuteFusionStep(command);
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
            if (command is OpenGraveyardRevivalSpellCommand openRevivalSpell)
                return OpenRevivalSpell(openRevivalSpell);
            if (command is ResolveHandManipulationCommand handAnswer)
                return ResolveHandManipulation(handAnswer,followups);
            if (command is AdvanceHandManipulationCommand handAdvance)
                return AdvanceHandManipulation(handAdvance,followups);
            if (command is ResolveGraveyardRevivalCommand revival)
                return ResolveGraveyardRevival(revival, followups);
            if (command is ResolveGraveyardRecoveryCommand recovery)
                return ResolveGraveyardRecovery(recovery, followups);
            if (command is FinishGraveyardRecoveryCommand)
                return FinishGraveyardRecovery();
            if (command is ResolveFlipSpellTrapPairCommand pair)
                return ResolveFlipSpellTrapPair(pair, followups);
            if (command is ResolveMonsterFlipTargetsCommand flipTargets)
                return ResolveQueuedMonsterTargets(flipTargets.Source,
                    new CardTarget?[] { flipTargets.First, flipTargets.Second }.OfType<CardTarget>().ToArray(),
                    DemoMonsterEffectTrigger.Flip, followups);
            if (command is ResolveMonsterPositionTargetCommand positionTarget)
                return ResolveQueuedMonsterTargets(positionTarget.Source, [positionTarget.Target],
                    DemoMonsterEffectTrigger.PositionChange, followups);
            if (command is ResolveMonsterSummonTargetCommand summonTarget)
                return ResolveQueuedMonsterTargets(summonTarget.Source, [summonTarget.Target],
                    DemoMonsterEffectTrigger.NormalOrFlipSummon, followups);
            if (command is OpenAttackResponseCommand openAttackResponse)
                return duel.OpenAttackResponse(openAttackResponse);
            if (command is ResolveAttackResponseCommand attackResponse)
                return ResolveAttackResponse(attackResponse, followups);
            if (command is ResolveBattleDamageDrawCommand battleDraw)
                return ResolveBattleDamageDraw(battleDraw, followups);
            if (command is ResolveBattleDamageDiscardCommand battleDiscard)
                return ResolveBattleDamageDiscard(battleDiscard, followups);
            if (command is FinishBattleDamageDrawCommand finishBattleDraw)
                return FinishBattleDamageDraw(finishBattleDraw);
            if (command is ResolveSpentAttackTrapCommand spentAttackTrap)
                return ResolveSpentAttackTrap(spentAttackTrap, followups);
            if (command is ResolveSummonResponseCommand summonResponse)
                return ResolveSummonResponse(summonResponse, followups);
            if (command is OpenBattleFlipDrawCommand openBattleFlip)
                return OpenBattleFlipDraw(openBattleFlip, followups);
            if (command is OpenBattleFlipRecoveryCommand recoveryFlip)
                return duel.OpenQueuedBattleFlipRecovery(recoveryFlip, followups);
            if (command is OpenBattleFlipDeckFlowCommand deckFlip)
                return OpenBattleFlipDeckFlow(deckFlip, followups);
            if (command is ResolveSlateWarriorFlipCommand slateFlip)
                return ResolveSlateWarriorFlip(slateFlip, followups);
            if (command is ResolveBattleFlipDamageCommand damageFlip)
                return duel.ResolveQueuedBattleFlipDamage(damageFlip, followups);
            if (command is OpenBattleFlipTargetsCommand openBattleTargets)
                return duel.OpenQueuedBattleFlipTargets(openBattleTargets, followups);
            if (command is FinishBattleFlipCommand finishBattleFlip)
                return duel.FinishQueuedBattleFlip(finishBattleFlip, followups);
            // An exact battle reservation may still need cleanup after lethal damage or Exodia.
            if (duel.GameOver && command is SnapshotBattleRemovalCommand afterWin &&
                duel.queuedBattleRemoved[afterWin.Role] is { } marked && marked.Target == afterWin.Target)
                return duel.SnapshotQueuedBattleRemoval(afterWin);
            if (command is OpenBattleRecruitmentCommand)
                return duel.OpenQueuedBattleRemovalSelection();
            if (command is FinishBattleRemovalCommand battleFinish)
                return duel.FinishQueuedBattleRemoval(battleFinish, followups);
            // The equipped monster's reserved cleanup still runs after lethal battle damage.
            if (command is DestroyCommand linkedEquip && duel.queuedBattleEquipmentRemovals.Remove(linkedEquip.Target))
                return DestroyQueuedSpellTrap(linkedEquip.Target);
            if (command is FinishFieldRemovalCommand)
                return FinishRemoval(followups);
            if (command is FinishSummonFlipDrawCommand)
            {
                duel.BeginNextQueuedDeckSearch();
                return new(CommandOutcome.Applied);
            }
            if (command is ResolveFinalDestinyCommand finalDestinyResolve)
                return ResolveFinalDestiny(finalDestinyResolve,followups);
            if (command is ResolveTargetedMonsterSpellCommand resolveDiscardSpell)
                return ResolveTargetedMonsterSpell(resolveDiscardSpell, followups);
            if (command is FinishRitualSummonCommand finishRitual)
                return FinishRitualSummon(finishRitual);
            if (ritualSummon is { } ritual && !ritualSpellSpent &&
                (command is not SendToGraveyardCommand spendRitual || spendRitual.Target != ritual.Spell))
                return new(CommandOutcome.Skipped, "Ritual spell was not spent.");
            if (command is FinishNormalPlacementCommand finish)
                return FinishPlacement(finish, followups);
            if (command is FinishFlipBatchCommand jar) return FinishFlipBatch(jar);
            if (command is FinishFlipDeckFlowCommand deckFlow) return FinishFlipDeckFlow(deckFlow);
            if (command is FinishMorphingJarCommand morphingJar) return FinishMorphingJar(morphingJar);
            if (command is FinishCardDestructionCommand cardDestruction) return FinishCardDestruction(cardDestruction);
            if (command is RecordFlipDrawDecisionCommand decision) return RecordFlipDrawDecision(decision, followups);
            // An unspent source cancels the entire reserved wipe, including linked consequences.
            if (wipe is { SourceSpent: false } pendingWipe &&
                (command is not SendToGraveyardCommand sendSource || sendSource.Target != pendingWipe.Source))
                return new(CommandOutcome.Skipped, "Wipe source was not spent.");
            // The cost must succeed before spending the spell. Recheck the
            // complete reservation before payment; after payment do not refund
            // just because the target disappears before resolution.
            if (targetedMonsterSpell is { } paidSpell)
            {
                if (command is DiscardCommand pay && pay.Target == paidSpell.Cost &&
                    !duel.TryPlanTargetedMonsterSpell(paidSpell.Source,paidSpell.Cost,paidSpell.Target))
                    return new(CommandOutcome.Skipped, "Reserved activation changed before payment.");
                if (command is SendToGraveyardCommand spend && spend.Target == paidSpell.Source &&
                    (!targetedMonsterSpellPaid || paidSpell.Cost is null &&
                     !duel.TryPlanTargetedMonsterSpell(paidSpell.Source,null,paidSpell.Target)))
                    return new(CommandOutcome.Skipped, "Payment or activation condition not satisfied.");
            }
            if (finalDestinyPayment is { } finalPayment)
            {
                if (command is DiscardCommand finalDiscard)
                {
                    var remaining = finalPayment.Costs.Skip(finalDestinyPaid).ToArray();
                    if (finalDestinyFailed || remaining.Length==0 || remaining[0]!=finalDiscard.Target ||
                        !duel.CanPayFinalDestiny(finalPayment.Source,remaining))
                    {
                        finalDestinyFailed=true;
                        return new(CommandOutcome.Skipped,"Reserved discard payment changed.");
                    }
                }
                if (command is SendToGraveyardCommand finalSpend && finalSpend.Target == finalPayment.Source &&
                    (finalDestinyFailed || finalDestinyPaid != FinalDestinyCost ||
                     !duel.IsFinalDestinySource(finalPayment.Source)))
                    return new(CommandOutcome.Skipped,"Final Destiny's complete cost was not paid.");
            }
            // Reserved battle removal consequences can finish after lethal battle damage.
            bool finishBattleRemovalEffect = duel.queuedBattlePostRemovalEffects.TryPeek(out var expectedBattleEffect) &&
                ReferenceEquals(expectedBattleEffect, command);
            if (finishBattleRemovalEffect) duel.queuedBattlePostRemovalEffects.Dequeue();
            bool finishMandatoryDrawEffect = reservedMandatoryDrawEffects.TryPeek(out var expectedDraw) && expectedDraw == command;
            if (finishMandatoryDrawEffect) reservedMandatoryDrawEffects.Dequeue();
            // Admitted mandatory batches still finish after Exodia or lethal damage.
            if (duel.LegacyActionsBlocked && !(duel.GameOver &&
                ((resolvingJar && command is DrawCommand) || finishMandatoryDrawEffect || finishBattleRemovalEffect || (command is ShuffleDeckCommand reservedShuffle && reservedDeckSearchOwner == reservedShuffle.Side && deckSearchMoved) || wipe is not null || placement is not null || linkedRemovalBatch)))
                return new(CommandOutcome.Rejected, "Duel has ended or a legacy decision is pending.");

            if (duel.HasQueuedEndDiscard && command is not EndTurnDiscardCommand)
                return new(CommandOutcome.Rejected, "Resolve the queued end-turn discard first.");

            switch (command)
            {
                case LabSpawnCommand spawn: return duel.ExecuteLabSpawn(spawn);
                case LabMoveCommand move: return duel.ExecuteLabMove(move);
                case LabCoinResultCommand coin:
                    duel.LabCoinResultReported?.Invoke(coin.Result);
                    return new(CommandOutcome.Applied,
                        $"region={coin.Result.Region}; count={coin.Result.Count}; bits={coin.Result.Bits}; highlight={coin.Result.Highlight}");
                case LabDiceResultCommand die:
                    duel.LabDiceResultReported?.Invoke(die.Result);
                    return new(CommandOutcome.Applied, $"region={die.Result.Region}; index={die.Result.Index}");
                case DeclareDirectAttackCommand attack:
                    return duel.ExecuteQueuedDirectAttack(attack, followups);
                case DeclareTargetedAttackCommand attack:
                    return duel.ExecuteQueuedTargetedAttack(attack, followups);
                case RevealBattleDefenderCommand reveal:
                    return duel.RevealQueuedBattleDefender(reveal, followups);
                case ResolveRevealedBattleCommand revealed:
                    return duel.ResolveQueuedRevealedBattle(revealed, followups);
                case SnapshotBattleRemovalCommand snapshot:
                    return duel.SnapshotQueuedBattleRemoval(snapshot);
                case ChangeBattlePhaseCommand phase:
                    return duel.ExecuteQueuedBattlePhaseChange(phase, followups);
                case FinishBattlePhaseCommand finishBattle:
                    return duel.FinishQueuedBattlePhase(finishBattle);
                case GoblinAttackForceDefenseCommand forcedDefense:
                    return duel.ExecuteGoblinAttackForceDefense(forcedDefense);
                case EndTurnCommand endTurn:
                    return duel.ExecuteQueuedEndTurn(endTurn, followups);
                case FinishEndPhaseReturnCommand endReturn:
                    return duel.FinishQueuedEndReturn(endReturn, followups);
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
                case StandbyDefenseCommand standbyDefense:
                    return duel.ExecuteStandbyDefense(standbyDefense);
                case DrawCommand draw:
                    if (flipDeckFlow is { IsMill: false } flow && flow.Side == draw.Side)
                        return DrawForShadowScout(flow);
                    if (duel.decks[draw.Side].Count == 0)
                    {
                        duel.Winner = 1 - draw.Side;
                        return new(CommandOutcome.Applied, "Empty deck: player loses (sandbox policy).");
                    }
                    duel.DrawToHand(draw.Side, checkExodia: !resolvingCardDestructionDraws && !DeferHandManipulationExodia);
                    return new(CommandOutcome.Applied);
                case DamageCommand damage:
                    duel.ChangeLife(damage.Side, -damage.Amount, damage.DeferDuelResult);
                    if (flipBanish is { } paid && paid.Removed.Count == paid.Required &&
                        damage.Side == 1 - paid.Owner && damage.Amount == FlipBanishBurnRules.BurnDamage)
                        paid.DamageApplied = true;
                    return new(CommandOutcome.Applied);
                case ApplyBattleDestructionStatChangeCommand statChange when finishBattleRemovalEffect:
                    return duel.ApplyBattleDestructionStatChange(statChange);
                case FinishJointDamageCommand:
                    if (duel.Life[0] == 0 && duel.Life[1] == 0) duel.IsDraw = true;
                    else if (duel.Life[0] == 0) duel.Winner = 1;
                    else if (duel.Life[1] == 0) duel.Winner = 0;
                    return new(CommandOutcome.Applied);
                case HealCommand heal:
                    duel.ChangeLife(heal.Side, heal.Amount);
                    return new(CommandOutcome.Applied);
                case EquipCommand equip:
                    return duel.ApplyQueuedEquipment(equip);
                case ActivateTemporaryStatCommand temporaryStat:
                    return ActivateTemporaryStat(temporaryStat, followups);
                case ResolveTemporaryStatCommand temporaryStatResult:
                    return ResolveTemporaryStat(temporaryStatResult);
                case ActivateGraveyardRevivalSpellCommand reviveSpell:
                    return ActivateRevivalSpell(reviveSpell, followups);
                case ActivateHandManipulationSpellCommand handSpell:
                    return ActivateHandManipulation(handSpell,followups);
                case ActivateDrawSpellCommand drawSpell:
                    return ActivateDrawSpell(drawSpell, followups);
                case ActivateDiscardMonsterSpellCommand paidSpellActivation:
                    return ActivateDiscardMonsterSpell(paidSpellActivation, followups);
                case ActivateConditionalMonsterSpellCommand conditionalSpell:
                    return ActivateConditionalMonsterSpell(conditionalSpell, followups);
                case ActivateDeckSearchSpellCommand searchSpell:
                    return ActivateDeckSearchSpell(searchSpell, followups);
                case OpenDeckSearchSpellCommand openSearch:
                    return OpenDeckSearchSpell(openSearch);
                case ActivateLifePointSpellCommand lp:
                    return ActivateLifePointSpell(lp, followups);
                case ActivateTargetedSpellTrapCommand targeted:
                    return ActivateTargetedSpellTrap(targeted, followups);
                case ActivateFinalDestinyCommand finalDestiny:
                    return ActivateFinalDestiny(finalDestiny,followups);
                case PrepareFieldSpellActivationCommand fieldPrepare:
                    return PrepareFieldActivation(fieldPrepare,followups);
                case ActivateFieldSpellCommand fieldActivate:
                    return ApplyFieldActivation(fieldActivate);
                case ActivateContinuousSpellCommand continuous:
                    return duel.ApplyContinuousSpell(continuous);
                case ActivateSpellTrapWipeCommand spellTrapWipe:
                    return ActivateSpellTrapWipe(spellTrapWipe.Source, followups);
                case ActivateMonsterWipeCommand monsterWipe:
                    return ActivateMonsterWipe(monsterWipe.Source, followups);
                case ActivateRaigekiCommand activate:
                    return ActivateMonsterWipe(activate.Source, followups, requireRaigeki: true);
                case PrepareRitualSummonCommand prepareRitual:
                    return PrepareRitualSummon(prepareRitual, followups);
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
                case ChangePositionByFlipCommand flipPosition:
                    return ApplyFlipPositionChange(flipPosition);
                case BindMonsterAttackCommand restriction:
                    return duel.ApplyMonsterAttackRestriction(restriction);
                case ResolvePositionCommand resolve:
                    return ResolvePosition(resolve, followups);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Deck:
                    return SendDeckCardToGraveyard(send.Target);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Hand:
                    return ObserveFixedSpellSpend(send.Target, SendHandToGraveyard(send.Target, discard: false));
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.FieldSpell:
                    return RemoveReplacedField(send.Target);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.SpellTrap:
                    return ObserveFixedSpellSpend(send.Target, SpendSetActivation(send.Target));
                case PrepareGraveyardBanishSummonCommand prepareBanish:
                    return PrepareGraveyardBanishSummon(prepareBanish, followups);
                case BanishCommand banish:
                    return BanishFromSource(banish.Target);
                case DiscardCommand discard:
                    var discarded = SendHandToGraveyard(discard.Target, discard: true);
                    if (finalDestinyPayment is not null)
                    {
                        if (discarded.Outcome==CommandOutcome.Applied) finalDestinyPaid++;
                        else finalDestinyFailed=true;
                    }
                    if (targetedMonsterSpell is { } reservedSpell && reservedSpell.Cost == discard.Target)
                        targetedMonsterSpellPaid = discarded.Outcome == CommandOutcome.Applied;
                    if (discarded.Outcome == CommandOutcome.Applied && flipDeckFlow is { IsMill: false } scout &&
                        scout.NewSpells.Contains(discard.Target)) scout.Discarded++;
                    if (discarded.Outcome == CommandOutcome.Applied && cardDestructionDiscards is { } counts)
                        counts[discard.Side]++;
                    return discarded;
                case SpecialSummonCommand specialSummon:
                    return SpecialSummonFromSource(specialSummon);
                case MoveToHandCommand move:
                    return MoveSelectedCardToHand(move.Source);
                case ShuffleDeckCommand shuffle:
                    return ShuffleDeck(shuffle.Side);
                case ReturnToDeckCommand returnDeck:
                    return returnDeck.Target.Zone == DuelZone.Hand
                        ? ReturnHandToDeck(returnDeck.Target) : ReturnMonsterToDeck(returnDeck.Target);
                case ReturnToHandCommand bounce:
                    if (duel.queuedEndReturn == bounce.Target)
                    {
                        int turn = duel.Turn;
                        var result = duel.TryResolveCardTarget(bounce.Target, out int slot, out _) &&
                            duel.IsEndPhaseReturnMonster(bounce.Side, slot)
                            ? ReturnQueuedMonster(bounce.Target, followups)
                            : new(CommandOutcome.Skipped, "The End Phase return source is no longer eligible.");
                        followups.ScheduleContinuation(_ => new([new FinishEndPhaseReturnCommand(
                            bounce.Side, turn, bounce.Target)]));
                        return result;
                    }
                    return bounce.Target.Zone == DuelZone.Monster ? ReturnQueuedMonster(bounce.Target, followups) : ReturnQueuedSpellTrap(bounce.Target);
                case DestroyCommand destroy when destroy.Target.Zone == DuelZone.Monster:
                    return DestroyMonster(destroy.Target, followups, destroy.Cause);
                case DestroyCommand destroy when destroy.Target.Zone is DuelZone.SpellTrap or DuelZone.FieldSpell:
                    return DestroyQueuedSpellTrap(destroy.Target, destroy.Cause);
                default:
                    return new(CommandOutcome.Rejected, "Command not migrated to DemoDuel.");
            }
        }

        private CommandResult ObserveFixedSpellSpend(CardTarget target, CommandResult result)
        {
            if (finalDestinyPayment?.Source == target) finalDestinySpent = result.Outcome == CommandOutcome.Applied;
            if (ritualSummon?.Spell == target) ritualSpellSpent = result.Outcome == CommandOutcome.Applied;
            if (temporaryStatActivation?.Source == target)
                temporaryStatSourceSpent = result.Outcome == CommandOutcome.Applied;
            if (handManipulation is { } handBatch && handBatch.Source == target)
                handBatch.SourceSpent = result.Outcome == CommandOutcome.Applied;
            if (revivalSpellSource == target) revivalSpellSpent = result.Outcome == CommandOutcome.Applied;
            if (drawSpellSource == target) drawSpellSourceSpent = result.Outcome == CommandOutcome.Applied;
            if (searchSpellSource == target) searchSpellSourceSpent = result.Outcome == CommandOutcome.Applied;
            if (targetedMonsterSpell is { } paidSpell && paidSpell.Source == target)
                targetedMonsterSpellSpent = result.Outcome == CommandOutcome.Applied;
            if (attackTrapPayment?.Source == target)
                attackTrapSpent = result.Outcome == CommandOutcome.Applied;
            if (wipe is { } current && current.Source == target)
                current.SourceSpent = result.Outcome == CommandOutcome.Applied;
            return result;
        }

        private CommandResult PrepareFieldActivation(PrepareFieldSpellActivationCommand command,IActionCommandSink followups)
        {
            if(fieldActivationSource is not null || wipe is not null || finishScheduled ||
                followups is not IActionBatchSink {PendingCount:0} queue ||
                !duel.TryPlanFieldActivation(command.Source,out var replaced))
                return new(CommandOutcome.Rejected,"Field activation no longer legal.");
            fieldActivationSource=command.Source;
            fieldActivationFailed=false;
            fieldReplacementTargets.Clear();
            foreach(var target in replaced)fieldReplacementTargets.Enqueue(target);
            queue.EnqueueBatch([.. replaced.Select(t=>(DuelCommand)new SendToGraveyardCommand(t.Side,t)),
                new ActivateFieldSpellCommand(command.Side,command.Source)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult RemoveReplacedField(CardTarget target)
        {
            if(fieldActivationSource is not { } source || fieldActivationFailed ||
                !fieldReplacementTargets.TryPeek(out var expected)||expected!=target)
                return new(CommandOutcome.Rejected,"No reserved Field Spell replacement.");
            if(!duel.TryPlanFieldActivation(source,out var current)||
                !current.SequenceEqual(fieldReplacementTargets))
            {
                fieldActivationFailed=true;
                return new(CommandOutcome.Skipped,"Field replacement occurrences changed.");
            }
            fieldReplacementTargets.Dequeue();
            duel.SendFieldSpellToGraveyard(target.Side);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ApplyFieldActivation(ActivateFieldSpellCommand command)
        {
            if(fieldActivationSource!=command.Source)
                return new(CommandOutcome.Rejected,"No matching Field Spell activation.");
            bool valid=!fieldActivationFailed&&fieldReplacementTargets.Count==0;
            fieldActivationSource=null;
            fieldReplacementTargets.Clear();
            fieldActivationFailed=false;
            if(!valid || !duel.TryPlanFieldActivation(command.Source,out var remaining)||remaining.Length!=0 ||
                !duel.TryResolveCardTarget(command.Source,out int index,out var card))
                return new(CommandOutcome.Skipped,"Field activation source or replacement changed.");
            duel.CommitFieldActivation(command.Side,
                command.Source.Zone == DuelZone.Hand ? index : null);
            var result=new CommandResult(CommandOutcome.Applied);
            return command.Source.Zone==DuelZone.Hand?result with
            {Placement=new(command.Source,card.Id,index,DuelZone.FieldSpell,0,false,false,0)}:result;
        }

        private CommandResult ActivateFinalDestiny(ActivateFinalDestinyCommand activate,IActionCommandSink followups)
        {
            if (finalDestinyPayment is not null || wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount:0 } queue ||
                !duel.CanPayFinalDestiny(activate.Source,activate.Costs) ||
                !duel.TryPlanFinalDestinyField(activate.Source,out _,out var targets,out _) || targets.Length==0)
                return new(CommandOutcome.Rejected,"Final Destiny requires a legal field and five distinct hand costs.");
            finalDestinyPayment=activate;
            finalDestinyPaid=0;
            finalDestinyFailed=finalDestinySpent=false;
            queue.EnqueueBatch([.. activate.Costs.Select(t => (DuelCommand)new DiscardCommand(activate.Side,t)),
                new SendToGraveyardCommand(activate.Side,activate.Source),
                new ResolveFinalDestinyCommand(activate.Side,activate.Source)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveFinalDestiny(ResolveFinalDestinyCommand resolve,IActionCommandSink followups)
        {
            if (finalDestinyPayment is not { } payment || payment.Source != resolve.Source)
                return new(CommandOutcome.Rejected,"No matching field wipe payment.");
            bool paid=finalDestinyPaid==FinalDestinyCost&&!finalDestinyFailed&&finalDestinySpent;
            finalDestinyPayment=null;
            finalDestinyPaid=0;
            finalDestinyFailed=finalDestinySpent=false;
            if (!paid || duel.LegacyActionsBlocked)
                return new(CommandOutcome.Skipped,"Field wipe cost or source spend incomplete.");
            if (wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount:0 } queue ||
                !duel.TryPlanFinalDestinyField(resolve.Source,out var commands,out var targets,out var plans))
                return new(CommandOutcome.Rejected,"Field wipe occurrences or links invalid.");
            // Non-targeting effect: snapshot the field after successful
            // payment, not at admission. Equipment appears only once.
            queue.EnqueueBatch(commands);
            foreach(var pair in plans) plannedLinks.Add(pair.Key,pair.Value);
            wipe=new(resolve.Side,FinalDestiny,resolve.Source,targets,monsterWipe:false){SourceSpent=true};
            ScheduleFinish(resolve.Side,followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateDiscardMonsterSpell(ActivateDiscardMonsterSpellCommand activate, IActionCommandSink followups)
        {
            if (targetedMonsterSpell is not null || wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Discard-cost spell needs isolated activation.");
            if (!duel.TryPlanTargetedMonsterSpell(activate.Source,activate.Cost,activate.Target))
                return new(CommandOutcome.Rejected, "Source, cost or target no longer legal.");
            targetedMonsterSpell = new(activate.Source,activate.Cost,activate.Target,TargetedMonsterSpellRules.TributeToTheDoomed);
            targetedMonsterSpellPaid = targetedMonsterSpellSpent = false;
            queue.EnqueueBatch([new DiscardCommand(activate.Side,activate.Cost),
                new SendToGraveyardCommand(activate.Side,activate.Source),
                new ResolveTargetedMonsterSpellCommand(activate.Side,activate.Source,activate.Target)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ActivateConditionalMonsterSpell(ActivateConditionalMonsterSpellCommand activate, IActionCommandSink followups)
        {
            if (targetedMonsterSpell is not null || wipe is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Conditional spell needs isolated activation.");
            if (!duel.TryPlanTargetedMonsterSpell(activate.Source,null,activate.Target) ||
                !duel.TryResolveCardTarget(activate.Source,out _,out var spell))
                return new(CommandOutcome.Rejected, "Source, condition or target no longer legal.");
            targetedMonsterSpell = new(activate.Source,null,activate.Target,spell.Id);
            targetedMonsterSpellPaid = true; // no payment required
            targetedMonsterSpellSpent = false;
            queue.EnqueueBatch([new SendToGraveyardCommand(activate.Side,activate.Source),
                new ResolveTargetedMonsterSpellCommand(activate.Side,activate.Source,activate.Target)]);
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveTargetedMonsterSpell(ResolveTargetedMonsterSpellCommand resolve, IActionCommandSink followups)
        {
            if (targetedMonsterSpell is not { } reserved || reserved.Source != resolve.Source ||
                reserved.Target != resolve.Target)
                return new(CommandOutcome.Rejected, "No matching targeted spell reservation.");
            bool paid = targetedMonsterSpellPaid && targetedMonsterSpellSpent;
            targetedMonsterSpell = null;
            targetedMonsterSpellPaid = targetedMonsterSpellSpent = false;
            if (!paid || duel.LegacyActionsBlocked)
                return new(CommandOutcome.Skipped, "Unpaid, unspent or blocked spell.");
            if (followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Isolated destruction required.");
            // Independent resolution clause, not an activation cost. It
            // still applies when the selected monster has become ineligible.
            bool skipDraw = reserved.ResourceId == TargetedMonsterSpellRules.OfferingsToTheDoomed;
            if (skipDraw) duel.ScheduleNextDrawPhaseSkip(reserved.Source.Side);
            if (!duel.IsMonsterSpellChoice(reserved.ResourceId,reserved.Source.Side,resolve.Target))
                return skipDraw ? new(CommandOutcome.Applied, "Next Draw Phase skipped; target no longer eligible.") :
                    new(CommandOutcome.Skipped, "Selected occurrence or effect criterion no longer valid.");
            if (Joey.Cards.PositionAndRemovalEffects.DesiredDefense(reserved.ResourceId) is bool defense)
            {
                queue.Enqueue(new ChangePositionByFlipCommand(resolve.Target.Side, resolve.Target, defense,
                    FromPositionSpell: true));
                return new(CommandOutcome.Applied);
            }
            // Existing standalone DestroyMonster reserves live linked
            // consequences at execution, and opens deferred triggers only
            // after those commands drain. No replacement is selected.
            queue.Enqueue(new DestroyCommand(resolve.Target.Side,resolve.Target));
            return new(CommandOutcome.Applied);
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
            if (reservedMandatoryDrawEffects.Count != 0 || drawSpellSource is not null ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Draw spell requires isolated activation.");
            if (!duel.TryResolveCardTarget(activate.Source, out _, out _))
                return new(CommandOutcome.Skipped, "Draw spell occurrence gone.");
            if (!duel.CanResolveQueuedDrawSpell(activate.Source, out var card))
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
                if (DrawSpellRules.IsCardDestruction(card)) return PlanCardDestruction(activate.Side);
                var effect = DrawSpellRules.QueuedEffect(card) ??
                    throw new InvalidOperationException("Reserved draw spell has no effect.");
                // Recheck availability before issuing the reserved batch.
                // No other admitted action can run between these draws.
                if (duel.DeckCount(activate.Side) < effect.Cards)
                    throw new InvalidOperationException("Reserved draw spell deck changed during activation.");
                var context = new EffectContext();
                context.Draw(activate.Side, effect.Cards);
                context.ChangeLifePoints(1 - activate.Side, effect.OpponentLifeGain);
                foreach (var next in context.Commands) reservedMandatoryDrawEffects.Enqueue(next);
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
            var context = new EffectContext();
            context.SendToGraveyard(activate.Source);
            if (change.JointDamage)
            {
                // Resolve the two losses in emission order. Check for a
                // winner only after both commands complete their gates.
                context.ChangeLifePoints(1 - activate.Side, change.OpponentDelta, deferDuelResult: true);
                context.ChangeLifePoints(activate.Side, change.CasterDelta, deferDuelResult: true);
                queue.EnqueueBatch(context.Commands);
                queue.Enqueue(new FinishJointDamageCommand(activate.Side));
                return new(CommandOutcome.Applied);
            }
            // Existing spells retain caster-first order.
            context.ChangeLifePoints(activate.Side, change.CasterDelta);
            context.ChangeLifePoints(1 - activate.Side, change.OpponentDelta);
            queue.EnqueueBatch(context.Commands);
            return new(CommandOutcome.Applied);
        }

        private CommandResult SpendSetActivation(CardTarget target)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Set spell occurrence gone.");
            bool fixedSpell = card.ClassCode == 22 && (LifePointSpellRules.SupportsQueued(card.Id) ||
                DrawSpellRules.SupportsQueued(card) || MonsterWipeRules.SupportsQueued(card.Id) ||
                DeckSearchSpellRules.Filter(card.Id) is not null || SpellTrapWipeRules.SupportsQueued(card.Id) ||
                SpellTrapTargetRules.SupportsQueued(card.Id) || TargetedMonsterSpellRules.Supports(card) || IsRevivalSpell(card) ||
                handManipulation?.Source == target && IsHandManipulationSpell(card) || card.Id == FinalDestiny ||
                ritualSummon?.Spell == target && RitualSearchRules.IsRitualSpell(card));
            bool ownMainTrap = card.ClassCode == 21 &&
                (LifePointSpellRules.SupportsQueued(card) || DrawSpellRules.SupportsQueued(card)) &&
                duel.CanActivateOwnMainTrap(target, slot);
            bool temporaryModifier = temporaryStatActivation?.Source == target &&
                TemporaryStatRules.For(card.Id)?.ClassCode == card.ClassCode;
            bool reservedTrapWipe = wipe is { } responseWipe &&
                (responseWipe.Summon is not null || responseWipe.Attack is not null) &&
                responseWipe.Source == target && responseWipe.ResourceId == card.Id &&
                card.ClassCode == 21 && duel.spellSetTurn[target.Side, slot] < duel.Turn;
            bool attackTrap = attackTrapPayment?.Source == target && duel.IsQueuedAttackResponseTrap(target.Side, slot);
            if (!(fixedSpell || ownMainTrap || temporaryModifier || reservedTrapWipe || attackTrap) || !duel.SpellFaceDown[target.Side, slot] ||
                duel.equipment.Any(e => e.Owner == target.Side && e.SourceZone == slot))
                return new(CommandOutcome.Rejected, "Only supported unlinked Set activations are migrated here.");
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
                    .. targets.Where(t => t != activate.Target).Select(t => (DuelCommand)new DestroyCommand(t.Side, t)
                         { Cause = RemovalCause.LinkedCleanup })]);
            }
            else
            {
                queue.EnqueueBatch([.. context.Commands, .. targets.Select(t => (DuelCommand)new DestroyCommand(t.Side,t)
                    { Cause = t == activate.Target ? RemovalCause.EffectDestruction : RemovalCause.LinkedCleanup })]);
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
            if (!duel.TryResolveCardTarget(source, out _, out _))
                return new(CommandOutcome.Skipped, "Wipe source occurrence gone.");
            if (!duel.CanResolveQueuedMonsterWipe(source, out var spell, out var snapshot) ||
                (requireRaigeki && spell.Id != MonsterWipeRules.Raigeki))
                return new(CommandOutcome.Rejected, "Monster wipe no longer legal.");
            return TryReserveMonsterWipe(source, spell.Id, snapshot, queue)
                ? new(CommandOutcome.Applied)
                : new(CommandOutcome.Rejected, "Untracked occurrence or linked equipment.");
        }

        private bool TryReserveMonsterWipe(CardTarget source, ushort resourceId,
            IReadOnlyList<MonsterWipeTarget> snapshot, IActionBatchSink queue,
            DemoSummonEvent? summon = null, DemoAttackDeclaration? attack = null)
        {
            var targets = new List<CardTarget>();
            var context = new EffectContext();
            context.SendToGraveyard(source);
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            foreach (var item in snapshot)
            {
                if (!duel.TryPlanRemovalLinks(item.Player, item.Zone, out var links) ||
                    !duel.TryCaptureCardTarget(item.Player, DuelZone.Monster, item.Zone, out var target))
                    return false;
                targets.Add(target);
                plans.Add(target, links);
                context.Destroy(target, links);
            }
            // Use the existing sandbox target order, not an invented visual order.
            // Each actual movement receives its own command. No spell/monster
            // movement happens in this activation producer.
            queue.EnqueueBatch(context.Commands);
            foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
            wipe = new(source.Side, resourceId, source, targets) { Summon = summon, Attack = attack };
            ScheduleFinish(source.Side, queue);
            return true;
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
            bool isRitual = ritualSummon is { } ritual && ritualSpellSpent && ritual.Materials.Contains(tribute.Target);
            bool isNormal = placement is { } normal && Tributes(normal).Contains(tribute.Target);
            if (!(isNormal || isRitual) || paidTributes.Contains(tribute.Target))
                return new(CommandOutcome.Rejected, "No matching unpaid tribute.");
            if (tribute.Target.Zone == DuelZone.Hand)
            {
                var result = SendHandToGraveyard(tribute.Target, discard: false);
                if (result.Outcome != CommandOutcome.Applied)
                    throw new InvalidOperationException("Reserved hand tribute changed during Ritual Summon.");
                paidTributes.Add(tribute.Target);
                return result;
            }
            if (!duel.TryResolveCardTarget(tribute.Target, out int slot, out _) ||
                !duel.TryPlanRemovalLinks(tribute.Side, slot, out var links) ||
                !plannedLinks.Remove(tribute.Target, out var expected) || !links.SequenceEqual(expected))
            {
                // External mutation during a reserved batch is unsupported.
                // Fault rather than allow a partially paid summon to proceed.
                throw new InvalidOperationException("Reserved tribute changed during placement.");
            }
            duel.deferringFieldRemovalTriggers = true;
            try { duel.RemoveMonster(tribute.Side, slot, isTribute: true, queueLinkedConsequences: true,
                removalCause: isRitual ? RemovalCause.RitualMaterial : RemovalCause.Tribute); }
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

        private CommandResult FinishPlacement(FinishNormalPlacementCommand finish, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(finish.Target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "Placed occurrence gone.");
            string message = "";
            duel.FinishNormalPlacement(finish.Side, slot, finish.FaceDown, ref message);
            duel.queuedSummonResponse = duel.PendingSummonResponse;
            if (!finish.FaceDown && card.Id == InvitationToADarkSleep)
                OpenAttackRestrictionAfterSummon(finish.Target, slot, card, DemoMonsterEffectTrigger.NormalOrFlipSummon);
            if (!finish.FaceDown && NormalSummonRules.DestroysAfterNormalSummon(card))
            {
                if (duel.queuedSummonResponse is not null)
                    selfDestructAfterSummonResponse = finish.Target;
                else
                    followups.Enqueue(new DestroyCommand(finish.Side, finish.Target));
            }
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
                // The dedicated FIELD slot is separate from the five S/T slots.
            }
            else
            {
                if (set.Destination != DuelZone.SpellTrap || duel.Spells[set.Side, set.Slot] is not null)
                    return new(CommandOutcome.Rejected, "Invalid Spell/Trap destination.");
            }
            duel.CommitSpellTrapSet(set.Side, index, set.Destination, set.Slot);
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
            if (side != duel.ActivePlayer || duel.Phase != DemoPhase.Main || duel.IsManualPositionLocked(side, slot, card) ||
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
            if (change.FlipSummoned && (card.Id == InvitationToADarkSleep ||
                card.Id == FlipEffectRules.DarkEyesIllusionist && !duel.FlipEffectsNegated))
            {
                var trigger = card.Id == InvitationToADarkSleep
                    ? DemoMonsterEffectTrigger.NormalOrFlipSummon : DemoMonsterEffectTrigger.Flip;
                FinishQueuedManualPosition(change, resolveFlip: false, out var message);
                OpenAttackRestrictionAfterSummon(position.Target, slot, card, trigger);
                return new(CommandOutcome.Applied, message);
            }
            if (!change.FlipSummoned || duel.FlipEffectsNegated)
            {
                FinishQueuedManualPosition(change, resolveFlip: true, out var message);
                return new(CommandOutcome.Applied, message);
            }
            if (!duel.CanQueueFlip(card))
                return new(CommandOutcome.Rejected, "Unmigrated FLIP effect.");
            if (card.Id == FlipEffectRules.Skelengel && duel.decks[side].Count > 0)
            {
                var request = new PendingEffectDecision(++duel.nextEffectDecisionId, side, card,
                    EffectDecisionKind.DrawOneOnFlip, 0, duel.Turn);
                FinishQueuedManualPosition(change, resolveFlip: false, out var message);
                if (duel.PendingSummonResponse is { } response && duel.UsesQueuedSummonResponse(response))
                    flipDrawAfterSummonResponse = request;
                else if (duel.LegacyActionsBlocked)
                    duel.queuedEffectDecisions.Enqueue(request);
                else
                    ScheduleManualFlipDraw(request, followups);
                return new(CommandOutcome.Applied, message);
            }
            if (HasQueuedDeckFlip(card.Id))
                return ResolveFlipDeckFlow(position.Side, card,
                    new FinishFlipBatchCommand(position.Side, position.Target, position.WasInDefense), followups);
            if (card.Id == FlipEffectRules.SlateWarrior)
            {
                followups.Enqueue(new ResolveSlateWarriorFlipCommand(side, position.Target));
                followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(side,
                    position.Target, position.WasInDefense)]));
                resolvingFlipBatch = true;
                return new(CommandOutcome.Applied);
            }
            if (card.Id == FlipEffectRules.BiteShoes)
            {
                duel.PendingFlipTarget = new(side, slot, card, DemoFlipEffect.ChangeFaceUpPosition);
                FinishQueuedManualPosition(change, resolveFlip: false, out var message);
                return new(CommandOutcome.Applied, message);
            }
            if (card.Id == FlipEffectRules.FourStarredLadybugOfDoom)
                return ResolveLadybug(position, followups);
            if (card.Id == FlipEffectRules.MorphingJar2)
                return ResolveJar2(position, change, followups);
            if (FlipBanishBurnRules.Cost(card.Id) is not null)
                return ResolveFlipBanish(position, card, followups);
            if (card.Id == FlipEffectRules.PrincessOfTsurugi)
            {
                duel.EnqueueOpposingSpellTrapDamage(side, followups);
                followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(side,
                    position.Target, position.WasInDefense)]));
                resolvingFlipBatch = true;
                return new(CommandOutcome.Applied);
            }
            if (card.Id == FlipEffectRules.ImmortalOfThunder)
            {
                followups.Enqueue(new HealCommand(side, ImmortalThunderRules.FlipLifeGain));
                followups.ScheduleContinuation(_ => new([new FinishFlipBatchCommand(side,
                    position.Target, position.WasInDefense)]));
                // The common finish stage skips legacy FLIP mutations.
                resolvingFlipBatch = true;
                return new(CommandOutcome.Applied);
            }

            // Choice-only effects can open a legacy request, but cannot
            // execute its answer here. A06/C12 migrate the response path.
            FinishQueuedManualPosition(change, resolveFlip: true, out var detail);
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
            card.IsMonster && card.Level is >= 1 and <= 4 && !NormalSummonRules.RequiresSpecialSummon(card) &&
            !NormalSummonRules.IsFlipOnlySummon(card);

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
            commands.Add(new FinishFlipBatchCommand(position.Side,position.Target,position.WasInDefense));
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
                var finish = new FinishFlipBatchCommand(side, position.Target, position.WasInDefense);
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
            // Observe the committed destination, without turning it into a
            // destruction/discard event. No private hand/deck identity escapes.
            duel.CardBanished?.Invoke(new(target.Side,target.Zone,
                target.Zone==DuelZone.Graveyard ? card : null));
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
                position.Target, position.WasInDefense)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveFlipSpellTrapPair(ResolveFlipSpellTrapPairCommand answer, IActionCommandSink followups)
        {
            CardTarget[] targets = new CardTarget?[] { answer.First, answer.Second }.OfType<CardTarget>().ToArray();
            if (wipe is not null || finishScheduled || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.TryPlanFlipSpellTrapPair(answer.Request, targets, out var source, out var batch, out var cascade))
                return new(CommandOutcome.Rejected, "FLIP pair request or occurrence changed.");
            duel.PendingFlipSpellTrapTargets = null;
            duel.resumedSummonFlipPair = null;
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

        private CommandResult ResolveSlateWarriorFlip(ResolveSlateWarriorFlipCommand command, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(command.Target, out int slot, out var card) ||
                card.Id != FlipEffectRules.SlateWarrior || duel.MonsterFaceDown[command.Side, slot])
                throw new InvalidOperationException("Reserved Slate Warrior FLIP changed.");

            if (duel.queuedBattleReveal is { FlipCalculated: true, FlipOpened: false } battle &&
                battle.Defender.Target == command.Target)
            {
                if (!duel.ValidateBattleReveal(battle, faceDown: false, allowEnded: true))
                    throw new InvalidOperationException("Reserved battle FLIP changed before the stat increase.");
                battle.FlipOpened = true;
                followups.Enqueue(new FinishBattleFlipCommand(command.Side, command.Target));
            }
            else if (!resolvingFlipBatch)
                return new(CommandOutcome.Rejected, "No matching Slate Warrior FLIP.");

            if (!duel.GameOver && !duel.FlipEffectsNegated)
                duel.AdjustPersistentMonsterStats(card, new(SlateWarriorStatChange, SlateWarriorStatChange));
            return new(CommandOutcome.Applied);
        }

        private CommandResult OpenBattleFlipDeckFlow(OpenBattleFlipDeckFlowCommand command, IActionCommandSink followups)
        {
            if (duel.queuedBattleReveal is not { FlipCalculated: true, FlipOpened: false } reserved ||
                reserved.Defender.Target != command.Defender ||
                !HasQueuedDeckFlip(reserved.Defender.Card.Id) ||
                followups is not IActionBatchSink { PendingCount: 0 })
                return new(CommandOutcome.Rejected, "No matching calculated battle deck FLIP.");
            if (!duel.ValidateBattleReveal(reserved, faceDown: false, allowEnded: true))
                throw new InvalidOperationException("Reserved battle FLIP changed before deck flow.");

            var finish = new FinishBattleFlipCommand(command.Side, command.Defender);
            if (duel.GameOver || duel.FlipEffectsNegated)
                followups.Enqueue(finish);
            else
            {
                var result = ResolveFlipDeckFlow(command.Side, reserved.Defender.Card, finish, followups);
                if (result.Outcome != CommandOutcome.Applied)
                    throw new InvalidOperationException("Reserved battle FLIP could not start deck flow.");
            }
            reserved.FlipOpened = true;
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveFlipDeckFlow(int controller, Card card, DuelCommand finish, IActionCommandSink followups)
        {
            if (resolvingFlipBatch || resolvingJar || flipDeckFlow is not null ||
                followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Deck flow needs isolated resolution.");
            if (card.Id == FlipEffectRules.MorphingJar) return ResolveMorphingJar(controller, finish, queue);
            bool mill = card.Id == FlipEffectRules.NeedleWorm;
            int side = 1 - controller, requested = mill ? 5 : 3;
            var expected = new List<CardTarget>();
            for (int i = 0; i < Math.Min(requested, duel.decks[side].Count); i++)
            {
                if (!duel.TryCaptureCardTarget(side, DuelZone.Deck, i, out var target))
                    return new(CommandOutcome.Rejected, "Untracked deck occurrence.");
                expected.Add(target);
            }
            var flow = new FlipDeckFlow(card.Id, controller, requested, expected);
            flipDeckFlow = flow;
            resolvingFlipBatch = finish is FinishFlipBatchCommand;
            queue.EnqueueBatch(expected.Select(target => mill
                ? (DuelCommand)new SendToGraveyardCommand(side, target)
                : new DrawCommand(side)));
            queue.ScheduleContinuation(_ =>
            {
                // Read-only continuation: only successfully drawn Spell copies
                // participate; never select matching resource IDs in old hand.
                return new([.. flow.NewSpells.Select(t => (DuelCommand)new DiscardCommand(side, t)),
                    new FinishFlipDeckFlowCommand(controller), finish]);
            });
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveMorphingJar(int controller, DuelCommand finish, IActionBatchSink queue)
        {
            var commands = new List<DuelCommand>();
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < duel.hands[side].Count; index++)
                {
                    if (!duel.TryCaptureCardTarget(side, DuelZone.Hand, index, out var target))
                        throw new InvalidOperationException("Reserved Morphing Jar hand changed.");
                    commands.Add(new DiscardCommand(side, target));
                }
            bool deckOut0 = duel.decks[0].Count < 5, deckOut1 = duel.decks[1].Count < 5;
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < Math.Min(5, duel.decks[side].Count); index++)
                    commands.Add(new DrawCommand(side));
            queue.EnqueueBatch(commands);
            resolvingJar = true;
            resolvingFlipBatch = finish is FinishFlipBatchCommand;
            queue.ScheduleContinuation(_ => new([new FinishMorphingJarCommand(controller, deckOut0, deckOut1), finish]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishMorphingJar(FinishMorphingJarCommand command)
        {
            if (!resolvingJar) return new(CommandOutcome.Rejected, "No pending Morphing Jar batch.");
            resolvingJar = false;
            if (!duel.GameOver)
            {
                if (command.DeckOut0 && command.DeckOut1) duel.IsDraw = true;
                else if (command.DeckOut0) duel.Winner = 1;
                else if (command.DeckOut1) duel.Winner = 0;
            }
            return new(CommandOutcome.Applied);
        }

        private CommandResult FinishFlipDeckFlow(FinishFlipDeckFlowCommand command)
        {
            if (flipDeckFlow is not { } flow || flow.Controller != command.Side)
                return new(CommandOutcome.Rejected, "No matching FLIP deck flow.");
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

        private CommandResult OpenBattleFlipDraw(OpenBattleFlipDrawCommand command,IActionCommandSink followups)
        {
            if (duel.queuedBattleReveal is not { FlipCalculated:true, FlipOpened:false } reserved ||
                reserved.Defender.Target!=command.Defender || queueDrawDecision is not null ||
                followups is not IActionBatchSink { PendingCount:0 })
                return new(CommandOutcome.Rejected,"No matching calculated battle FLIP.");
            if (!duel.ValidateBattleReveal(reserved,faceDown:false,allowEnded:true))
                throw new InvalidOperationException("Reserved battle FLIP changed before decision.");
            reserved.FlipOpened=true;
            var finish=new FinishBattleFlipCommand(command.Side,command.Defender);
            // No optional draw when damage has ended the duel or deck is empty.
            if (duel.GameOver || duel.FlipEffectsNegated || duel.DeckCount(command.Side)==0)
            {
                followups.Enqueue(finish);
                return new(CommandOutcome.Applied);
            }
            var request=new PendingEffectDecision(++duel.nextEffectDecisionId,command.Side,
                reserved.Defender.Card,EffectDecisionKind.DrawOneOnFlip,0,duel.Turn);
            queueDrawDecision=request;
            followups.ScheduleContinuation(_ => new([],answer =>
            {
                var selected=answer ?? throw new InvalidOperationException("Missing battle FLIP answer.");
                return new([new RecordFlipDrawDecisionCommand(command.Side,request.Id,selected)],
                    _ => new([finish]));
            },request));
            return new(CommandOutcome.Applied);
        }

        private CommandResult RecordFlipDrawDecision(RecordFlipDrawDecisionCommand decision, IActionCommandSink followups)
        {
            if (queueDrawDecision is not { } request || request.Id != decision.DecisionId || request.Owner != decision.Side)
                return new(CommandOutcome.Rejected, "No matching queued draw decision.");

            if (duel.queuedBattleReveal is { FlipCalculated:true, FlipOpened:true } battle &&
                !duel.ValidateBattleReveal(battle,faceDown:false))
                throw new InvalidOperationException("Reserved battle FLIP changed before answer.");
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
            resolvingFlipBatch = false;
            if (jar2 is { } excavation)
            {
                jar2 = null;
                duel.LastMorphingJar2 = Array.AsReadOnly(Enumerable.Range(0,2).Select(p => new DemoExcavationResult(
                    p,excavation.Returned[p],excavation.Summoned[p]+excavation.Sent[p],excavation.Found[p],
                    excavation.Summoned[p],excavation.Sent[p],excavation.Found[p]<excavation.Returned[p])).ToArray());
                // The original FLIP source may now be in another zone or
                // face-down again. Preserve the original position event.
                FinishQueuedManualPosition(excavation.Change,resolveFlip:false,out var jar2Detail);
                return new(CommandOutcome.Applied,jar2Detail);
            }
            if (flipBanish is { } burn)
            {
                flipBanish = null;
                duel.LastFlipBanishBurn = new(burn.Effect, burn.Owner, burn.Source, burn.Required,
                    Array.AsReadOnly(burn.Removed.ToArray()), burn.DamageApplied ? FlipBanishBurnRules.BurnDamage : 0, burn.DamageApplied);
            }
            if (!duel.TryResolveCardTarget(finish.Target, out int slot, out var card))
                return new(CommandOutcome.Skipped, "FLIP source gone.");
            var change = new DemoMonsterPositionChange(finish.Side, slot, card, true, finish.WasInDefense,
                duel.MonsterFaceDown[finish.Side, slot], duel.DefensePosition[finish.Side, slot]);
            FinishQueuedManualPosition(change, resolveFlip: false, out var detail);
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
            if (!card.IsMonster || NormalSummonRules.IsFlipOnlySummon(card) ||
                duel.Monsters[summon.Side, summon.Slot] is not null)
                return new(CommandOutcome.Rejected, "Special Summon requires a monster and a free slot.");
            if (summon.Source.Zone == DuelZone.Hand)
            {
                bool paidInherentSummon = banishSummon is { } paidSummon && paidSummon.Source == summon.Source &&
                    paidSummon.Slot == summon.Slot && paidSummon.Defense == summon.FaceUpDefense &&
                    banishSummonPaid == GraveyardBanishSummonRules.QueuedRequiredCount(card) && banishSummonCosts.Count == 0;
                if (summon.FaceDownDefense || !(paidInherentSummon || IsPaidRitualSummon(summon)))
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
            duel.ContinueQueuedGraveyardOrDeckSelection();
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

            var battle = duel.HasQueuedBattleRecoveryChoice ? duel.queuedBattleReveal : null;
            ReserveGraveyardTransfer(answer.Side, movement, queue);
            if (battle is not null)
                followups.ScheduleContinuation(_ => new([new FinishBattleFlipCommand(answer.Side, battle.Defender.Target)]));
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
            duel.ContinueQueuedGraveyardOrDeckSelection();
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveQueuedMonsterTargets(CardTarget source, IReadOnlyList<CardTarget> targets,
            DemoMonsterEffectTrigger trigger, IActionCommandSink followups)
        {
            if (followups is not IActionBatchSink { PendingCount: 0 } queue ||
                wipe is not null || finishScheduled || duel.PendingFlipTarget is not { } pending ||
                pending.Trigger != trigger || !duel.CanAdmitQueuedMonsterFlipAnswer())
                return new(CommandOutcome.Rejected, "No matching queued monster selection.");
            if (!duel.TryPlanQueuedMonsterFlipAnswer(source, targets, out var batch, out var plans))
                return new(CommandOutcome.Rejected, "Monster effect source or target choice no longer valid.");

            var battle = duel.HasQueuedBattleTargetChoice ? duel.queuedBattleReveal : null;
            if (battle is not null)
            {
                if (pending.Effect == DemoFlipEffect.ChangeFaceUpPosition)
                    battle.ExpectedPositionChange = batch.OfType<ChangePositionByFlipCommand>().SingleOrDefault();
                else if (MovesMonsterFlipTarget(pending))
                {
                    var destination = IsBounce(pending) ? DuelZone.Hand : DuelZone.Graveyard;
                    foreach (var target in targets) battle.ExpectedEffectMoves.Add(target, destination);
                }
            }
            // Clear only this pending interaction inside its command, not at
            // admission. The reserved queued battle owns its continuation.
            duel.PendingFlipTarget = null;
            duel.resumedSummonMonsterFlip = null;
            duel.flipSelections.Clear();
            queue.EnqueueBatch(batch);
            if (MovesMonsterFlipTarget(pending))
            {
                foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
                linkedRemovalBatch = true;
                ScheduleFinish(source.Side, followups);
            }
            if (battle is not null)
                followups.ScheduleContinuation(_ => new([new FinishBattleFlipCommand(source.Side,battle.Defender.Target)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult ApplyFlipPositionChange(ChangePositionByFlipCommand command)
        {
            if (!duel.TryResolveCardTarget(command.Target, out int slot, out _) ||
                duel.MonsterFaceDown[command.Side, slot] || duel.DefensePosition[command.Side, slot] == command.Defense)
                return new(CommandOutcome.Skipped, "FLIP position target is no longer eligible.");

            var change = duel.CommitManualPosition(command.Side, slot, countsAsManualChange: false);
            if (command.FromPositionSpell)
                duel.FinishManualPosition(change, resolveFlip: false, out _);
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

        private CommandResult DestroyMonster(CardTarget target, IActionCommandSink followups,
            RemovalCause cause = RemovalCause.EffectDestruction)
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
            try { duel.RemoveMonster(target.Side, slot, queueLinkedConsequences: true,
                removalCause: cause); }
            finally { duel.deferringFieldRemovalTriggers = false; }
            if (wipe is { } current && current.Targets.Remove(target))
            {
                current.Counts[target.Side]++;
                if (hidden) current.FaceDown++;
            }
            ScheduleFinish(target.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult DestroyQueuedSpellTrap(CardTarget target,
            RemovalCause cause = RemovalCause.LinkedCleanup)
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
            int removedZone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
            bool wasHidden = duel.IsSpellTrapFaceDown(target.Side, removedZone);
            // Primary selected targets and direct wipes are effect destruction.
            // Do not synthesize Break for dependent equipment/Board cleanup.
            bool directEffect = cause == RemovalCause.EffectDestruction && reserved && wipe is { } removal &&
                (removal.PrimaryTarget is null || removal.PrimaryTarget == target) &&
                (removal.FlipPair is not { } directPair || directPair.Contains(target));
            duel.DestroySpellTrapCard(target.Side, removedZone);
            if (directEffect)
                duel.SpellTrapDestroyed?.Invoke(new(target.Side, removedZone, card, wasHidden));
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

        private CommandResult FinishRemoval(IActionCommandSink followups)
        {
            if (!finishScheduled) return new(CommandOutcome.Rejected, "No field removal to finalize.");
            finishScheduled = false;
            linkedRemovalBatch = false;
            plannedLinks.Clear();
            bool summonResponse = wipe?.Summon is not null;
            if (wipe is { } current)
            {
                if (current.SourceSpent)
                {
                    if (current.MonsterWipe && current.ResourceId != SummonTrapRules.TrapHole)
                        duel.LastMonsterWipe = new(current.ResourceId, current.Caster,
                            current.Counts[0], current.Counts[1], current.FaceDown);
                    if (current.Summon is { } summon)
                        duel.LastSummonTrap = new(current.ResourceId, current.Caster, summon.Player,
                            summon.Kind, current.Counts[0] + current.Counts[1]);
                    if (current.Attack is { } attack)
                        duel.LastAttackTrap = new(current.ResourceId, current.Caster, attack.AttackingPlayer,
                            current.Counts[0] + current.Counts[1], AttackContinued: false);
                }
                wipe = null;
            }
            // No search or optional prompt opens between the destruction commands.
            if (summonResponse)
                ContinueAfterSummonResponse(followups);
            else
                duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }
    }
}
