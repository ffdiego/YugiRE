namespace Joey.Core;

public enum CpuStepStatus { Acted, WaitingForOpponent, GameOver, Blocked }
public enum CpuAction { None, Draw, Summon, Set, Spell, Equip, Position, BeginBattle, Attack, Selection, Response, Decision, Discard, EndTurn, EndBattle }
public sealed record CpuStep(CpuStepStatus Status, CpuAction Action, int Player, string Message,
    Card? Card = null, int? SourceZone = null, int? TargetZone = null, int? HandIndex = null,
    IReadOnlyList<int>? AdditionalSpentHandIndices = null, int? FieldOwner = null);

/// <summary>
/// Deterministic alpha strategy, NOT a reproduction of Joey's native selector.
/// One Step commits or admits one engine action. The host drives the attached queue.
/// Responds to its own choices even during the opponent's turn. Create per duel/side.
/// </summary>
public sealed class CpuDuelController
{
    public int Player { get; }
    public const int TacticalActionLimit = 48;
    public const int MaxActionsPerTurn = 256;
    public int ActionsThisTurn { get; private set; }
    private int observedTurn = -1;
    private int tacticalActions;
    private DemoDuel? observedDuel;

    public CpuDuelController(int player)
    {
        if (player is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(player));
        Player = player;
    }

    private static (CardTarget Source, CardTarget Target)? FindFissure(DemoDuel duel, int player)
    {
        foreach (var zone in new[] { DuelZone.Hand, DuelZone.SpellTrap })
        {
            int count = zone == DuelZone.Hand ? duel.Hand.Count : 5;
            for (int index = 0; index < count; index++)
            {
                Card? card = zone == DuelZone.Hand ? duel.Hand[index] : duel.Spells[player, index];
                if (card is not { ClassCode: 22, Id: TargetedMonsterSpellRules.Fissure } ||
                    !duel.TryCaptureCardTarget(player, zone, index, out var source)) continue;
                // Deterministic CPU tie choice. The selected occurrence still
                // passes the same legality checks as a player's choice.
                if (duel.QueuedTargetedMonsterSpellCandidates(source).FirstOrDefault() is { } choice)
                    return (source, choice.Target);
            }
        }
        return null;
    }

    /// <summary>Who owns the current blocking choice; null means ordinary turn play.</summary>
    public static int? PendingOwner(DemoDuel duel) =>
        duel.PendingEffectDecision?.Owner ?? duel.PendingCardSelection?.Owner ??
        duel.PendingFlipSpellTrapTargets?.Owner ??
        duel.PendingFlipTarget?.Owner ?? duel.PendingSpellTrapTarget?.Owner ?? duel.PendingFusionSummon?.Owner ??
        (duel.PendingSummonResponse is { } summon ? summon.EligibleTraps[0].Owner :
        duel.PendingAttackResponse is { } attack ? attack.EligibleTraps[0].Owner : null);

    private delegate bool Operation(out string message);

    // A CPU-owned queue decision must not wait for a human preference or click.
    public static EffectDecisionAnswer DecideOptionalEffect(DemoDuel duel, PendingEffectDecision request) =>
        request.Kind == EffectDecisionKind.DrawOneOnFlip && duel.DeckCount(request.Owner) > 0
            ? EffectDecisionAnswer.Accept : EffectDecisionAnswer.Decline;

    // Historical source compatibility only: false cannot select an executor.
    [Obsolete("Use Step(duel); the typed action queue is mandatory.")]
    public CpuStep Step(DemoDuel duel, bool queueTurnActions) =>
        queueTurnActions ? Step(duel) : new(CpuStepStatus.Blocked, CpuAction.None, Player,
            "Legacy CPU execution has been removed.");

