using Joey.Core;

internal static class ActionQueueFlipChecks
{
    private sealed class Gate(bool transfersOnly = false) : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            Release || transfersOnly && command.Command is not (DiscardCommand or DrawCommand);
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
        CheckBattleMorphingJar(check);
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

        var jar2 = DemoDuel.FromPartialObservation(0, [], Slots(Monster(FlipEffectRules.MorphingJar2)), Slots(), 8000);
        jar2.MonsterFaceDown[0, 0] = jar2.DefensePosition[0, 0] = true;
        var jq2 = jar2.CreatePrimitiveActionQueue();
        check(jar2.TryQueueChangePosition(0, out _) && jar2.MonsterFaceDown[0, 0],
            "C12 migrated Jar #2 admission leaves field unchanged");
        Drain(jq2);
        check(jar2.LastMorphingJar2[0].SpecialSummoned == 1 && jar2.MonsterFaceDown[0,0],
            "C12 Jar #2 completes through queue");
    }

    private static void CheckBattleMorphingJar(Action<bool, string> check)
    {
        var jar = Monster(FlipEffectRules.MorphingJar);
        var ordinary = Monster(24);
        var attacker = ordinary with { Properties = ordinary.Properties | (140u << 9) };
        foreach (int attackingSide in new[] { 0, 1 })
        foreach (var (size0, size1) in new[] { (6, 6), (2, 6), (6, 2), (2, 2), (0, 0) })
        {
            int owner = 1 - attackingSide;
            Card[] Deck(int side, int count) => Enumerable.Range(0, count + (side == owner ? 1 : 0))
                .Select(index => Monster((ushort)(3000 + 10 * side + index))).ToArray();
            var duel = DemoDuel.FromPartialObservation(attackingSide, [ordinary, ordinary],
                Slots(attackingSide == 0 ? attacker : jar), Slots(attackingSide == 1 ? attacker : jar), 8000,
                side0Deck: Deck(0, size0), side1Deck: Deck(1, size1));
            duel.NextTurn();
            duel.Draw(out _);
            duel.NextTurn();
            duel.MonsterFaceDown[owner, 0] = duel.DefensePosition[owner, 0] = true;
            List<DemoHandTransfer> transfers = [];
            duel.HandTransferred += transfers.Add;
            var gate = new Gate(transfersOnly: true);
            var queue = duel.CreatePrimitiveActionQueue(gate);
            check(duel.BeginBattle(out _) && duel.TryQueueTargetedAttack(0, 0, out _) &&
                duel.MonsterFaceDown[owner, 0] && transfers.Count == 0, "Morphing Jar battle admission is nonmutating");
            for (int i = 0; i < 25 && transfers.Count == 0; i++) queue.Tick();
            queue.Tick();
            check(transfers.Count == 1 && !transfers[0].FromDeck && queue.IsBusy &&
                duel.Monsters[owner, 0] == jar && !duel.MonsterFaceDown[owner, 0],
                "Morphing Jar discard gate holds the revealed defender on the field");
            gate.Release = true;
            Drain(queue);
            int? winner = (size0 < 5) == (size1 < 5) ? null : size0 < 5 ? 1 : 0;
            check(duel.HandFor(0).Count == Math.Min(5, size0) && duel.HandFor(1).Count == Math.Min(5, size1) &&
                transfers.Take(3).All(transfer => !transfer.FromDeck) &&
                transfers.Skip(3).Count() == Math.Min(5, size0) + Math.Min(5, size1) &&
                transfers.Skip(3).All(transfer => transfer.FromDeck) && duel.Winner == winner &&
                duel.IsDraw == (size0 < 5 && size1 < 5) &&
                duel.Monsters[owner, 0] is null && duel.Graveyard(owner).Last() == jar,
                "Battle Morphing Jar discards both hands before draws, then resolves deck-out and battle removal");
            var commands = queue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed)
                .Select(entry => entry.Entry.Command.GetType().Name).ToList();
            int finish = commands.IndexOf("FinishMorphingJarCommand");
            check(finish > commands.LastIndexOf(nameof(DiscardCommand)) &&
                finish > commands.LastIndexOf(nameof(DrawCommand)) &&
                commands.IndexOf("FinishBattleRemovalCommand") > finish,
                "Battle Morphing Jar finalizes after all movements and before the marked defender is removed");
        }

        var exodia = ExodiaRules.RequiredNames.Select((name, index) =>
            Monster((ushort)(3100 + index)) with { Name = name }).ToArray();
        var ending = DemoDuel.FromPartialObservation(1, [], Slots(jar), Slots(attacker), 8000,
            side0Deck: exodia, side1Deck: [ordinary, ordinary]);
        ending.MonsterFaceDown[0, 0] = ending.DefensePosition[0, 0] = true;
        var endingQueue = ending.CreatePrimitiveActionQueue();
        check(ending.BeginBattle(out _) && ending.TryQueueTargetedAttack(0, 0, out _), "Battle Jar Exodia fixture");
        Drain(endingQueue);
        check(ending.ExodiaWinner == 0 && ending.Winner == 0 && ending.HandFor(1).Count == 2 &&
            ending.Graveyard(0).Single() == jar,
            "Battle Jar completes the other player's draws and removal after Exodia");
    }
}
