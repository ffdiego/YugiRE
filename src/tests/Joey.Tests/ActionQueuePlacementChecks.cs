using Joey.Core;

internal static class ActionQueuePlacementChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new Exception("Placement pilot did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        Card low = new(24, 24, "Fixture monster", "", (3u << 20) | (4u << 25));
        Card high = new(81, 81, "Fixture boss", "", (3u << 20) | (7u << 25));
        Card spell = new(336, 336, "Fixture spell", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (bool faceDown in new[] { false, true })
        {
            Card?[] occupied = [low, low, low, low, low];
            Card?[] empty = new Card?[5];
            (DemoDuel Duel, ActionQueue Queue, List<DemoMonsterTribute> Paid, List<DemoNormalSummon> Placed) Run(bool delayed)
            {
                var duel = DemoDuel.FromPartialObservation(side, [high, low],
                    side == 0 ? occupied : empty, side == 1 ? occupied : empty, 8000);
                var gate = new Gate();
                var queue = duel.CreatePrimitiveActionQueue(delayed ? gate : null);
                List<DemoMonsterTribute> paid = [];
                List<DemoNormalSummon> placed = [];
                duel.MonsterTributed += paid.Add;
                duel.MonsterNormalSummoned += placed.Add;
                Card physical = duel.Hand[0];
                check(!duel.TryQueueNormalPlacement(0, 1, faceDown, out _, [0, 0]) &&
                    !duel.TryQueueNormalPlacement(0, 3, faceDown, out _, [0, 1]) &&
                    duel.Hand.Count == 2 && queue.PendingCount == 0 && paid.Count == 0,
                    "A02 placement rejects repeated costs and occupied destination atomically");
                check(duel.TryQueueNormalPlacement(0, 1, faceDown, out _, [0, 1]) &&
                    duel.Hand.Count == 2 && duel.Graveyard(side).Count == 0 &&
                    !duel.EndTurn(out _), "A02 placement admitted without paying or advancing turn");
                if (delayed)
                {
                    // Release each stage separately; execute the following stage
                    // with the gate closed so no later mutation can slip through.
                    queue.Tick();
                    check(paid.Count == 0 && queue.PendingCount == 3, "A02 preparation only emits commands");
                    gate.Release = true; queue.Tick(); gate.Release = false;
                    queue.Tick(); queue.Tick();
                    check(paid.Count == 1 && duel.Monsters[side, 0] is null &&
                        duel.Monsters[side, 1] is not null && placed.Count == 0,
                        "A02 first tribute waits without double payment");
                    gate.Release = true; queue.Tick(); gate.Release = false;
                    queue.Tick();
                    check(paid.Count == 2 && placed.Count == 0 && duel.Hand.Count == 2,
                        "A02 second tribute is distinct from placement");
                    gate.Release = true; queue.Tick(); gate.Release = false;
                    queue.Tick();
                    check(placed.Count == 1 && ReferenceEquals(duel.Monsters[side, 1], physical) &&
                        queue.IsBusy, "A02 summon commits once after both tribute gates");
                    gate.Release = true;
                }
                Drain(queue);
                check(paid.Select(t => t.Zone).SequenceEqual([0, 1]) && placed.Count == 1 &&
                    placed[0].FaceDown == faceDown && ReferenceEquals(placed[0].Card, physical) &&
                    duel.Graveyard(side).SequenceEqual([low, low]) && duel.Hand.Single() == low &&
                    duel.MonsterFaceDown[side, 1] == faceDown && duel.DefensePosition[side, 1] == faceDown &&
                    !duel.CanSummonFromHand(0) && !duel.CanChangePosition(1),
                    "A02 normal placement preserves cost, identity, face and turn locks");
                return (duel, queue, paid, placed);
            }
            var immediate = Run(false);
            var delayed = Run(true);
            check(immediate.Queue.Trace.SequenceEqual(delayed.Queue.Trace) &&
                immediate.Paid.SequenceEqual(delayed.Paid) && immediate.Placed.SequenceEqual(delayed.Placed),
                "A02 immediate and gated placement have identical trace and events");
        }
        var simple = DemoDuel.FromPartialObservation(0, [low, spell], new Card?[5], new Card?[5], 8000);
        var simpleQueue = simple.CreatePrimitiveActionQueue();
        check(simple.TryQueueNormalPlacement(0, 2, false, out _), "A02 no-tribute normal summon admitted");
        Drain(simpleQueue);
        check(simple.Monsters[0, 2] == low && simple.TryQueueSpellTrapSet(0, out _),
            "A02 normal summon allows Spell/Trap Set afterward");
        Drain(simpleQueue);
        check(simple.Spells[0, 0] == spell && simple.SpellFaceDown[0, 0] && simple.Hand.Count == 0,
            "A02 Set moves Spell into matching zone");
    }
}