    public CpuStep Step(DemoDuel duel)
    {
        ArgumentNullException.ThrowIfNull(duel);
        if (!duel.HasActionQueue)
            return new(CpuStepStatus.Blocked, CpuAction.None, Player, "Attach the duel action queue before stepping the CPU.");
        if (!ReferenceEquals(duel, observedDuel) || observedTurn != duel.Turn)
        {
            observedDuel = duel; observedTurn = duel.Turn;
            ActionsThisTurn = tacticalActions = 0;
        }
        CpuStep Idle(CpuStepStatus status, string message) => new(status, CpuAction.None, Player, message);
        if (duel.GameOver) return Idle(CpuStepStatus.GameOver, "Duel finished.");
        if (duel.ActionQueueBusy) return Idle(CpuStepStatus.Blocked, "Waiting for the current action to finish.");
        int? pendingOwner = PendingOwner(duel);
        if (pendingOwner is int owner && owner != Player || pendingOwner is null && duel.ActivePlayer != Player)
            return Idle(CpuStepStatus.WaitingForOpponent, "Waiting for opponent.");
        if (ActionsThisTurn >= MaxActionsPerTurn)
            return Idle(CpuStepStatus.Blocked, "CPU safety limit reached; unsupported state. Reset the duel.");

        CpuStep Act(CpuAction action, Operation operation, Card? card = null,
            int? source = null, int? target = null, int? handIndex = null,
            IReadOnlyList<int>? additionalSpentHandIndices = null, int? fieldOwner = null)
        {
            if (!operation(out string message))
                return new(CpuStepStatus.Blocked, action, Player, "CPU action rejected: " + message);
            ActionsThisTurn++;
            if (pendingOwner is null) tacticalActions++;
            // A set card's identity is private, even in the public action log.
            return new(CpuStepStatus.Acted, action, Player,
                action switch
                {
                    CpuAction.Set => "Set a card face-down.",
                    CpuAction.Summon => $"Summoned {card!.Name}." +
                        (duel.PendingSummonResponse is null ? "" : " Trap response available."),
                    _ => message
                }, card, source, target, handIndex, additionalSpentHandIndices, fieldOwner);
        }

        if (duel.PendingEffectDecision is not null)
            return Idle(CpuStepStatus.Blocked, "Legacy effect decision is unsupported.");
        if (duel.PendingSummonResponse is { } summon)
        {
            var trap = summon.EligibleTraps.FirstOrDefault(t => t.Owner == Player &&
                (t.Trap.Id == SummonTrapRules.TrapHole ||
                 BoardPower(duel, 1 - Player) > BoardPower(duel, Player)));
            return Act(CpuAction.Response, (out string m) =>
                duel.TryQueueSummonResponse(summon, trap, out m, ActionOrigin.Cpu));
        }
        if (duel.PendingAttackResponse is { } attack)
        {
            var trap = attack.EligibleTraps.FirstOrDefault(t => t.Owner == Player);
            return Act(CpuAction.Response, (out string m) =>
                duel.TryQueueAttackResponse(attack, trap, out m, ActionOrigin.Cpu));
        }
        if (duel.PendingSpellTrapTarget is not null)
            return Idle(CpuStepStatus.Blocked, "Legacy Spell/Trap selection is unsupported.");
        if (duel.PendingFusionSummon is { } fusion)
        {
            var option = fusion.Options.MaxBy(option => option.Result.Attack)!;
            return Act(CpuAction.Spell, (out string m) => duel.TryQueueFusionSummon(
                fusion, option.DeckIndex, option.SuggestedMaterials, out m, origin: ActionOrigin.Cpu));
        }
        if (duel.PendingFlipSpellTrapTargets is { } twoSpellTargets)
        {
            if (duel.UsesQueuedFlipSpellTrapSelection(twoSpellTargets))
            {
                if (duel.FlipEffectsNegated)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TrySelectQueuedFlipSpellTrapTarget(twoSpellTargets, -1, -1, out m, ActionOrigin.Cpu));
                var queuedChoice = duel.QueuedFlipSpellTrapCandidates(twoSpellTargets)
                    .Where(c => !duel.SelectedFlipSpellTrapTargets.Any(s =>
                        s.Player == c.Target.Side && s.Zone == c.Zone))
                    .OrderBy(c => c.Target.Side == Player ? 1 : 0)
                    .ThenBy(c => c.Zone).FirstOrDefault();
                return queuedChoice is null
                    ? Idle(CpuStepStatus.Blocked, "Mandatory queued Spell/Trap pair has no candidate.")
                    : Act(CpuAction.Selection, (out string m) => duel.TrySelectQueuedFlipSpellTrapTarget(
                        twoSpellTargets, queuedChoice.Target.Side, queuedChoice.Zone, out m, ActionOrigin.Cpu));
            }
            return Idle(CpuStepStatus.Blocked, "Unsupported Spell/Trap selection.");
        }
        if (duel.PendingCardSelection is { } selection)
        {
            var candidate = selection.EligibleCards
                .Where(c => !duel.SelectedBatchIndices.Contains(c.SourceIndex))
                .OrderByDescending(c => CardValue(c.Card)).ThenBy(c => c.SourceIndex).FirstOrDefault();
            if (duel.UsesQueuedHandSelection(selection))
            {
                if (candidate is not null || selection.MinSelections == 0)
                    return Act(CpuAction.Selection, (out string m) => duel.TrySubmitQueuedHandSelection(
                        selection, candidate?.SourceIndex, out m, ActionOrigin.Cpu), candidate?.Card);
                return Idle(CpuStepStatus.Blocked, "Mandatory queued hand selection has no candidate.");
            }
            if (duel.UsesQueuedFieldSelection(selection))
            {
                int free = Enumerable.Range(0, 5).Count(z => duel.Monsters[Player, z] is null);
                var choices = selection.EligibleCards.OrderByDescending(c => CardValue(c.Card))
                    .ThenBy(c => c.SourceIndex).Take(Math.Min(free, selection.MaxSelections)).ToArray();
                var targets = new List<CardTarget>();
                foreach (var choice in choices)
                {
                    if (!duel.TryCaptureQueuedFieldSelection(selection, choice.SourceIndex, out var target))
                        return Idle(CpuStepStatus.Blocked, "Queued field selection occurrence changed.");
                    targets.Add(target);
                }
                bool set = selection.MaySelectFaceDownDefense && choices.Length == 1 &&
                    choices[0].Card.Defense > choices[0].Card.Attack;
                return Act(CpuAction.Selection, (out string m) => duel.TrySubmitQueuedFieldSelection(
                    selection, targets, set, out m, ActionOrigin.Cpu), choices.FirstOrDefault()?.Card);
            }
            return Idle(CpuStepStatus.Blocked, "Unsupported card selection.");
        }
        if (duel.PendingFlipTarget is { } flip)
        {
            if (duel.UsesQueuedMonsterFlipSelection(flip))
            {
                if (flip.Trigger == DemoMonsterEffectTrigger.Flip && duel.FlipEffectsNegated)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TryFinishQueuedMonsterFlipSelection(flip, out m, ActionOrigin.Cpu));
                var choice = duel.QueuedMonsterFlipCandidates()
                    .Where(c => !duel.SelectedFlipTargets.Any(t => t.Player == c.Target.Side && t.Zone == c.Zone))
                    .Where(c => flip.Effect != DemoFlipEffect.ReturnUpToTwo || c.Target.Side != Player)
                    .OrderByDescending(c => c.Target.Side != Player)
                    .ThenByDescending(c => c.Target.Side == Player ? -KnownPower(duel, Player, c.Zone) :
                        KnownPower(duel, c.Target.Side, c.Zone))
                    .ThenBy(c => c.Zone).FirstOrDefault();
                if (choice is not null)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TrySelectQueuedMonsterFlipTarget(flip, choice.Target.Side, choice.Zone, out m, ActionOrigin.Cpu));
                if (flip.Effect == DemoFlipEffect.ReturnUpToTwo)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TryFinishQueuedMonsterFlipSelection(flip, out m, ActionOrigin.Cpu));
                return Idle(CpuStepStatus.Blocked, "Mandatory queued FLIP has no legal target.");
            }
            return Idle(CpuStepStatus.Blocked, "Unsupported monster selection.");
        }
        bool DrawTurnCard(out string pending) => duel.TryQueueTurnDraw(out pending, ActionOrigin.Cpu);
        bool EnterBattle(out string pending) => duel.TryQueueBattlePhaseChange(true, out pending, ActionOrigin.Cpu);
        bool LeaveBattle(out string pending) => duel.TryQueueBattlePhaseChange(false, out pending, ActionOrigin.Cpu);
        bool EndTurn(out string pending) => duel.TryQueueEndTurn(out pending, ActionOrigin.Cpu);
        bool AttackFrom(int source, int target, out string pending) =>
            duel.TryQueueAttack(source, target, out pending, ActionOrigin.Cpu);

        if (duel.PendingEndDiscards > 0)
        {
            int discard = Enumerable.Range(0, duel.Hand.Count).OrderBy(i => CardValue(duel.Hand[i])).ThenBy(i => i).First();
            return Act(CpuAction.Discard, (out string m) => duel.TryQueueEndTurnDiscard(discard, out m, ActionOrigin.Cpu));
        }
        if (tacticalActions >= TacticalActionLimit)
            return Act(CpuAction.EndTurn, EndTurn);
        if (duel.CanDraw) return Act(CpuAction.Draw, DrawTurnCard);
        if (duel.Phase == DemoPhase.Main)
        {
            int gate = Enumerable.Range(0, 2).FirstOrDefault(duel.CanUseFusionGate, -1);
            if (gate >= 0)
                return Act(CpuAction.Selection, (out string m) => duel.BeginFusionGate(gate, out m));
            int gateInHand = Enumerable.Range(0, duel.Hand.Count)
                .FirstOrDefault(i => duel.Hand[i].Id == FusionRules.FusionGate, -1);
            if (gateInHand >= 0 && duel.HasFusionCombination() &&
                duel.CanQueueFixedSpellActivation(DuelZone.Hand, gateInHand))
                return Act(CpuAction.Spell, (out string m) => duel.TryQueueActivation(gateInHand, false, out m, ActionOrigin.Cpu), duel.Hand[gateInHand],
                    handIndex: gateInHand, fieldOwner: Player);
            {
                // Preserve the existing LP/draw/S/T-clear priorities. The
                // typed producer owns all movement and its presentation.
                foreach (var zone in new[] { DuelZone.Hand, DuelZone.SpellTrap })
                {
                    int count = zone == DuelZone.Hand ? duel.Hand.Count : 5;
                    for (int i = 0; i < count; i++)
                    {
                        var candidate = zone == DuelZone.Hand ? duel.Hand[i] : duel.Spells[Player, i];
                        if (candidate is not { ClassCode: 22 }) continue;
                        var lp = LifePointSpellRules.ResolveQueued(candidate.Id, Player);
                        bool useLpSpell = lp is { } change && (duel.Life[Player] > -change.CasterDelta ||
                            change.JointDamage && duel.Life[1 - Player] <= -change.OpponentDelta);
                        if (!(useLpSpell ||
                              duel.ShouldPlayAttackRestrictionSpell(Player, candidate) ||
                              ShouldClearSpellTraps(duel, candidate.Id,
                                  excludedSlot: zone == DuelZone.SpellTrap ? i : -1) ||
                              DrawSpellRules.Effect(candidate) is { } draw && duel.DeckCount(Player) > draw.Cards ||
                              DrawSpellRules.IsCardDestruction(candidate) &&
                                  duel.DeckCount(1 - Player) < duel.HandFor(1 - Player).Count) ||
                            !duel.CanQueueFixedSpellActivation(zone, i)) continue;
                        int index = i;
                        return Act(CpuAction.Spell, (out string m) =>
                            duel.TryQueueActivation(index, zone == DuelZone.SpellTrap, out m, ActionOrigin.Cpu));
                    }
                }
            }
            if (FindFissure(duel, Player) is { } fissure)
                return Act(CpuAction.Selection, (out string m) =>
                    duel.TryQueueTargetedMonsterSpell(fissure.Source, fissure.Target, out m, ActionOrigin.Cpu));
            {
                int searchInHand = Enumerable.Range(0, duel.Hand.Count)
                    .FirstOrDefault(i => duel.CanQueueDeckSearchSpell(DuelZone.Hand, i), -1);
                if (searchInHand >= 0)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TryQueueDeckSearchSpell(DuelZone.Hand, searchInHand, out m, ActionOrigin.Cpu));
                int searchSet = Enumerable.Range(0, 5)
                    .FirstOrDefault(i => duel.CanQueueDeckSearchSpell(DuelZone.SpellTrap, i), -1);
                if (searchSet >= 0)
                    return Act(CpuAction.Selection, (out string m) =>
                        duel.TryQueueDeckSearchSpell(DuelZone.SpellTrap, searchSet, out m, ActionOrigin.Cpu));
            }
            int fusionSpell = Enumerable.Range(0, duel.Hand.Count)
                .FirstOrDefault(i => duel.CanBeginQueuedFusionSelection(DuelZone.Hand, i), -1);
            if (fusionSpell >= 0)
                return Act(CpuAction.Selection, (out string m) => duel.TryBeginQueuedFusionSelection(DuelZone.Hand, fusionSpell, out m));
            {
                foreach (var zone in new[] { DuelZone.Hand, DuelZone.SpellTrap })
                {
                    int count = zone == DuelZone.Hand ? duel.Hand.Count : 5;
                    for (int i = 0; i < count; i++)
                    {
                        var candidate = zone == DuelZone.Hand ? duel.Hand[i] : duel.Spells[Player, i];
                        if (candidate is not { ClassCode: 22 } ||
                            !SpellTrapTargetRules.SupportsQueued(candidate.Id) ||
                            !duel.TryCaptureCardTarget(Player, zone, i, out var source)) continue;
                        var target = duel.QueuedSpellTrapTargetCandidates(source)
                            .FirstOrDefault(c => c.Target.Side != Player);
                        if (target is not null)
                            return Act(CpuAction.Selection, (out string m) =>
                                duel.TryQueueTargetedSpellTrap(source, target.Target, out m, ActionOrigin.Cpu));
                    }
                }
            }
            if (duel.SuggestQueuedRitualSummon() is { } ritual)
                return Act(CpuAction.Spell, (out string m) => duel.TryQueueRitualSummon(
                    ritual.Spell, ritual.Monster, ritual.Slot, ritual.Materials, ritual.Defense, out m, ActionOrigin.Cpu));

            for (int z = 0; z < 5; z++)
            {
                if (!duel.CanChangePosition(z)) continue;
                // Hidden opponent cards use a fixed prior, never their actual DAT stats.
                bool wantDefense = duel.AttackValue(Player, z) < MaxVisibleEnemyAttack(duel) &&
                    duel.DefenseValue(Player, z) > duel.AttackValue(Player, z);
                bool flipUseful = duel.MonsterFaceDown[Player, z] && FlipUseful(duel, duel.Monsters[Player, z]!);
                bool positionEffectUseful = !duel.MonsterFaceDown[Player, z] &&
                    !duel.DefensePosition[Player, z] &&
                    MonsterPositionEffectRules.TriggersOnAttackToDefense(duel.Monsters[Player, z]!.Id) &&
                    Enumerable.Range(0, 5).Any(e => duel.Monsters[1 - Player, e] is not null);
                if (positionEffectUseful || flipUseful ||
                    !duel.MonsterFaceDown[Player, z] && duel.DefensePosition[Player, z] != wantDefense ||
                    duel.MonsterFaceDown[Player, z] && !wantDefense && duel.AttackValue(Player, z) >= 1000)
                {
                    int zone = z;
                    return Act(CpuAction.Position, (out string m) => duel.TryQueueChangePosition(zone, out m, ActionOrigin.Cpu), duel.Monsters[Player, zone], zone);
                }
            }
            int summonIndex = Enumerable.Range(0, duel.Hand.Count).Where(duel.CanSummonFromHand)
                .Where(i => DemoDuel.NormalTributes(duel.Hand[i]) == 0 ||
                    duel.Hand[i].Attack > Enumerable.Range(0, 5).Where(z => duel.Monsters[Player, z] is not null)
                        .OrderBy(z => duel.AttackValue(Player, z)).Take(DemoDuel.NormalTributes(duel.Hand[i]))
                        .Sum(z => duel.AttackValue(Player, z)))
                .OrderByDescending(i => Math.Max(duel.Hand[i].Attack, duel.Hand[i].Defense) +
                    (FlipUseful(duel, duel.Hand[i]) ? 1400 : 0)).ThenBy(i => i).FirstOrDefault(-1);
            if (summonIndex >= 0)
            {
                var card = duel.Hand[summonIndex];
                int[] tributes = Enumerable.Range(0, 5).Where(z => duel.Monsters[Player, z] is not null)
                    .OrderBy(z => duel.AttackValue(Player, z)).ThenBy(z => z).Take(DemoDuel.NormalTributes(card)).ToArray();
                bool set = FlipEffectRules.Supports(card.Id) ||
                    NormalSummonRules.DestroysAfterNormalSummon(card) ||
                    card.Attack < MaxVisibleEnemyAttack(duel) && card.Defense > card.Attack;
                int destination = duel.FirstFreeMonsterZone(tributes);
                return Act(set ? CpuAction.Set : CpuAction.Summon,
                    (out string m) => duel.TryQueueNormalPlacement(summonIndex, destination, set, out m, tributes, ActionOrigin.Cpu),
                    card, target: destination, handIndex: summonIndex);
            }
            int specialSlot = duel.FirstFreeMonsterZone();
            if (specialSlot >= 0)
            {
                for (int index = 0; index < duel.Hand.Count; index++)
                {
                    Card card = duel.Hand[index];
                    int required = GraveyardBanishSummonRules.QueuedRequiredCount(card);
                    if (required == 0 || !duel.TryCaptureCardTarget(Player, DuelZone.Hand, index, out var source))
                        continue;
                    var candidates = duel.QueuedGraveyardBanishSummonCandidates(source);
                    if (candidates.Count == 0) continue;
                    var cost = candidates.Take(required).Select(candidate => candidate.Target).ToArray();
                    bool defense = card.Attack < MaxVisibleEnemyAttack(duel) && card.Defense > card.Attack;
                    return Act(CpuAction.Summon, (out string m) =>
                        duel.TryQueueGraveyardBanishSummon(source, specialSlot, cost, defense, out m, ActionOrigin.Cpu),
                        card, target: specialSlot, handIndex: index);
                }
            }
            // Preserve the CPU's preference for its own strongest compatible
            // monster. In queue mode the candidates include tracked Set sources.
            {
                foreach (var zone in new[] { DuelZone.Hand, DuelZone.SpellTrap })
                {
                    int count = zone == DuelZone.Hand ? duel.Hand.Count : 5;
                    for (int i = 0; i < count; i++)
                    {
                        var candidate = zone == DuelZone.Hand ? duel.Hand[i] : duel.Spells[Player, i];
                        if (candidate is not { ClassCode: 22 } || !EquipmentRules.SupportsQueued(candidate.Id) ||
                            !duel.TryCaptureCardTarget(Player, zone, i, out var source)) continue;
                        var target = duel.QueuedEquipmentCandidates(source).Where(c => c.Target.Side == Player)
                            .OrderByDescending(c => duel.AttackValue(Player, c.Slot)).ThenBy(c => c.Slot).FirstOrDefault();
                        if (target is not null)
                            return Act(CpuAction.Equip, (out string m) =>
                                duel.TryQueueEquipment(source, target.Target, out m, ActionOrigin.Cpu));
                    }
                }
            }
            int trapIndex = Enumerable.Range(0, duel.Hand.Count).FirstOrDefault(i =>
                (SummonTrapRules.Supports(duel.Hand[i].Id) || AttackTrapRules.Supports(duel.Hand[i].Id)) && duel.CanSetFromHand(i), -1);
            if (trapIndex >= 0)
                return Act(CpuAction.Set, (out string m) => duel.TryQueueSpellTrapSet(trapIndex, out m, ActionOrigin.Cpu), duel.Hand[trapIndex], target: duel.FirstFreeSpellZone(), handIndex: trapIndex);
            if (duel.CanBeginBattle) return Act(CpuAction.BeginBattle, EnterBattle);
        }
        if (duel.Phase == DemoPhase.Battle)
        {
            int enemy = 1 - Player;
            foreach (int source in Enumerable.Range(0, 5).Where(duel.CanAttack)
                .OrderByDescending(z => duel.AttackValue(Player, z)))
            {
                int power = duel.AttackValue(Player, source);
                var enemies = Enumerable.Range(0, 5).Where(z => duel.Monsters[enemy, z] is not null).ToArray();
                if ((power > 0 || enemies.Length == 0) && duel.CanAttackDirectly(source))
                {
                    var directAttempt = Act(CpuAction.Attack, (out string m) => AttackFrom(source, -1, out m),
                        duel.Monsters[Player, source], source, -1);
                    if (directAttempt.Status != CpuStepStatus.Blocked || !duel.HasDeclaredAttack(Player, source))
                        return directAttempt;
                }
                var targets = enemies.Select(z => (Zone: z,
                        Margin: !duel.MonsterFaceDown[enemy, z]
                            ? duel.QueuedTargetedBattlePower(Player, source, z).Difference
                            : power - KnownPower(duel, enemy, z)))
                    .Where(candidate => candidate.Margin > 0)
                    .OrderByDescending(candidate => !duel.MonsterFaceDown[enemy, candidate.Zone] &&
                        !duel.DefensePosition[enemy, candidate.Zone] ? candidate.Margin + 1000 : 500)
                    .ThenBy(candidate => candidate.Zone).Select(candidate => candidate.Zone);
                foreach (int target in targets)
                {
                    var targetedAttempt = Act(CpuAction.Attack, (out string m) => AttackFrom(source, target, out m),
                        duel.Monsters[Player, source], source, target);
                    if (targetedAttempt.Status != CpuStepStatus.Blocked || !duel.HasDeclaredAttack(Player, source))
                        return targetedAttempt;
                }
            }
            return Act(CpuAction.EndBattle, LeaveBattle);
        }
        return Act(CpuAction.EndTurn, EndTurn);
    }

    private bool ShouldClearSpellTraps(DemoDuel duel, ushort resourceId, int excludedSlot = -1)
    {
        if (!SpellTrapWipeRules.SupportsQueued(resourceId))
            return false;
        bool hitsOwnSide = SpellTrapWipeRules.HitsOwnSideQueued(resourceId);
        int own = hitsOwnSide
            ? SpellTrapWipeRules.CountOnSide(Player, duel.Spells, duel.FieldSpells, excludedSlot) : 0;
        return SpellTrapWipeRules.CountOnSide(1 - Player, duel.Spells, duel.FieldSpells) > own;
    }

    private static int CardValue(Card c) => c.IsMonster ? Math.Max(c.Attack, c.Defense) +
        (FlipEffectRules.Supports(c.Id) ? 800 : 0) : DrawSpellRules.Effect(c) is { Cards: 2 } ? 2400 : LifePointSpellRules.Supports(c.Id) ? 1800 :
        AttackTrapRules.Supports(c.Id) || SummonTrapRules.Supports(c.Id) ? 2000 : 100;
    private static int KnownPower(DemoDuel d, int p, int z) => d.MonsterFaceDown[p, z] ? 1200 :
        d.DefensePosition[p, z] ? d.DefenseValue(p, z) : d.AttackValue(p, z);
    private static int BoardPower(DemoDuel d, int p) => Enumerable.Range(0, 5)
        .Where(z => d.Monsters[p, z] is not null).Sum(z => KnownPower(d, p, z));
    private int MaxVisibleEnemyAttack(DemoDuel d) => Enumerable.Range(0, 5)
        .Where(z => d.Monsters[1 - Player, z] is not null)
        .Select(z => d.MonsterFaceDown[1 - Player, z] ? 1200 : d.AttackValue(1 - Player, z)).DefaultIfEmpty(0).Max();
    private bool FlipUseful(DemoDuel d, Card c) => c.Id switch
    {
        FlipEffectRules.ManEaterBug or FlipEffectRules.PenguinSoldier or FlipEffectRules.HaneHane =>
            Enumerable.Range(0, 5).Any(z => d.Monsters[1 - Player, z] is not null),
        FlipEffectRules.MagicianOfFaith => d.Graveyard(Player).Any(c => c.ClassCode == 22),
        FlipEffectRules.MaskOfDarkness => d.Graveyard(Player).Any(c => c.ClassCode == 21),
        FlipEffectRules.MorphingJar => d.HandFor(Player).Count < 3 && d.DeckCount(Player) >= 5,
        FlipEffectRules.NeedleWorm or FlipEffectRules.HirosShadowScout => true,
        FlipEffectRules.FourStarredLadybugOfDoom =>
            MonsterWipeRules.LevelFourOpponentTargets(Player, d.Monsters, d.MonsterFaceDown).Count > 0,
        FlipEffectRules.Skelengel => d.DeckCount(Player) > 0,
        FlipEffectRules.PrincessOfTsurugi => !d.FlipEffectsNegated &&
            SpellTrapWipeRules.CountOnSide(1 - Player, d.Spells, d.FieldSpells) > 0,
        FlipEffectRules.BubonicVermin =>
            Enumerable.Range(0, 5).Any(z => d.Monsters[Player, z] is null) &&
            d.DeckSnapshot(Player).Any(candidate => candidate.IsMonster && candidate.Id == c.Id),
        FlipEffectRules.LadyAssailantOfFlames => d.DeckCount(Player) >= 3,
        FlipEffectRules.FireSorcerer => d.HandFor(Player).Count >= 2,
        FlipEffectRules.ImmortalOfThunder => d.Life[Player] <= 5000,
        FlipEffectRules.Greenkappa or FlipEffectRules.TornadoBird =>
            FlipSpellTrapSelectionRules.Candidates(c.Id, d.Spells,
                d.SpellFaceDown, d.FieldSpells, d.FieldSpellFaceDown).Count >= 2,
        _ => false
    };
}
