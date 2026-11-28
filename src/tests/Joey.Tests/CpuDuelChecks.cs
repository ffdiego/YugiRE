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

    private static bool DrawThroughQueue(DemoDuel duel)
    {
        if (!duel.TryQueueTurnDraw(out _, ActionOrigin.Test)) return false;
        var queue = duel.Actions;
        for (int tick = 0; tick < 100 && queue.IsBusy; tick++) queue.Tick();
        if (queue.IsBusy || queue.Fault is not null)
            throw new InvalidOperationException("Turn draw fixture did not drain.");
        return true;
    }

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
            check(DrawThroughQueue(duel), "C12 Fissure fixture draw before Main");
            var queue = duel.Actions;
            int baseline = queue.Trace.Count;
            var cpu = new CpuDuelController(side);
            check(duel.TryCaptureCardTarget(1 - side, DuelZone.Monster, 0, out var first) &&
                duel.TryCaptureCardTarget(1 - side, DuelZone.Monster, 1, out var second) &&
                first.Occurrence != second.Occurrence, "C12 Fissure has two tracked minimum occurrences");

            check(cpu.Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
                queue.IsBusy && duel.Graveyard(side).Count == 0 && duel.Monsters[1 - side, 0] is not null,
                "C12 CPU chooses tied Fissure target without mutating during admission");
            var entry = queue.Trace.Skip(baseline).Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
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
            338, 345, LifePointSpellRules.BlueMedicine, LifePointSpellRules.TremendousFire,
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
            var gate = new FixedSpellGate { Release = true };
            var queue = duel.ConfigureActionQueue(gate);
            check(duel.SetFromHand(0, out _) && TurnFixture.End(duel, out _) && DrawThroughQueue(duel),
                "C12 fixed spell fixture: tracked enemy Set, draw source");
            gate.Release = false;
            int baseline = queue.Trace.Count;
            if (fromSet) check(duel.SetFromHand(0, out _), "C12 fixed spell fixture Set");
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            check(duel.CanQueueFixedSpellActivation(zone, 0), "C12 fixed producer available on the intrinsic queue");
            if (fromSet)
            {
                duel.SpellFaceDown[side, 0] = false;
                check(!duel.CanQueueFixedSpellActivation(zone, 0), "Face-up source cannot activate again");
                duel.SpellFaceDown[side, 0] = true;
            }
            check(DemoDuel.SupportsQueuedFixedSpell(spell) &&
                duel.CanQueueFixedSpellActivation(zone, 0) && !queue.IsBusy &&
                queue.Trace.Count == baseline && duel.Graveyard(side).Count == 0 &&
                duel.DeckCount(side) == 4 && duel.Life.All(lp => lp == 8000),
                "C12 UI query uses tracked legality without mutation");
            var step = new CpuDuelController(side).Step(duel);
            check(step is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell, Card: null, HandIndex: null } &&
                queue.IsBusy && !duel.CanQueueFixedSpellActivation(zone, 0) &&
                duel.Graveyard(side).Count == 0 && duel.DeckCount(side) == 4 &&
                duel.Spells[enemy, 0] is not null && duel.Life.All(lp => lp == 8000),
                "C12 CPU admission only; no immediate mutation or duplicate legacy presentation metadata");
            var entry = queue.Trace.Skip(baseline).Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
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
            var gate = new FixedSpellGate { Release = true };
            var queue = duel.ConfigureActionQueue(gate);
            check(duel.SetFromHand(0, out _) && TurnFixture.End(duel, out _) && DrawThroughQueue(duel),
                "C12 targeted S/T fixture: tracked opponent card and drawn source");
            int baseline = queue.Trace.Count;
            if (id == SpellTrapTargetRules.RemoveTrap) duel.SpellFaceDown[enemy, 0] = false;
            if (fromSet) check(duel.SetFromHand(0, out _), "C12 targeted S/T fixture Set");
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            if (fromSet && id == SpellTrapTargetRules.MysticalSpaceTyphoon)
            {
                check(duel.TryCaptureCardTarget(side, zone, 0, out var sameTurn) &&
                    duel.QueuedSpellTrapTargetCandidates(sameTurn).Count == 0 && queue.Trace.Count == baseline,
                    "C12 MST cannot activate on its Set turn");
                check(TurnFixture.End(duel, out _) && TurnFixture.End(duel, out _) && DrawThroughQueue(duel),
                    "C12 MST later-turn fixture");
                baseline = queue.Trace.Count;
            }
            gate.Release = false;
            check(duel.TryCaptureCardTarget(side, zone, 0, out var source),
                "C12 tracked source occurrence");
            var choices = duel.QueuedSpellTrapTargetCandidates(source);
            check(choices.Count == 1 && choices[0].Target.Side == enemy &&
                choices[0].Target.Zone == (field ? DuelZone.FieldSpell : DuelZone.SpellTrap) &&
                choices[0].Zone == (field ? SpellTrapTargetRules.FieldZone : 0) &&
                duel.PendingSpellTrapTarget is null && !queue.IsBusy && queue.Trace.Count == baseline,
                "C12 local target query has no legacy prompt or state mutation");
            var target = choices[0].Target;
            check(new CpuDuelController(side).Step(duel) is
                { Status: CpuStepStatus.Acted, Action: CpuAction.Selection, Card: null } &&
                queue.IsBusy && duel.PendingSpellTrapTarget is null && duel.Graveyard(side).Count == 0 &&
                duel.TryCaptureCardTarget(enemy, target.Zone, 0, out var stillPresent) && stillPresent == target,
                "C12 CPU admits exact target, no legacy spend or movement");
            var entry = queue.Trace.Skip(baseline).Single(e => e.Stage == ActionTraceStage.Admitted).Entry;
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
        check(stale.SetFromHand(0, out _) && TurnFixture.End(stale, out _) && DrawThroughQueue(stale),
            "C12 stale S/T fixture");
        var staleQueue = stale.Actions;
        int staleBaseline = staleQueue.Trace.Count;
        check(stale.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var staleSource),
            "C12 stale source captured");
        var oldChoice = stale.QueuedSpellTrapTargetCandidates(staleSource).Single();
        stale.Spells[1, 0] = Spell(1056, 21); // same ID, different/untracked occurrence
        check(!stale.TryQueueTargetedSpellTrap(staleSource, oldChoice.Target, out _) &&
            !staleQueue.IsBusy && staleQueue.Trace.Count == staleBaseline && stale.Hand.Count == 1 &&
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
        var emptyQueue = empty.Actions;
        check(cpu.Step(empty) is { Status: CpuStepStatus.Acted, Action: CpuAction.Draw } &&
            empty.Winner is null && emptyQueue.IsBusy, "CPU admits deck-out through the queue");
        for (int i = 0; i < 100 && emptyQueue.IsBusy; i++) emptyQueue.Tick();
        check(empty.Winner == 0 && cpu.Step(empty).Status == CpuStepStatus.GameOver,
            "Queued deck-out completes once");

        foreach (int opponentLife in new[] { 1000, 1001 })
        {
            var fire = State(1, [Spell(LifePointSpellRules.TremendousFire)], deck: [weak]);
            fire.Life[0] = opponentLife;
            fire.Life[1] = 500;
            check(DrawThroughQueue(fire), "CPU: Tremendous Fire fixture drew for the turn");
            var queue = fire.Actions;
            var action = cpu.Step(fire);
            for (int tick = 0; tick < 15 && queue.IsBusy; tick++) queue.Tick();
            check(opponentLife == 1000
                ? action.Action == CpuAction.Spell && fire.IsDraw
                : action.Action == CpuAction.Summon && fire.Life[1] == 500 && fire.Graveyard(1).Count == 0,
                "CPU: Tremendous Fire can tie the duel and is skipped when only its own damage is lethal");
        }

        var direct = State(1, [], own: strong);
        var directQueue = direct.CreatePrimitiveActionQueue();
        check(direct.BeginBattle(out _) &&
            cpu.Step(direct) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 } &&
            direct.Life[0] == 8000 && directQueue.IsBusy,
            "CPU: direct attack admission defers damage in queued mode");
        for (int tick = 0; tick < 10 && directQueue.IsBusy; tick++) directQueue.Tick();
        var directTrace = directQueue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed).ToArray();
        check(!directQueue.IsBusy && direct.Life[0] == 8000 - strong.Attack &&
            directTrace.Select(entry => entry.Entry.Command.GetType()).SequenceEqual(
                new[] { typeof(DeclareDirectAttackCommand), typeof(DamageCommand) }) &&
            directTrace[0].Entry.Origin == ActionOrigin.Cpu,
            "CPU: direct declaration and damage use the typed executor");

        var unsupportedDirect = State(1, [Spell(0x593)], own: strong);
        check(unsupportedDirect.SetFromHand(0, out _) && unsupportedDirect.ActivateSetSpell(0, out _),
            "CPU: Infinite Cards is a tracked face-up continuous card outside the battle pilot");
        var unsupportedQueue = unsupportedDirect.CreatePrimitiveActionQueue();
        check(unsupportedDirect.BeginBattle(out _) &&
            !unsupportedDirect.TryQueueAttack(0, -1, out _, out bool unsupported) && unsupported &&
            unsupportedQueue.Trace.Count == 0 && unsupportedDirect.Life[0] == 8000 &&
            cpu.Step(unsupportedDirect) is { Status: CpuStepStatus.Blocked, Action: CpuAction.Attack } &&
            unsupportedDirect.Life[0] == 8000 && !unsupportedQueue.IsBusy && unsupportedQueue.Trace.Count == 0,
            "CPU: unsupported attack is rejected without compatibility execution");

        var rejectedAttack = State(1, [], own: strong);
        var rejectedQueue = rejectedAttack.CreatePrimitiveActionQueue();
        check(rejectedAttack.BeginBattle(out _) &&
            !rejectedAttack.TryQueueAttack(0, 0, out _, out bool missingTarget) && !missingTarget &&
            rejectedAttack.TryQueueAttack(0, -1, out _) &&
            !rejectedAttack.TryQueueAttack(0, -1, out _, out bool busy) && !busy &&
            cpu.Step(rejectedAttack).Status == CpuStepStatus.Blocked && rejectedAttack.Life[0] == 8000,
            "CPU: invalid target and busy queue cannot authorize a compatibility attack");
        for (int tick = 0; tick < 10 && rejectedQueue.IsBusy; tick++) rejectedQueue.Tick();
        check(!rejectedAttack.TryQueueAttack(0, -1, out _, out bool alreadyAttacked) && !alreadyAttacked,
            "CPU: an already used attacker cannot authorize compatibility execution");

        var untrackedAttack = State(1, [], own: strong);
        var untrackedQueue = untrackedAttack.CreatePrimitiveActionQueue();
        check(untrackedAttack.BeginBattle(out _), "CPU: untracked attack is tested in Battle Phase");
        untrackedAttack.Monsters[1, 0] = strong with { };
        check(cpu.Step(untrackedAttack) is { Status: CpuStepStatus.Blocked, Action: CpuAction.Attack } &&
            untrackedAttack.Life[0] == 8000 && untrackedAttack.CanAttack(0) && untrackedQueue.Trace.Count == 0,
            "CPU: an untracked occurrence cannot fall back or consume the attack");

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
            check(controller.Step(d).Action == CpuAction.Draw,
                "C12 CPU placement: turn draw");
            Drain();
            var placementStep = controller.Step(d);
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
            check(controller.Step(d).Action == CpuAction.Draw,
                "C12 CPU position: turn draw");
            DrainPosition();
            check(controller.Step(d) is
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
        // Full DAT duels are now differential lab work, not a CI gate.
        // See archive/card-tests for the historical two-executor comparison.
    }


}
