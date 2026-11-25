namespace Joey.Core;

public enum CpuStepStatus { Acted, WaitingForOpponent, GameOver, Blocked }
public enum CpuAction { None, Draw, Summon, Set, Spell, Equip, Position, BeginBattle, Attack, Selection, Response, Decision, Discard, EndTurn, EndBattle }
public sealed record CpuStep(CpuStepStatus Status, CpuAction Action, int Player, string Message,
    Card? Card = null, int? SourceZone = null, int? TargetZone = null, int? HandIndex = null,
    IReadOnlyList<int>? AdditionalSpentHandIndices = null, int? FieldOwner = null);

/// <summary>
/// Deterministic alpha strategy, NOT a reproduction of Joey's native selector.
/// One Step commits or admits one engine action. The host drives any opted-in queue.
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

    /// <summary>Who owns the current blocking choice; null means ordinary turn play.</summary>
    public static int? PendingOwner(DemoDuel duel) =>
        duel.PendingEffectDecision?.Owner ?? duel.PendingCardSelection?.Owner ??
        duel.PendingFlipSpellTrapTargets?.Owner ??
        duel.PendingFlipTarget?.Owner ?? duel.PendingSpellTrapTarget?.Owner ?? duel.PendingFusionSummon?.Owner ??
        (duel.PendingSummonResponse is { } summon ? summon.EligibleTraps[0].Owner :
        duel.PendingAttackResponse is { } attack ? attack.EligibleTraps[0].Owner : null);

    private delegate bool Operation(out string message);

    public CpuStep Step(DemoDuel duel) => Step(duel, queueTurnActions: false);

    public CpuStep Step(DemoDuel duel, bool queueTurnActions)
    {
        ArgumentNullException.ThrowIfNull(duel);
        if (!ReferenceEquals(duel, observedDuel) || observedTurn != duel.Turn)
        {
            observedDuel = duel; observedTurn = duel.Turn;
            ActionsThisTurn = tacticalActions = 0;
        }
        CpuStep Idle(CpuStepStatus status, string message) => new(status, CpuAction.None, Player, message);
        if (duel.GameOver) return Idle(CpuStepStatus.GameOver, "Duel finished.");
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

        if (duel.PendingEffectDecision is { } decision)
        {
            var answer = decision.Kind == EffectDecisionKind.DrawOneOnFlip && duel.DeckCount(decision.Owner) > 0
                ? EffectDecisionAnswer.Accept : EffectDecisionAnswer.Decline;
            return Act(CpuAction.Decision, (out string m) => duel.ResolveEffectDecision(
                decision.Id, answer, out m));
        }
        if (duel.PendingSummonResponse is { } summon)
        {
            var trap = summon.EligibleTraps.FirstOrDefault(t => t.Owner == Player &&
                (t.Trap.Id == SummonTrapRules.TrapHole ||
                 BoardPower(duel, 1 - Player) > BoardPower(duel, Player)));
            return trap is null ? Act(CpuAction.Response, duel.PassSummonResponse) :
                Act(CpuAction.Response, (out string m) => duel.ActivateSummonTrap(Player, trap.Zone, out m), trap.Trap, source: trap.Zone);
        }
        if (duel.PendingAttackResponse is { } attack)
        {
            var trap = attack.EligibleTraps.FirstOrDefault(t => t.Owner == Player);
            return trap is null ? Act(CpuAction.Response, duel.PassAttackResponse) :
                Act(CpuAction.Response, (out string m) => duel.ActivateAttackTrap(Player, trap.Zone, out m), trap.Trap, source: trap.Zone);
        }
        if (duel.PendingSpellTrapTarget is { } pendingTarget)
        {
            var target = pendingTarget.EligibleTargets.FirstOrDefault(candidate => candidate.Player != Player);
            if (target is null) return Act(CpuAction.Selection, duel.CancelSpellTrapTarget);
            return Act(CpuAction.Spell, (out string m) => duel.ResolveSpellTrapTarget(target.Player, target.Zone, out m),
                pendingTarget.Spell,
                source: pendingTarget.FromSet ? pendingTarget.SourceIndex : duel.FirstFreeSpellZone(),
                handIndex: pendingTarget.FromSet ? null : pendingTarget.SourceIndex);
        }
        if (duel.PendingFusionSummon is { } fusion)
        {
            var option = fusion.Options.MaxBy(option => option.Result.Attack)!;
            int[] spent = option.SuggestedMaterials.Where(material => material.Zone == FusionMaterialZone.Hand)
                .Select(material => material.Index).ToArray();
            return Act(CpuAction.Spell,
                (out string m) => duel.ResolveFusionSummon(option.DeckIndex, option.SuggestedMaterials, out m),
                fusion.Spell, source: fusion.FieldOwner is null
                    ? fusion.FromSet ? fusion.SourceIndex : duel.FirstFreeSpellZone() : null,
                handIndex: fusion.FromSet || fusion.FieldOwner is not null ? null : fusion.SourceIndex,
                additionalSpentHandIndices: spent, fieldOwner: fusion.FieldOwner);
        }
        if (duel.PendingFlipSpellTrapTargets is { } twoSpellTargets)
        {
            if (queueTurnActions && duel.UsesQueuedFlipSpellTrapSelection(twoSpellTargets))
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
            // Prefer adverse targets, while allowing own-side selections when
            // the effect requires two cards across the entire field.
            var choice = twoSpellTargets.EligibleTargets
                .Where(t => !duel.SelectedFlipSpellTrapTargets.Any(s =>
                    s.Player == t.Player && s.Zone == t.Zone))
                .OrderBy(t => t.Player == Player ? 1 : 0)
                .ThenBy(t => t.Zone).FirstOrDefault();
            return choice is null
                ? Idle(CpuStepStatus.Blocked, "Mandatory two-target FLIP has no candidate.")
                : Act(CpuAction.Selection, (out string m) =>
                    duel.SelectFlipSpellTrapTarget(choice.Player, choice.Zone, out m));
        }
        if (duel.PendingCardSelection is { } selection)
        {
            var candidate = selection.EligibleCards
                .Where(c => !duel.SelectedBatchIndices.Contains(c.SourceIndex))
                .OrderByDescending(c => CardValue(c.Card)).ThenBy(c => c.SourceIndex).FirstOrDefault();
            if (queueTurnActions && duel.UsesQueuedHandSelection(selection))
            {
                if (candidate is not null || selection.MinSelections == 0)
                    return Act(CpuAction.Selection, (out string m) => duel.TrySubmitQueuedHandSelection(
                        selection, candidate?.SourceIndex, out m, ActionOrigin.Cpu), candidate?.Card);
                return Idle(CpuStepStatus.Blocked, "Mandatory queued hand selection has no candidate.");
            }
            if (queueTurnActions && duel.UsesQueuedFieldSelection(selection))
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
            if (selection.MaxSelections > 1)
            {
                int free = Enumerable.Range(0, 5).Count(z => duel.Monsters[Player, z] is null);
                if (candidate is not null && duel.SelectedBatchIndices.Count < free)
                    return Act(CpuAction.Selection, (out string m) => duel.SelectBatchCard(candidate.SourceIndex, out m));
                return Act(CpuAction.Selection, duel.FinishOptionalCardSelection);
            }
            if (candidate is not null)
                return Act(CpuAction.Selection, (out string m) => duel.ResolveCardSelection(candidate.SourceIndex, null,
                    selection.Destination == CardSelectionDestination.Field && selection.MaySelectFaceDownDefense &&
                    candidate.Card.Defense > candidate.Card.Attack, out m), candidate.Card);
            if (selection.MinSelections == 0)
                return Act(CpuAction.Selection, duel.SkipOptionalCardSelection);
            return Idle(CpuStepStatus.Blocked, "Mandatory card selection has no legal candidate.");
        }
        if (duel.PendingFlipTarget is { } flip)
        {
            bool bounce = flip.Effect is DemoFlipEffect.ReturnOne or DemoFlipEffect.ReturnUpToTwo;
            var targets = (from p in Enumerable.Range(0, 2)
                           from z in Enumerable.Range(0, 5)
                           where duel.Monsters[p, z] is not null &&
                               (!bounce || !duel.IsMarkedForBattleDestruction(p, z)) &&
                               (flip.Effect != DemoFlipEffect.DestroyOpponentMonster || p != flip.Owner) &&
                               !duel.SelectedFlipTargets.Any(t => t.Player == p && t.Zone == z)
                           orderby p != Player descending,
                               (p == Player ? -KnownPower(duel, p, z) : KnownPower(duel, p, z)) descending, z
                           select (Player: p, Zone: z)).ToArray();
            if (flip.Effect == DemoFlipEffect.ReturnUpToTwo &&
                !targets.Any(t => t.Player != Player))
                return Act(CpuAction.Selection, duel.FinishFlipTargets);
            if (targets.Length > 0)
                return Act(CpuAction.Selection, (out string m) => duel.ResolveFlipTarget(targets[0].Player, targets[0].Zone, out m));
            return Idle(CpuStepStatus.Blocked, "Mandatory FLIP has no legal target.");
        }
        bool DrawTurnCard(out string pending) => queueTurnActions
            ? duel.TryQueueTurnDraw(out pending, ActionOrigin.Cpu)
            : duel.Draw(out pending);
        bool EnterBattle(out string pending) => queueTurnActions
            ? duel.TryQueueBattlePhaseChange(true, out pending, ActionOrigin.Cpu)
            : duel.BeginBattle(out pending);
        bool LeaveBattle(out string pending) => queueTurnActions
            ? duel.TryQueueBattlePhaseChange(false, out pending, ActionOrigin.Cpu)
            : duel.EndBattle(out pending);
        bool EndTurn(out string pending) => queueTurnActions
            ? duel.TryQueueEndTurn(out pending, ActionOrigin.Cpu)
            : duel.EndTurn(out pending);

        if (duel.PendingEndDiscards > 0)
        {
            int discard = Enumerable.Range(0, duel.Hand.Count).OrderBy(i => CardValue(duel.Hand[i])).ThenBy(i => i).First();
            return Act(CpuAction.Discard, (out string m) => duel.HasQueuedEndDiscard
                ? duel.TryQueueEndTurnDiscard(discard, out m, ActionOrigin.Cpu)
                : duel.DiscardForEndTurn(discard, out m));
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
            if (gateInHand >= 0 && duel.HasFusionCombination())
                return Act(CpuAction.Spell, (out string m) => duel.Play(gateInHand, out m), duel.Hand[gateInHand],
                    handIndex: gateInHand, fieldOwner: Player);
            int spell = Enumerable.Range(0, duel.Hand.Count).FirstOrDefault(i =>
                duel.CanActivateFromHand(i) && (LifePointSpellRules.Supports(duel.Hand[i].Id) ||
                    ShouldClearSpellTraps(duel, duel.Hand[i].Id) ||
                    DrawSpellRules.Effect(duel.Hand[i]) is { } draw && duel.DeckCount(Player) > draw.Cards), -1);
            if (spell >= 0) return Act(CpuAction.Spell, (out string m) => duel.Play(spell, out m), duel.Hand[spell],
                source: duel.FirstFreeSpellZone(), handIndex: spell);
            int fusionSpell = Enumerable.Range(0, duel.Hand.Count)
                .FirstOrDefault(i => FusionRules.Supports(duel.Hand[i]) && duel.CanActivateFromHand(i), -1);
            if (fusionSpell >= 0)
                return Act(CpuAction.Selection, (out string m) => duel.Play(fusionSpell, out m));
            int targeted = Enumerable.Range(0, duel.Hand.Count).FirstOrDefault(i =>
                SpellTrapTargetRules.Supports(duel.Hand[i].Id) && duel.CanActivateFromHand(i) &&
                duel.SpellTrapTargets(duel.Hand[i].Id).Any(candidate => candidate.Player != Player), -1);
            if (targeted >= 0)
                return Act(CpuAction.Selection, (out string m) => duel.Play(targeted, out m));

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
                    return Act(CpuAction.Position, (out string m) => duel.ChangePosition(zone, out m), duel.Monsters[Player, zone], zone);
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
                    card.Attack < MaxVisibleEnemyAttack(duel) && card.Defense > card.Attack;
                return Act(set ? CpuAction.Set : CpuAction.Summon,
                    (out string m) => duel.SummonLeftmost(summonIndex, set, out m, tributes), card, target: duel.FirstFreeMonsterZone(tributes), handIndex: summonIndex);
            }
            // Equip only our own compatible monsters. Never sets an equipment it cannot activate.
            for (int i = 0; i < duel.Hand.Count; i++)
            {
                if (!EquipmentRules.Supports(duel.Hand[i].Id) || !duel.CanActivateFromHand(i)) continue;
                int target = Enumerable.Range(0, 5).Where(z => duel.Monsters[Player, z] is not null &&
                    duel.EquipmentEligibility(i, Player, z) is { IsSupported: true, Value: true })
                    .OrderByDescending(z => duel.AttackValue(Player, z)).FirstOrDefault(-1);
                if (target < 0) continue;
                int index = i;
                return Act(CpuAction.Equip, (out string m) => duel.Equip(index, Player, target, out m), duel.Hand[index],
                    source: duel.FirstFreeSpellZone(), target: target, handIndex: index);
            }
            int trapIndex = Enumerable.Range(0, duel.Hand.Count).FirstOrDefault(i =>
                (SummonTrapRules.Supports(duel.Hand[i].Id) || AttackTrapRules.Supports(duel.Hand[i].Id)) && duel.CanSetFromHand(i), -1);
            if (trapIndex >= 0)
                return Act(CpuAction.Set, (out string m) => duel.SetFromHand(trapIndex, out m), duel.Hand[trapIndex], target: duel.FirstFreeSpellZone(), handIndex: trapIndex);
            if (duel.CanBeginBattle) return Act(CpuAction.BeginBattle, EnterBattle);
        }
        if (duel.Phase == DemoPhase.Battle)
        {
            int enemy = 1 - Player;
            foreach (int source in Enumerable.Range(0, 5).Where(duel.CanAttack)
                .OrderByDescending(z => duel.AttackValue(Player, z)))
            {
                int power = duel.AttackValue(Player, source);
                if (power > 0 && duel.CanAttackDirectly(source))
                    return Act(CpuAction.Attack, (out string m) => duel.Attack(source, -1, out m),
                        duel.Monsters[Player, source], source, -1);
                var enemies = Enumerable.Range(0, 5).Where(z => duel.Monsters[enemy, z] is not null).ToArray();
                int target = enemies.Where(z => power > KnownPower(duel, enemy, z))
                    .OrderByDescending(z => !duel.MonsterFaceDown[enemy, z] && !duel.DefensePosition[enemy, z]
                        ? power - duel.AttackValue(enemy, z) + 1000 : 500)
                    .ThenBy(z => z).FirstOrDefault(-1);
                if (enemies.Length > 0 && target < 0) continue;
                return Act(CpuAction.Attack, (out string m) => duel.Attack(source, target, out m), duel.Monsters[Player, source], source, target);
            }
            return Act(CpuAction.EndBattle, LeaveBattle);
        }
        return Act(CpuAction.EndTurn, EndTurn);
    }

    private bool ShouldClearSpellTraps(DemoDuel duel, ushort resourceId)
    {
        if (!SpellTrapWipeRules.Supports(resourceId)) return false;
        int own = SpellTrapWipeRules.HitsOwnSide(resourceId)
            ? SpellTrapWipeRules.CountOnSide(Player, duel.Spells, duel.FieldSpells) : 0;
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
