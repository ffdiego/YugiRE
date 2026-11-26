using Joey.Core;

internal static class QueuedSummonSelfDestructionChecks
{
    private static Card Monster(ushort id, uint attack, uint defense = 800) =>
        new(id, id, "Summon fixture", "", (3u << 20) | (4u << 25) | ((attack / 10) << 9) | (defense / 10));

    private static Card?[] Slots(Card? first = null, Card? second = null) => [first, second, null, null, null];

    private sealed class SummonGate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            Release || command.Command is not SummonCommand;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Normal Summon fixture did not drain.");
    }

    private static void EndTurn(DemoDuel duel, ActionQueue queue)
    {
        if (!duel.TryQueueEndTurn(out _)) throw new InvalidOperationException("Fixture could not end turn.");
        Drain(queue);
    }

    public static void Run(Action<bool, string> check)
    {
        var boar = Monster(1118, 2000);
        var nuvia = Monster(1413, 2000);
        var ordinary = Monster(24, 1500);

        foreach (var card in new[] { boar, nuvia })
        {
            var summoned = DemoDuel.FromPartialObservation(1, [card], Slots(), Slots(), 8000);
            var gate = new SummonGate();
            var queue = summoned.CreatePrimitiveActionQueue(gate);
            check(summoned.TryQueueNormalPlacement(0, 0, false, out _) && summoned.Monsters[1, 0] is null,
                "Self-destruction Normal Summon admission leaves the card in hand");
            for (int i = 0; i < 20 && summoned.Monsters[1, 0] is null; i++) queue.Tick();
            queue.Tick();
            check(summoned.Monsters[1, 0] == card && summoned.Graveyard(1).Count == 0,
                "Self-destruction waits until the Normal Summon gate completes");
            gate.Release = true;
            Drain(queue);
            check(summoned.Monsters[1, 0] is null && summoned.Graveyard(1).Single() == card &&
                queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is DestroyCommand) == 1,
                "Normal Summon schedules exactly one typed self-destruction");

            var set = DemoDuel.FromPartialObservation(1, [card], Slots(ordinary, ordinary), Slots(), 8000,
                side1Deck: [new Card(0, 0, "Draw fixture", "", 22u << 20)]);
            set.MonsterFaceDown[0, 1] = set.DefensePosition[0, 1] = true;
            var setQueue = set.CreatePrimitiveActionQueue();
            var cpu = new CpuDuelController(1);
            check(cpu.Step(set).Action == CpuAction.Draw, "CPU draws before considering the self-destruction monster");
            Drain(setQueue);
            check(cpu.Step(set) is { Action: CpuAction.Set, Status: CpuStepStatus.Acted },
                "CPU Sets the monster instead of Normal Summoning it face-up");
            Drain(setQueue);
            check(set.MonsterFaceDown[1, 0] && set.Graveyard(1).Count == 0, "Set keeps the monster on the field");
            EndTurn(set, setQueue);
            EndTurn(set, setQueue);
            check(set.TryQueueChangePosition(0, out _), "Previously Set monster can be Flip Summoned");
            Drain(setQueue);
            bool isBoar = card.Id == boar.Id;
            check(!set.MonsterFaceDown[1, 0] && set.AttackValue(1, 0) == (isBoar ? 1000 : 1600) &&
                set.DefenseValue(1, 0) == 800 && set.Graveyard(1).Count == 0,
                "Flip Summon preserves the monster and counts both opposing positions in ATK");
            for (int slot = 0; slot < 2; slot++)
            {
                check(set.TryCaptureCardTarget(0, DuelZone.Monster, slot, out var target), "Capture opposing monster");
                setQueue.Enqueue(new DestroyCommand(0, target));
                Drain(setQueue);
                int expected = slot == 1 ? 2000 : isBoar ? 1000 : 1800;
                check(set.AttackValue(1, 0) == expected, "ATK recomputes after an opposing occurrence leaves");
            }
        }

        var trapHole = new Card(0, SummonTrapRules.TrapHole, "Trap Hole", "", 21u << 20);
        foreach (bool useTrap in new[] { false, true })
        {
            var duel = DemoDuel.FromPartialObservation(0, [trapHole], Slots(), Slots(), 8000, side1Deck: [nuvia]);
            var queue = duel.CreatePrimitiveActionQueue();
            check(duel.TryQueueSpellTrapSet(0, out _), "Self-destruction response fixture Sets Trap Hole");
            Drain(queue);
            EndTurn(duel, queue);
            check(duel.TryQueueTurnDraw(out _), "Nuvia is drawn for its Normal Summon");
            Drain(queue);
            check(duel.TryQueueNormalPlacement(0, 0, false, out _), "Nuvia Normal Summon admits a response");
            Drain(queue);
            var pending = duel.PendingSummonResponse ?? throw new InvalidOperationException("Missing Trap Hole response.");
            check(duel.Monsters[1, 0] == nuvia && duel.TryQueueSummonResponse(pending,
                useTrap ? pending.EligibleTraps.Single() : null, out _), "Nuvia remains until the summon response is resolved");
            Drain(queue);
            check(duel.Graveyard(1).Single() == nuvia && duel.Monsters[1, 0] is null &&
                duel.Graveyard(0).Count == (useTrap ? 1 : 0) && queue.Trace.Count(entry =>
                    entry.Stage == ActionTraceStage.Executed && entry.Entry.Command is DestroyCommand) == 1,
                "Passing triggers self-destruction and Trap Hole prevents a duplicate destruction");
        }

        var revival = DemoDuel.FromPartialObservation(1, [], Slots(ordinary), Slots(), 8000,
            side1Graveyard: [boar, nuvia], side1Deck: [boar]);
        var revivalQueue = revival.CreatePrimitiveActionQueue();
        check(revival.TryCaptureCardTarget(1, DuelZone.Graveyard, 0, out var boarGrave),
            "Capture Boar Soldier in the Graveyard");
        check(revival.TryCaptureCardTarget(1, DuelZone.Deck, 0, out var boarDeck),
            "Capture Boar Soldier in the Deck");
        revivalQueue.EnqueueBatch([new SpecialSummonCommand(1, boarGrave, 0), new SpecialSummonCommand(1, boarDeck, 0)]);
        Drain(revivalQueue);
        check(revival.Monsters[1, 0] is null && revival.Graveyard(1).Count == 2 && revival.DeckCount(1) == 1,
            "Boar Soldier's Flip Summon requirement prevents Special Summon without moving it");
        check(revival.TryCaptureCardTarget(1, DuelZone.Graveyard, 1, out var nuviaGrave), "Capture Nuvia for Special Summon");
        revivalQueue.Enqueue(new SpecialSummonCommand(1, nuviaGrave, 0));
        Drain(revivalQueue);
        check(revival.Monsters[1, 0] == nuvia && revival.AttackValue(1, 0) == 1800 &&
            revival.Graveyard(1).Single() == boar, "Nuvia survives Special Summon and applies its normal ATK penalty");
    }
}
