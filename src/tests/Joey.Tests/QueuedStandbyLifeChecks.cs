using Joey.Core;

internal static class QueuedStandbyLifeChecks
{
    private static Card Monster(ushort id) =>
        new(id, id, $"Monster {id}", "", (3u << 20) | (4u << 25) | (100u << 9) | 80u);

    private sealed class Gate : IPresentationGate
    {
        public Type? Hold { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => command.Command.GetType() != Hold;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 90; tick++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Standby life effects did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        Card spirit = Monster(1437), fairy = Monster(1438), mermaid = Monster(1441), marie = Monster(1446);
        Card filler = new(342, 342, "Sparks", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        {
            int enemy = 1 - side;
            Card?[] own = [spirit, spirit, fairy, fairy, mermaid];
            Card?[] opposing = [spirit, mermaid, marie, null, null];
            var duel = DemoDuel.FromPartialObservation(side, [],
                side == 0 ? own : opposing, side == 1 ? own : opposing, 8000,
                side0Graveyard: side == 0 ? [marie, filler, marie] : [marie],
                side1Graveyard: side == 1 ? [marie, filler, marie] : [marie],
                side0Deck: side == 0 ? [filler, filler] : [],
                side1Deck: side == 1 ? [filler, filler] : []);
            duel.DefensePosition[side, 1] = duel.DefensePosition[side, 2] = true;
            duel.MonsterFaceDown[side, 3] = duel.DefensePosition[side, 3] = side == 0;
            duel.DefensePosition[side, 4] = side == 1;
            var gate = new Gate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            queue.Enqueue(new DrawCommand(side));
            Drain(queue);
            check(duel.Life.All(lp => lp == 8000) && duel.CanDraw, "An effect draw does not run Standby LP effects");

            gate.Hold = typeof(TurnDrawCommand);
            check(new CpuDuelController(side).Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Draw } &&
                duel.Hand.Count == 1, "CPU admits the normal draw before the Standby effects");
            queue.Tick();
            queue.Tick();
            check(queue.Active?.Command is TurnDrawCommand && duel.Hand.Count == 2 && duel.Life.All(lp => lp == 8000),
                "Standby healing waits for the turn draw gate");
            gate.Hold = typeof(HealCommand);
            for (int tick = 0; queue.IsBusy && queue.Active?.Command is not HealCommand && tick < 15; tick++)
                queue.Tick();
            queue.Tick();
            check(queue.Active?.Command is HealCommand { Amount: 1000 } && duel.Life[side] == 9000 &&
                duel.Life[enemy] == 8000, "First Standby heal has a separate gate and applies once");
            gate.Hold = null;
            Drain(queue);
            var heals = queue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed)
                .Select(entry => entry.Entry.Command).OfType<HealCommand>().ToArray();
            check(duel.Life[side] == 11200 && duel.Life[enemy] == 8000 &&
                heals.SequenceEqual(new[] { 1000, 1000, 800, 200, 200 }.Select(amount => new HealCommand(side, amount))) &&
                !duel.TryQueueTurnDraw(out _),
                "Standby gains use only eligible own occurrences and run once per turn");
        }

        var offerings = new Card(1451, TargetedMonsterSpellRules.OfferingsToTheDoomed, "Offerings", "", 22u << 20);
        var skip = DemoDuel.FromPartialObservation(0, [offerings], [mermaid, null, null, null, null],
            [Monster(24), null, null, null, null], 8000);
        var skipQueue = skip.CreatePrimitiveActionQueue();
        check(skip.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var source) &&
            skip.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var target) &&
            skip.TryQueueTargetedMonsterSpell(source, target, out _), "Offerings replaces the controller's next Draw Phase");
        Drain(skipQueue);
        check(skip.TryQueueEndTurn(out _), "Standby skip fixture ends the controller turn");
        Drain(skipQueue);
        check(skip.TryQueueEndTurn(out _), "Standby skip fixture returns to the controller");
        Drain(skipQueue);
        check(skip.TryQueueTurnDraw(out _), "Standby still follows a skipped Draw Phase");
        Drain(skipQueue);
        check(!skip.GameOver && skip.Life[0] == 8800 && skip.Hand.Count == 0,
            "Skipped empty-deck draw still permits Cure Mermaid's Standby heal");

        var deckOut = DemoDuel.FromPartialObservation(0, [], [mermaid, null, null, null, null], new Card?[5], 8000);
        var deckOutQueue = deckOut.CreatePrimitiveActionQueue();
        check(deckOut.TryQueueTurnDraw(out _), "Ordinary empty-deck draw is admitted");
        Drain(deckOutQueue);
        check(deckOut.Winner == 1 && deckOut.Life[0] == 8000 &&
            !deckOutQueue.Trace.Any(entry => entry.Entry.Command is HealCommand),
            "Losing on the turn draw prevents Standby healing");

        var jam = Monster(RevivalJamRules.ResourceId);
        var revival = DemoDuel.FromPartialObservation(1, [], [jam, mermaid, null, null, null],
            [Monster(24), null, null, null, null], 8000, side0Deck: [filler]);
        revival.MonsterFaceDown[0, 0] = revival.DefensePosition[0, 0] = true;
        check(revival.BeginBattle(out _) && revival.Attack(0, 0, out _) &&
            revival.PendingEffectDecision is { } decision &&
            revival.ResolveEffectDecision(decision.Id, EffectDecisionAnswer.Accept, out _) && revival.EndTurn(out _),
            "Cure Mermaid and a paid Revival Jam return share the same Standby");
        var revivalGate = new Gate { Hold = typeof(SpecialSummonCommand) };
        var revivalQueue = revival.CreatePrimitiveActionQueue(revivalGate);
        check(revival.TryQueueTurnDraw(out _), "Turn draw schedules the existing return before Standby heals");
        for (int tick = 0; revivalQueue.IsBusy && revivalQueue.Active?.Command is not SpecialSummonCommand && tick < 20; tick++)
            revivalQueue.Tick();
        check(revivalQueue.Active?.Command is SpecialSummonCommand && revival.Monsters[0, 0]?.Id == jam.Id &&
            revival.Life[0] == 7000, "Cure Mermaid waits until Revival Jam's summon gate releases");
        revivalGate.Hold = null;
        Drain(revivalQueue);
        check(revival.Life[0] == 7800, "Cure Mermaid heals after the delayed return finishes");
    }
}
