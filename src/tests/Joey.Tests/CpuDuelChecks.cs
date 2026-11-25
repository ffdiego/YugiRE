using Joey.Core;
using Joey.LookAndFeel;

internal static class CpuDuelChecks
{
    private static Card Monster(ushort id, int attack = 1800, int defense = 1000) =>
        new(id + 2000, id, "Fixture " + id, "", (15u << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card Spell(ushort id, int type = 22) => new(id + 2000, id, "Fixture " + id, "", (uint)type << 20);
    private static Card?[] Slots(Card? first = null) => [first, null, null, null, null];
    private static DemoDuel State(int side, IReadOnlyList<Card> hand, Card? own = null, Card? enemy = null,
        IReadOnlyList<Card>? grave = null, IReadOnlyList<Card>? deck = null) =>
        DemoDuel.FromPartialObservation(side, hand, Slots(side == 0 ? own : enemy),
            Slots(side == 1 ? own : enemy), 8000, true,
            side == 0 ? grave : null, side == 1 ? grave : null,
            side == 0 ? deck : null, side == 1 ? deck : null);

    private static void QueuedFissure(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        {
            Card fissure = Spell(TargetedMonsterSpellRules.Fissure);
            Card?[] enemy = [Monster(24, 700), Monster(24, 700), Monster(1351, 900), Monster(24, 100), null];
            var duel = DemoDuel.FromPartialObservation(side, [fissure], side == 0 ? Slots() : enemy,
                side == 0 ? enemy : Slots(), 8000, side0Deck: [Monster(24)], side1Deck: [Monster(24)]);
            duel.MonsterFaceDown[1 - side, 3] = true;
            duel.DefensePosition[1 - side, 3] = true;
            if (fromSet) check(duel.SetFromHand(0, out _), "C12 Fissure fixture Set");
            check(duel.Draw(out _), "C12 Fissure fixture draw before Main");
            var queue = duel.CreatePrimitiveActionQueue();
            var cpu = new CpuDuelController(side);
            check(duel.TryCaptureCardTarget(1 - side, DuelZone.Monster, 0, out var first) &&
                duel.TryCaptureCardTarget(1 - side, DuelZone.Monster, 1, out var second) &&
                first.Occurrence != second.Occurrence, "C12 Fissure has two tracked minimum occurrences");

            check(cpu.Step(duel, queueTurnActions: true) is { Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
                queue.IsBusy && duel.Graveyard(side).Count == 0 && duel.Monsters[1 - side, 0] is not null,
                "C12 CPU chooses tied Fissure target without mutating during admission");
            var entry = queue.Trace.Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
            var presentation = duel.DescribeActionPresentation(entry.Command);
            check(entry.Origin == ActionOrigin.Cpu && presentation is { Kind: ActionPresentationKind.Activate } &&
                presentation.Source.Zone == (fromSet ? DuelZone.SpellTrap : DuelZone.Hand) &&
                presentation.SourceIndex == 0 && presentation.DestinationSlot == 0,
                "C12 activation presentation reads the same Fissure source for UI and CPU");
            for (int tick = 0; tick < 40 && queue.IsBusy; tick++) queue.Tick();
            check(!queue.IsBusy && queue.Fault is null && duel.Monsters[1 - side, 0] is null &&
                duel.Monsters[1 - side, 1] is not null && duel.Monsters[1 - side, 2] is not null &&
                duel.Monsters[1 - side, 3] is not null && duel.Graveyard(side).Single().Id == fissure.Id,
                "C12 CPU resolves exactly the chosen minimum through Fissure queue");
        }
    }

    private sealed class FixedSpellGate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    private static void QueuedFixedSpells(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        foreach (ushort id in new ushort[]
        {
            338, 345, LifePointSpellRules.BlueMedicine,
            DrawSpellRules.PotOfGreed, DrawSpellRules.UpstartGoblin,
            SpellTrapWipeRules.HarpiesFeatherDuster, SpellTrapWipeRules.HeavyStorm,
            SpellTrapWipeRules.GiantTrunade
        })
        {
            int enemy = 1 - side;
            Card spell = Spell(id), trap = Spell(1056, 21);
            Card[] deck = [spell, Monster(24), Monster(25), Monster(26), Monster(27)];
            var duel = DemoDuel.FromPartialObservation(enemy, [trap], Slots(), Slots(), 8000,
                equipmentContextResolved: true, side0Deck: side == 0 ? deck : null,
                side1Deck: side == 1 ? deck : null);
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.Draw(out _),
                "C12 fixed spell fixture: tracked enemy Set, draw source");
            if (fromSet) check(duel.SetFromHand(0, out _), "C12 fixed spell fixture Set");
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            check(!duel.CanQueueFixedSpellActivation(zone, 0), "No queue: fixed producer unavailable");
            var gate = new FixedSpellGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            if (fromSet)
            {
                duel.SpellFaceDown[side, 0] = false;
                check(!duel.CanQueueFixedSpellActivation(zone, 0), "Face-up source cannot activate again");
                duel.SpellFaceDown[side, 0] = true;
            }
            check(DemoDuel.SupportsQueuedFixedSpell(spell) &&
                duel.CanQueueFixedSpellActivation(zone, 0) && !queue.IsBusy &&
                queue.Trace.Count == 0 && duel.Graveyard(side).Count == 0 &&
                duel.DeckCount(side) == 4 && duel.Life.All(lp => lp == 8000),
                "C12 UI query uses tracked legality without mutation");
            var step = new CpuDuelController(side).Step(duel, queueTurnActions: true);
            check(step is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell, Card: null, HandIndex: null } &&
                queue.IsBusy && !duel.CanQueueFixedSpellActivation(zone, 0) &&
                duel.Graveyard(side).Count == 0 && duel.DeckCount(side) == 4 &&
                duel.Spells[enemy, 0] is not null && duel.Life.All(lp => lp == 8000),
                "C12 CPU admission only; no immediate mutation or duplicate legacy presentation metadata");
            var entry = queue.Trace.Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
            var preview = duel.DescribeActionPresentation(entry.Command);
            check(entry.Origin == ActionOrigin.Cpu && preview is { Kind: ActionPresentationKind.Activate } &&
                preview.Card.Id == id && preview.Source.Zone == zone && preview.SourceIndex == 0 &&
                preview.DestinationSlot == 0,
                "C12 App gate sees exact pre-spend source");
            queue.Tick(); queue.Tick();
            check(queue.IsBusy && duel.Graveyard(side).Count == 0 && duel.DeckCount(side) == 4 &&
                duel.Spells[enemy, 0] is not null && duel.Life.All(lp => lp == 8000),
                "C12 activation gate prevents source spend and effect");
            gate.Release = true;
            for (int tick = 0; tick < 100 && queue.IsBusy; tick++) queue.Tick();
            check(!queue.IsBusy && queue.Fault is null && duel.Graveyard(side).Single().Id == id &&
                duel.Spells[side, 0] is null &&
                queue.Trace.Where(e => e.Stage == ActionTraceStage.Executed).All(e => e.Result?.Outcome == CommandOutcome.Applied),
                "C12 fixed spell resolves through typed commands");
            if (LifePointSpellRules.ResolveQueued(id, side) is { } lp)
                check(duel.Life[side] == 8000 + lp.CasterDelta &&
                    duel.Life[enemy] == 8000 + lp.OpponentDelta && duel.Hand.Count == 0,
                    "C12 LP spell matches existing resolver");
            else if (DrawSpellRules.Effect(spell) is { } draw)
                check(duel.DeckCount(side) == 4 - draw.Cards && duel.Hand.Count == draw.Cards &&
                    duel.Life[enemy] == 8000 + draw.OpponentLifeGain,
                    "C12 draw spell keeps ordered draws and opponent heal");
            else
                check(duel.Spells[enemy, 0] is null && (id == SpellTrapWipeRules.GiantTrunade
                    ? duel.HandFor(enemy).Single().Id == trap.Id && duel.Graveyard(enemy).Count == 0
                    : duel.Graveyard(enemy).Single().Id == trap.Id && duel.HandFor(enemy).Count == 0),
                    "C12 S/T wipe preserves return vs destruction");
        }

        foreach (bool set in new[] { false, true })
        {
            var zone = set ? DuelZone.SpellTrap : DuelZone.Hand;
            foreach (ushort id in new ushort[] { DrawSpellRules.PotOfGreed, MonsterWipeRules.Raigeki,
                MonsterWipeRules.EternalRest, SpellTrapWipeRules.HarpiesFeatherDuster, SpellTrapWipeRules.GiantTrunade })
            {
                var duel = State(0, [Spell(id)]);
                if (set) check(duel.SetFromHand(0, out _), "C12 unavailable source fixture Set");
                var queue = duel.CreatePrimitiveActionQueue();
                check(!duel.CanQueueFixedSpellActivation(zone, 0) &&
                    !duel.TryQueueActivation(0, set, out _) && queue.Trace.Count == 0 &&
                    !queue.IsBusy && duel.Graveyard(0).Count == 0,
                    "C12 no legal deck or targets: query and dispatcher both reject");
            }
            var wipe = State(0, [Spell(MonsterWipeRules.Raigeki)], enemy: Monster(24));
            if (set) check(wipe.SetFromHand(0, out _), "C12 Raigeki fixture Set");
            var wipeQueue = wipe.CreatePrimitiveActionQueue();
            check(wipe.CanQueueFixedSpellActivation(zone, 0) &&
                wipe.TryQueueActivation(0, set, out _) && wipe.Monsters[1, 0] is not null,
                "C12 App fixed monster wipe admits without early removal");
            for (int tick = 0; tick < 100 && wipeQueue.IsBusy; tick++) wipeQueue.Tick();
            check(!wipeQueue.IsBusy && wipe.Monsters[1, 0] is null &&
                wipe.Graveyard(0).Single().Id == MonsterWipeRules.Raigeki,
                "C12 App monster wipe uses existing typed destruction");
        }
    }

    private static void QueuedTargetedSpellTraps(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        foreach (ushort id in new ushort[] { SpellTrapTargetRules.MysticalSpaceTyphoon,
            SpellTrapTargetRules.RemoveTrap, SpellTrapTargetRules.Spiritualism })
        foreach (bool field in new[] { false, true })
        {
            if (field && id == SpellTrapTargetRules.RemoveTrap) continue;
            int enemy = 1 - side;
            Card spell = Spell(id), targetCard = field ? Spell(332) : Spell(1056, 21);
            Card[] deck = [spell, Monster(24), Monster(25)];
            var duel = DemoDuel.FromPartialObservation(enemy, [targetCard], Slots(), Slots(), 8000,
                equipmentContextResolved: true, side0Deck: side == 0 ? deck : null,
                side1Deck: side == 1 ? deck : null);
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.Draw(out _),
                "C12 targeted S/T fixture: tracked opponent card and drawn source");
            if (id == SpellTrapTargetRules.RemoveTrap) duel.SpellFaceDown[enemy, 0] = false;
            if (fromSet) check(duel.SetFromHand(0, out _), "C12 targeted S/T fixture Set");
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            var gate = new FixedSpellGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            if (fromSet && id == SpellTrapTargetRules.MysticalSpaceTyphoon)
            {
                check(duel.TryCaptureCardTarget(side, zone, 0, out var sameTurn) &&
                    duel.QueuedSpellTrapTargetCandidates(sameTurn).Count == 0 && queue.Trace.Count == 0,
                    "C12 MST cannot activate on its Set turn");
                check(duel.EndTurn(out _) && duel.EndTurn(out _) && duel.Draw(out _),
                    "C12 MST later-turn fixture");
            }
            check(duel.TryCaptureCardTarget(side, zone, 0, out var source),
                "C12 tracked source occurrence");
            var choices = duel.QueuedSpellTrapTargetCandidates(source);
            check(choices.Count == 1 && choices[0].Target.Side == enemy &&
                choices[0].Target.Zone == (field ? DuelZone.FieldSpell : DuelZone.SpellTrap) &&
                choices[0].Zone == (field ? SpellTrapTargetRules.FieldZone : 0) &&
                duel.PendingSpellTrapTarget is null && !queue.IsBusy && queue.Trace.Count == 0,
                "C12 local target query has no legacy prompt or state mutation");
            var target = choices[0].Target;
            check(new CpuDuelController(side).Step(duel, queueTurnActions: true) is
                { Status: CpuStepStatus.Acted, Action: CpuAction.Selection, Card: null } &&
                queue.IsBusy && duel.PendingSpellTrapTarget is null && duel.Graveyard(side).Count == 0 &&
                duel.TryCaptureCardTarget(enemy, target.Zone, 0, out var stillPresent) && stillPresent == target,
                "C12 CPU admits exact target, no legacy spend or movement");
            var entry = queue.Trace.Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
            var preview = duel.DescribeActionPresentation(entry.Command);
            check(entry.Origin == ActionOrigin.Cpu && preview is { Kind: ActionPresentationKind.Activate } &&
                preview.Source == source && preview.Card.Id == id && preview.DestinationSlot == 0,
                "C12 A03 activation snapshot uses the exact targeted-spell source");
            queue.Tick(); queue.Tick();
            check(queue.IsBusy && duel.Graveyard(side).Count == 0 &&
                duel.TryCaptureCardTarget(enemy, target.Zone, 0, out stillPresent) && stillPresent == target,
                "C12 target remains until activation gate releases");
            gate.Release = true;
            for (int tick = 0; tick < 100 && queue.IsBusy; tick++) queue.Tick();
            check(!queue.IsBusy && queue.Fault is null && duel.Graveyard(side).Single().Id == id &&
                duel.PendingSpellTrapTarget is null && !duel.TryCaptureCardTarget(enemy, target.Zone, 0, out _) &&
                (id == SpellTrapTargetRules.Spiritualism
                    ? duel.HandFor(enemy).Single().Id == targetCard.Id && duel.Graveyard(enemy).Count == 0
                    : duel.Graveyard(enemy).Single().Id == targetCard.Id && duel.HandFor(enemy).Count == 0),
                "C12 targeted S/T returns or destroys the selected occurrence after source spend");
        }

        var stale = DemoDuel.FromPartialObservation(1, [Spell(1056, 21)], Slots(), Slots(), 8000,
            side0Deck: [Spell(SpellTrapTargetRules.MysticalSpaceTyphoon)]);
        check(stale.SetFromHand(0, out _) && stale.EndTurn(out _) && stale.Draw(out _),
            "C12 stale S/T fixture");
        var staleQueue = stale.CreatePrimitiveActionQueue();
        check(stale.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var staleSource),
            "C12 stale source captured");
        var oldChoice = stale.QueuedSpellTrapTargetCandidates(staleSource).Single();
        stale.Spells[1, 0] = Spell(1056, 21); // same ID, different/untracked occurrence
        check(!stale.TryQueueTargetedSpellTrap(staleSource, oldChoice.Target, out _) &&
            !staleQueue.IsBusy && staleQueue.Trace.Count == 0 && stale.Hand.Count == 1 &&
            stale.Graveyard(0).Count == 0,
            "C12 stale target never spends source or falls back to legacy resolution");
    }

    public static void Run(Action<bool, string> check)
    {
        QueuedFissure(check);
        QueuedFixedSpells(check);
        QueuedTargetedSpellTraps(check);
        var origin = new CardBounds(400, 520, 108, 153);
        var destination = NativeDuelLayout.MonsterBounds(0, true);
        var halfway = CardTravel.Sample(origin, destination, 4);
        check(CardTravel.Sample(origin, destination, 0) == origin &&
            CardTravel.Sample(origin, destination, CardTravel.Updates) == destination &&
            halfway.Y < 420 && halfway.Y > destination.Y && halfway.Width < origin.Width,
            "Alpha presentation: counter-based card travel moves and scales to the exact zone");
        var strong = Monster(1); var weak = Monster(2, 500, 500);
        var cpu = new CpuDuelController(1);
        var empty = State(1, []);
        check(cpu.Step(empty) is { Status: CpuStepStatus.Acted, Action: CpuAction.Draw } && empty.Winner == 0,
            "CPU: empty deck loses once and game-over becomes immutable");
        check(cpu.Step(empty).Status == CpuStepStatus.GameOver && empty.Turn == 2, "CPU: no action after defeat");
        var other = State(0, [strong], deck: [weak]);
        check(cpu.Step(other).Status == CpuStepStatus.WaitingForOpponent && other.Hand.Count == 1,
            "CPU: leaves opponent turn untouched");
        var win = State(1, [Spell(345)], deck: [weak]); win.Life[0] = 100;
        cpu.Step(win);
        check(cpu.Step(win).Action == CpuAction.Spell && win.Winner == 1, "CPU: lethal spell wins");

        var direct = State(1, [], own: strong);
        var legacyDirect = State(1, [], own: strong);
        var directQueue = direct.CreatePrimitiveActionQueue();
        check(direct.BeginBattle(out _) && legacyDirect.BeginBattle(out _) &&
            cpu.Step(direct, queueTurnActions: true) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 } &&
            direct.Life[0] == 8000 && directQueue.IsBusy &&
            cpu.Step(legacyDirect) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 },
            "CPU: direct attack admission defers damage in queued mode");
        for (int tick = 0; tick < 10 && directQueue.IsBusy; tick++) directQueue.Tick();
        var directTrace = directQueue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed).ToArray();
        check(!directQueue.IsBusy && direct.Life.SequenceEqual(legacyDirect.Life) &&
            directTrace.Select(entry => entry.Entry.Command.GetType()).SequenceEqual(
                new[] { typeof(DeclareDirectAttackCommand), typeof(DamageCommand) }) &&
            directTrace[0].Entry.Origin == ActionOrigin.Cpu,
            "CPU: direct declaration and damage match legacy final life points");

        var unsupportedDirect = State(1, [], own: strong);
        unsupportedDirect.Spells[1, 0] = Spell(999);
        var unsupportedQueue = unsupportedDirect.CreatePrimitiveActionQueue();
        check(unsupportedDirect.BeginBattle(out _) &&
            cpu.Step(unsupportedDirect, queueTurnActions: true) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack } &&
            unsupportedDirect.Life[0] == 8000 - strong.Attack && !unsupportedQueue.IsBusy,
            "CPU: a direct attack outside the isolated pilot keeps its legacy execution");

        var activation = State(1, [Spell(342)], deck: [weak]);
        activation.Spells[1, 0] = Spell(1002, 21);
        cpu.Step(activation);
        check(cpu.Step(activation) is { Action: CpuAction.Spell, SourceZone: 1, HandIndex: 0, Card.Id: 342 } &&
            activation.Spells[1, 1] is null && activation.Graveyard(1).Last().Id == 342,
            "CPU: spell activation reports its display zone even after the spell leaves the field");

        foreach (int side in new[] { 0, 1 })
        foreach (bool trap in new[] { false, true })
        {
            Card placement = trap ? Spell(1056, 21) : strong;
            var d = State(side, [placement], deck: [Spell(999)]);
            var queue = d.CreatePrimitiveActionQueue();
            var controller = new CpuDuelController(side);
            void Drain()
            {
                for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
                if (queue.IsBusy) throw new InvalidOperationException("CPU placement queue did not drain.");
            }
            check(controller.Step(d, queueTurnActions: true).Action == CpuAction.Draw,
                "C12 CPU placement: turn draw");
            Drain();
            var placementStep = controller.Step(d, queueTurnActions: true);
            check(placementStep.Status == CpuStepStatus.Acted &&
                placementStep.Action == (trap ? CpuAction.Set : CpuAction.Summon) &&
                queue.IsBusy && d.Hand.Count == 2 && d.Spells[side,0] is null && d.Monsters[side,0] is null,
                "C12 CPU placement: admission without mutation");
            var entry = queue.Trace.Last(x => x.Stage == ActionTraceStage.Admitted).Entry;
            check(entry.Origin == ActionOrigin.Cpu &&
                entry.Command.GetType().Name == (trap ? "SetCommand" : "PrepareNormalPlacementCommand"),
                "C12 CPU placement: typed producer");
            Drain();
            check(d.Hand.Count == 1 && (trap
                ? d.Spells[side,0] == placement && d.SpellFaceDown[side,0]
                : d.Monsters[side,0] == placement && !d.MonsterFaceDown[side,0]),
                "C12 CPU placement: expected board");
        }

        foreach (int side in new[] { 0, 1 })
        foreach (bool optional in new[] { false, true })
        {
            var positionCard = optional ? Monster(FlipEffectRules.Skelengel, 100, 100) : strong;
            var d = State(side, [], own: positionCard, deck: [Spell(999), Spell(998)]);
            d.MonsterFaceDown[side,0] = d.DefensePosition[side,0] = true;
            var queue = d.CreatePrimitiveActionQueue();
            var controller = new CpuDuelController(side);
            void DrainPosition()
            {
                for (int i = 0; i < 100 && queue.IsBusy; i++)
                {
                    if (queue.PendingDecision is { } request)
                        queue.SubmitDecision(request.Id, CpuDuelController.DecideOptionalEffect(d, request));
                    queue.Tick();
                }
                if (queue.IsBusy) throw new InvalidOperationException("CPU position queue did not drain.");
            }
            check(controller.Step(d, queueTurnActions: true).Action == CpuAction.Draw,
                "C12 CPU position: turn draw");
            DrainPosition();
            check(controller.Step(d, queueTurnActions: true) is
                { Status: CpuStepStatus.Acted, Action: CpuAction.Position, SourceZone: 0 } &&
                d.MonsterFaceDown[side,0] && queue.IsBusy,
                "C12 CPU position: admits without revealing");
            var entry = queue.Trace.Last(x => x.Stage == ActionTraceStage.Admitted).Entry;
            check(entry.Origin == ActionOrigin.Cpu && entry.Command is FlipCommand &&
                d.DescribeActionPresentation(entry.Command) is { Kind: ActionPresentationKind.Flip },
                "C12 CPU position: same FlipCommand and presentation");
            DrainPosition();
            check(!d.MonsterFaceDown[side,0] && !d.DefensePosition[side,0] &&
                !d.CanChangePosition(0) && d.Hand.Count == (optional ? 2 : 1) &&
                queue.PendingDecision is null && d.PendingEffectDecision is null,
                "C12 CPU position: flip and optional draw finish without human prompt");
        }

        var summonTrap = State(0, [strong]);
        var destroyed = new List<DemoMonsterDestruction>();
        summonTrap.MonsterDestroyed += destroyed.Add;
        summonTrap.Spells[1, 3] = Spell(1002, 21);
        summonTrap.SpellFaceDown[1, 3] = true;
        check(summonTrap.SummonLeftmost(0, false, out _) &&
            cpu.Step(summonTrap) is { Action: CpuAction.Response, SourceZone: 3, Card.Id: 1002 } &&
            summonTrap.Spells[1, 3] is null && summonTrap.Graveyard(0).Single() == strong &&
            destroyed.SequenceEqual(new[] { new DemoMonsterDestruction(0, 0, strong, false, false) }),
            "CPU: Trap Hole reports its zone and one destroyed monster after normal resolution");

        var tributeMonster = strong with { Properties = (strong.Properties & ~(15u << 25)) | (6u << 25) };
        var tribute = State(0, [tributeMonster], own: weak);
        tribute.MonsterDestroyed += destroyed.Add;
        check(tribute.SummonLeftmost(0, false, out _, [0]) && tribute.Graveyard(0).Single() == weak &&
            destroyed.Count == 1, "Presentation: tribute movement does not trigger destruction");

        var noMoves = State(1, [], own: weak, enemy: strong, deck: [Spell(999)]);
        int count = 0;
        while (noMoves.Turn == 2 && !noMoves.GameOver && count++ < 30)
            check(cpu.Step(noMoves).Status == CpuStepStatus.Acted, "CPU: bounded few-options action");
        check(noMoves.Turn == 3 && count < 10 && noMoves.Life[1] == 8000,
            "CPU: refuses losing face-up attack and ends turn with unsupported hand card");
        var defense = State(1, [Monster(3, 700, 2000)], enemy: strong, deck: [Spell(999)]);
        cpu.Step(defense); var set = cpu.Step(defense);
        check(set.Action == CpuAction.Set && defense.DefensePosition[1, 0] &&
            !set.Message.Contains("Fixture"), "CPU: defensive set with hidden identity in log");

        foreach (ushort id in new ushort[] { 500, 590, 427, 101, 601, 540 })
        {
            var flip = Monster(id, 450, 600);
            var d = State(1, [], flip, strong, [Spell(342), Spell(1002, 21)],
                Enumerable.Repeat(weak, 12).ToArray());
            d.MonsterFaceDown[1, 0] = d.DefensePosition[1, 0] = true;
            check(d.ChangePosition(0, out _), $"CPU FLIP {id}: setup trigger");
            int steps = 0;
            while (CpuDuelController.PendingOwner(d) == 1 && !d.GameOver && steps++ < 12)
                check(cpu.Step(d).Status == CpuStepStatus.Acted, $"CPU FLIP {id}: resolve choice");
            check(CpuDuelController.PendingOwner(d) is null && steps < 12, $"CPU FLIP {id}: no stalled prompt");
        }
        var attackFlip = State(0, [], strong, Monster(500, 450, 600));
        attackFlip.MonsterFaceDown[1, 0] = attackFlip.DefensePosition[1, 0] = true;
        attackFlip.NextTurn(); attackFlip.NextTurn(); attackFlip.BeginBattle(out _); attackFlip.Attack(0, 0, out _);
        check(attackFlip.PendingFlipTarget?.Owner == 1 && cpu.Step(attackFlip).Status == CpuStepStatus.Acted &&
            attackFlip.PendingFlipTarget is null && attackFlip.Monsters[0, 0] is null,
            "CPU: defender resolves mandatory FLIP during human turn");

        var revive = State(1, [], Monster(1405), strong);
        revive.NextTurn(); revive.BeginBattle(out _); revive.Attack(0, 0, out _);
        check(revive.PendingEffectDecision?.Owner == 1 && cpu.Step(revive).Action == CpuAction.Decision &&
            revive.PendingEffectDecision is null && revive.Life[1] == 8000,
            "CPU: optional revival cost is declined without blocking opponent");

        var discard = State(1, Enumerable.Repeat(Spell(999), 12).ToArray(), deck: [Spell(999)]);
        int discards = 0;
        for (int i = 0; i < 25 && discard.Turn == 2; i++)
        {
            var step = cpu.Step(discard);
            if (step.Action == CpuAction.Discard) discards++;
            check(step.Status == CpuStepStatus.Acted, "CPU: discard progress");
        }
        check(discard.Turn == 3 && discards == 7 && discard.HandFor(1).Count == 6,
            "CPU: enforces end-turn hand limit and advances exactly once");

        var stressed = State(1, Enumerable.Repeat(Spell(338), 75).ToArray(), deck: [weak]);
        int stressActions = 0, tacticalSpells = 0;
        while (stressed.Turn == 2 && stressActions++ < CpuDuelController.MaxActionsPerTurn)
        {
            var step = cpu.Step(stressed);
            if (step.Status != CpuStepStatus.Acted) throw new Exception(step.Message);
            if (step.Action == CpuAction.Spell) tacticalSpells++;
        }
        check(stressed.Turn == 3 && tacticalSpells == CpuDuelController.TacticalActionLimit - 1 &&
            stressed.HandFor(1).Count == 6 && stressActions < CpuDuelController.MaxActionsPerTurn,
            "CPU: tactical limit stops optional actions but still finishes mandatory discards");

        // Entire deterministic matches, with FLIP, LP and trap responses in both directions.
        var pool = new[] { strong, Monster(3, 1400, 2000), Monster(500, 450, 600), Monster(427, 300, 400),
            Monster(101, 900, 400), Monster(590, 700, 600), Spell(345), Spell(342), Spell(341), Spell(1002, 21), Spell(1056, 21) };
        var deck = Enumerable.Range(0, 40).Select(i => pool[i % pool.Length]).ToArray();
        string Play(int seed, DuelLookAndFeel style)
        {
            var d = DemoDuel.FromDecks(deck, deck, seed);
            var controllers = new[] { new CpuDuelController(0), new CpuDuelController(1) };
            var clock = new DuelPresentationClock(style);
            var transcript = new List<string>();
            for (int i = 0; i < 5000 && !d.GameOver; i++)
            {
                int owner = CpuDuelController.PendingOwner(d) ?? d.ActivePlayer;
                var result = controllers[owner].Step(d);
                if (result.Status != CpuStepStatus.Acted) throw new Exception($"CPU seed {seed}: {result.Message}");
                if (controllers[owner].ActionsThisTurn > CpuDuelController.MaxActionsPerTurn) throw new Exception("Unbounded CPU turn");
                transcript.Add($"{d.Turn}:{result.Action}:{d.Life[0]}:{d.Life[1]}:{result.Message}");
                clock.Show(DuelPresentationCue.CardMove); clock.Advance(TimeSpan.FromSeconds(1));
            }
            if (!d.GameOver) throw new Exception("CPU match did not terminate: " + seed);
            return string.Join("|", transcript) + $"/{d.Winner}/{d.IsDraw}";
        }
        for (int seed = 0; seed < 40; seed++)
        {
            string expected = Play(seed, new DuelLookAndFeel());
            check(expected == Play(seed, new FastLookAndFeel()) && expected == Play(seed, new InstantLookAndFeel()),
                $"CPU: full duel seed {seed}, deterministic result independent of presentation");
        }
    }

    public static void RunCatalog(Action<bool, string> check, Archive archive)
    {
        var catalog = Catalog.Load(archive);
        var packs = archive.Read(@"bin#\card_pack.bin");
        var playable = CardListResources.ForPacks(catalog, packs);
        var deck = AlphaDeck.Create(playable);
        var fusion = AlphaDeck.CreateFusionDeck(playable);
        var art = CardArtwork.Load(archive, catalog);
        check(deck.Concat(fusion).All(c => art.For(c) is { } image && !image.StartsWith(@"card\token_", StringComparison.OrdinalIgnoreCase)),
            "Alpha: all cards are available in the packs and have non-token artwork");
        check(deck.Count == 40 && deck.GroupBy(c => c.Id).All(g => g.Count() <= 3), "Alpha: curated DAT deck has 40 cards, at most three copies");
        int fusions = 0;
        for (int seed = 0; seed < 30; seed++)
        {
            var d = DemoDuel.FromDecks(deck, deck, seed, fusion, fusion);
            d.MonsterSpecialSummoned += summon =>
            {
                if (FusionRules.IsFusionMonster(summon.Card)) fusions++;
            };
            var players = new[] { new CpuDuelController(0), new CpuDuelController(1) };
            int steps = 0;
            while (!d.GameOver && steps++ < 5000)
            {
                var action = players[CpuDuelController.PendingOwner(d) ?? d.ActivePlayer].Step(d);
                if (action.Status != CpuStepStatus.Acted) throw new Exception($"Alpha DAT seed {seed}: {action.Message}");
            }
            check(d.GameOver && steps < 5000, $"Alpha DAT: complete duel seed {seed} in {steps} actions / {d.Turn} turns");
        }
        check(fusions > 0, "Alpha DAT: the curated Fusion Deck was used in a complete CPU duel");
    }
}
