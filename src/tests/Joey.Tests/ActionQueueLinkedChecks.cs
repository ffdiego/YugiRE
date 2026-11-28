using Joey.Core;

internal static class ActionQueueLinkedChecks
{
    private static Card Monster(ushort id, int type = 3) =>
        new(id, id, $"Fixture {id}", "", ((uint)type << 20) | (4u << 25) | (100u << 9));
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0, 5).Select(i => i < cards.Length ? cards[i] : null).ToArray();
    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new Exception("Linked pilot failed to drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not DestroyCommand || Release;
    }

    public static void Run(Action<bool, string> check)
    {
        var normal = Monster(24, 15);
        var immortal = Monster(ImmortalThunderRules.ResourceId);
        var raigeki = new Card(336, 336, "Raigeki", "", 22u << 20);
        var sword = new Card(300, 300, "Legendary Sword", "", 22u << 20);
        foreach (int life in new[] { 8000, 4000 })
        {
            DemoDuel State()
            {
                var d = DemoDuel.FromPartialObservation(0, [sword, raigeki], Slots(),
                    Slots(normal, immortal, normal), 8000, equipmentContextResolved: true);
                d.MonsterWasFlipped[1, 1] = true;
                d.Life[1] = life;
                if (!d.Equip(0, 1, 0, out _)) throw new Exception("Equip fixture failed.");
                return d;
            }
            var legacy = State();
            check(legacy.Play(0, out _), "A02 linked legacy comparison fixture");
            (DemoDuel Duel, ActionQueue Queue) Run(bool delayed)
            {
                var d = State();
                var gate = new Gate();
                var q = d.CreatePrimitiveActionQueue(delayed ? gate : null);
                check(d.TryQueueRaigekiFromHand(0, out _), "A02 admits Raigeki with tracked linked effects");
                if (delayed)
                {
                    q.Tick(); q.Tick(); q.Tick(); // activation, source, first monster
                    check(d.Monsters[1, 0] is null && d.Spells[0, 0] == sword &&
                        d.Life[1] == life && d.Monsters[1, 1] == immortal,
                        "A02 linked equip has not moved before monster gate");
                    q.Tick();
                    check(d.Spells[0, 0] == sword, "A02 waiting does not execute equip removal");
                    gate.Release = true;
                    q.Tick(); // complete monster
                    q.Tick(); // destroy equip
                    check(d.Spells[0, 0] is null && d.EquipmentCards.Count == 0 &&
                        d.Life[1] == life && d.Monsters[1, 1] == immortal,
                        "A02 equip destruction is a separate command");
                }
                Drain(q);
                check(d.Life.SequenceEqual(legacy.Life) && d.Winner == legacy.Winner &&
                    d.Graveyard(0).SequenceEqual(legacy.Graveyard(0)) &&
                    d.Graveyard(1).SequenceEqual(legacy.Graveyard(1)) &&
                    d.EquipmentCards.Count == 0 && d.Monsters[1, 2] is null &&
                    d.LastMonsterWipe == legacy.LastMonsterWipe &&
                    q.Trace.Count(t => t.Stage == ActionTraceStage.Executed && t.Entry.Command is DamageCommand) == 1 &&
                    q.Trace.Count(t => t.Stage == ActionTraceStage.Executed && t.Entry.Command is DestroyCommand) == 4,
                    "A02 linked wipe matches legacy including lethal LP and remaining targets");
                return (d, q);
            }
            var immediate = Run(false);
            var gated = Run(true);
            check(immediate.Queue.Trace.SequenceEqual(gated.Queue.Trace), "A02 linked commands trace is gate independent");
        }

        foreach (var answer in new[] { EffectDecisionAnswer.Accept, EffectDecisionAnswer.Decline })
        {
            var skelengel = Monster(FlipEffectRules.Skelengel);
            var d = DemoDuel.FromPartialObservation(0, [], Slots(skelengel), Slots(), 8000, side0Deck: [normal]);
            d.MonsterFaceDown[0, 0] = d.DefensePosition[0, 0] = true;
            var q = d.CreatePrimitiveActionQueue();
            check(d.TryQueueChangePosition(0, out _), "A02 Skelengel admitted");
            for (int i = 0; i < 20 && q.PendingDecision is null; i++) q.Tick();
            var pending = q.PendingDecision ?? throw new Exception("Skelengel did not expose queue decision.");
            check(q.Tick() == ActionTickResult.WaitingForDecision && d.Hand.Count == 0 &&
                !d.TryQueueEndTurn(out _) && !d.ResolveEffectDecision(pending.Id, answer, out _),
                "A02 real optional draw waits on scheduler decision, no legacy bypass");
            bool rejected = false;
            try { q.Enqueue(new DrawCommand(0)); } catch (InvalidOperationException) { rejected = true; }
            check(rejected && !q.SubmitDecision(pending.Id + 1, answer) &&
                q.SubmitDecision(pending.Id, answer) && !q.SubmitDecision(pending.Id, answer) &&
                d.Hand.Count == 0, "A02 pending choice rejects work and stale/repeated answer");
            Drain(q);
            check(d.Hand.Count == (answer == EffectDecisionAnswer.Accept ? 1 : 0) &&
                d.EffectDecisionHistory.Single().Answer == answer && q.PendingDecision is null,
                "A02 scheduler answer records decision and draws only on Accept");
        }

        var suppressed = DemoDuel.FromPartialObservation(0, [], Slots(Monster(FlipEffectRules.Skelengel)),
            Slots(), 8000, side0Deck: [normal]);
        suppressed.MonsterFaceDown[0, 0] = suppressed.DefensePosition[0, 0] = true;
        var suppressionQueue = suppressed.CreatePrimitiveActionQueue();
        check(suppressed.TryQueueChangePosition(0, out _), "A08 queued optional draw setup");
        for (int i = 0; i < 20 && suppressionQueue.PendingDecision is null; i++) suppressionQueue.Tick();
        var pendingDraw = suppressionQueue.PendingDecision ?? throw new Exception("Missing queued draw fixture.");
        check(suppressionQueue.SubmitDecision(pendingDraw.Id, EffectDecisionAnswer.Accept),
            "A08 initially legal optional draw is accepted");
        suppressionQueue.Tick(); // publish the answer command for execution
        ushort suppression = FlipEffectSuppressionRules.RoyalCommand;
        suppressed.Spells[1, 0] = new Card(suppression, suppression, "Suppression fixture", "", 21u << 20);
        Drain(suppressionQueue);
        check(suppressed.DeckCount(0) == 1 && suppressed.Hand.Count == 0 &&
            suppressed.EffectDecisionHistory.Count == 0 &&
            suppressionQueue.Trace.All(entry => entry.Entry.Command is not DrawCommand) &&
            suppressionQueue.Trace.Last().Result?.Outcome == CommandOutcome.Skipped,
            "A08 a negated accepted decision never admits its dependent draw");

        var sangan = Monster(47);
        var search = DemoDuel.FromPartialObservation(0, [raigeki], Slots(), Slots(sangan), 8000, side1Deck: [normal]);
        var sq = search.CreatePrimitiveActionQueue();
        search.TryQueueRaigekiFromHand(0, out _);
        Drain(sq);
        bool blocked = false;
        try { sq.Enqueue(new DrawCommand(0)); } catch (InvalidOperationException) { blocked = true; }
        check(blocked && search.PendingCardSelection is not null &&
            search.ResolveCardSelection(search.PendingCardSelection.EligibleCards.Single().SourceIndex, out _),
            "A02 legacy pending choice also blocks new queue admissions");
    }
}
