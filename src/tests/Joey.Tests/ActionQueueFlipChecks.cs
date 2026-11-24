using Joey.Core;

internal static class ActionQueueFlipChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }
    private static Card Monster(ushort id) => new(id, id, $"Fixture {id}", "", (3u << 20) | (4u << 25));
    private static Card?[] Slots(Card? card = null) => [card, null, null, null, null];
    private static void Drain(ActionQueue q)
    {
        for (int i = 0; i < 100 && q.IsBusy; i++) q.Tick();
        if (q.IsBusy) throw new Exception("FLIP queue did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        var jar = Monster(FlipEffectRules.MorphingJar);
        var ordinary = Monster(24);
        Card[] Deck(int count) => Enumerable.Range(0, count).Select(i => Monster((ushort)(3000 + i))).ToArray();

        foreach (int side in new[] { 0, 1 })
        foreach (var sizes in new[] { (6, 6), (2, 6), (6, 2), (2, 2) })
        {
            DemoDuel State()
            {
                var d = DemoDuel.FromPartialObservation(side, [ordinary, ordinary],
                    side == 0 ? Slots(jar) : Slots(), side == 1 ? Slots(jar) : Slots(), 8000,
                    side0Deck: Deck(sizes.Item1 + (side == 1 ? 1 : 0)),
                    side1Deck: Deck(sizes.Item2 + (side == 0 ? 1 : 0)));
                // Give opponent one hand card via the existing normal draw
                // and cycle back. This preserves the specified deck sizes.
                d.NextTurn(); d.Draw(out _); d.NextTurn();
                d.MonsterFaceDown[side, 0] = d.DefensePosition[side, 0] = true;
                return d;
            }
            var legacy = State();
            List<DemoHandTransfer> legacyEvents = [];
            legacy.HandTransferred += legacyEvents.Add;
            check(legacy.ChangePosition(0, out _), "A02 legacy Morphing Jar comparison fixture");
            (DemoDuel Duel, ActionQueue Queue, List<DemoHandTransfer> Events) RunPilot(bool delayed)
            {
                var d = State();
                var gate = new Gate();
                var q = d.CreatePrimitiveActionQueue(delayed ? gate : null);
                List<DemoHandTransfer> events = [];
                d.HandTransferred += events.Add;
                check(d.TryQueueChangePosition(0, out _) && d.MonsterFaceDown[side, 0] &&
                    events.Count == 0 && !d.EndTurn(out _), "A02 FLIP admission leaves state unchanged");
                if (delayed)
                {
                    q.Tick(); q.Tick();
                    check(!d.MonsterFaceDown[side, 0] && !d.DefensePosition[side, 0] &&
                        events.Count == 0 && q.IsBusy, "A02 reveal commits once before FLIP effect");
                    gate.Release = true;
                    q.Tick();
                    for (int i = 0; i < 90 && q.IsBusy; i++)
                    {
                        gate.Release = false;
                        q.Tick();
                        int count = events.Count;
                        bool wasActive = q.Active is not null;
                        q.Tick();
                        // A continuation tick may enqueue work but never move a card.
                        if (wasActive)
                            check(events.Count == count, "A02 active FLIP operation waits without repeated movement");
                        gate.Release = true; q.Tick();
                    }
                }
                Drain(q);
                check(d.HandFor(0).SequenceEqual(legacy.HandFor(0)) &&
                    d.HandFor(1).SequenceEqual(legacy.HandFor(1)) &&
                    d.Graveyard(0).SequenceEqual(legacy.Graveyard(0)) &&
                    d.Graveyard(1).SequenceEqual(legacy.Graveyard(1)) &&
                    d.DeckSnapshot(0).SequenceEqual(legacy.DeckSnapshot(0)) &&
                    d.DeckSnapshot(1).SequenceEqual(legacy.DeckSnapshot(1)) &&
                    d.Winner == legacy.Winner && d.IsDraw == legacy.IsDraw &&
                    events.SequenceEqual(legacyEvents) &&
                    q.Trace.Count(x => x.Stage == ActionTraceStage.Executed && x.Entry.Command is DiscardCommand) == 3 &&
                    q.Trace.Count(x => x.Stage == ActionTraceStage.Executed && x.Entry.Command is DrawCommand) ==
                        Math.Min(5, sizes.Item1) + Math.Min(5, sizes.Item2),
                    "A02 Morphing Jar preserves legacy ordering, exact card counts and deck-out");
                return (d, q, events);
            }
            var immediate = RunPilot(false);
            var delayed = RunPilot(true);
            check(immediate.Queue.Trace.SequenceEqual(delayed.Queue.Trace) &&
                immediate.Events.SequenceEqual(delayed.Events), "A02 Morphing Jar gate-independent trace");
        }

        var normal = DemoDuel.FromPartialObservation(0, [], Slots(ordinary), Slots(), 8000);
        var normalQueue = normal.CreatePrimitiveActionQueue();
        check(normal.TryQueueChangePosition(0, out _), "A02 ordinary face-up position admitted");
        Drain(normalQueue);
        check(normal.DefensePosition[0, 0] && !normal.TryQueueChangePosition(0, out _) &&
            normalQueue.Trace.Any(t => t.Entry.Command is ChangePositionCommand), "A02 position lock enforced");

        var immortal = DemoDuel.FromPartialObservation(0, [], Slots(Monster(FlipEffectRules.ImmortalOfThunder)), Slots(), 8000);
        immortal.MonsterFaceDown[0, 0] = immortal.DefensePosition[0, 0] = true;
        var iq = immortal.CreatePrimitiveActionQueue();
        check(immortal.TryQueueChangePosition(0, out _), "A02 Immortal FLIP admitted");
        Drain(iq);
        check(immortal.Life[0] == 11000 && iq.Trace.Count(t => t.Stage == ActionTraceStage.Executed &&
            t.Entry.Command is HealCommand) == 1, "A02 Immortal FLIP gain is its own command");

        var unsupported = DemoDuel.FromPartialObservation(0, [], Slots(Monster(FlipEffectRules.MorphingJar2)), Slots(), 8000);
        unsupported.MonsterFaceDown[0, 0] = unsupported.DefensePosition[0, 0] = true;
        unsupported.CreatePrimitiveActionQueue();
        check(!unsupported.TryQueueChangePosition(0, out _) && unsupported.MonsterFaceDown[0, 0],
            "A02 unmigrated bulk FLIP rejected before reveal");
    }
}
