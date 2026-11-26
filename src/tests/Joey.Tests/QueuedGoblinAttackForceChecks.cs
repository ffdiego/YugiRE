using Joey.Core;

internal static class QueuedGoblinAttackForceChecks
{
    private static Card Monster(ushort id, uint attack, uint defense = 0) =>
        new(id, id, "Battle fixture", "", (15u << 20) | (4u << 25) | ((attack / 10) << 9) | (defense / 10));

    private static Card?[] Slots(Card? first = null, Card? second = null) => [first, second, null, null, null];

    private sealed class DefenseGate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            Release || command.Command.GetType().Name != "GoblinAttackForceDefenseCommand";
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Goblin Attack Force fixture did not drain.");
    }

    private static void EndTurn(DemoDuel duel, ActionQueue queue)
    {
        if (!duel.TryQueueEndTurn(out _)) throw new InvalidOperationException("Fixture could not end turn.");
        Drain(queue);
    }

    public static void Run(Action<bool, string> check)
    {
        var goblin = Monster(1344, 2300);
        foreach (int side in new[] { 0, 1 })
        {
            var duel = DemoDuel.FromPartialObservation(side, [], side == 0 ? Slots(goblin, goblin) : Slots(),
                side == 1 ? Slots(goblin, goblin) : Slots(), 8000);
            var gate = new DefenseGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            if (side == 0)
            {
                EndTurn(duel, queue);
                EndTurn(duel, queue);
            }
            check(duel.BeginBattle(out _) && duel.TryQueueDirectAttack(0, out _), "Goblin Attack Force declares its attack");
            Drain(queue);
            check(!duel.DefensePosition[side, 0] && duel.TryQueueBattlePhaseChange(false, out _),
                "Goblin Attack Force remains in attack until Battle Phase ends");
            for (int i = 0; i < 25 && !duel.DefensePosition[side, 0]; i++) queue.Tick();
            queue.Tick();
            check(duel.DefensePosition[side, 0] && !duel.DefensePosition[side, 1] && duel.Phase == DemoPhase.Battle &&
                queue.IsBusy, "Only the attacking occurrence turns and its gate precedes Main Phase 2");
            gate.Release = true;
            Drain(queue);
            check(duel.Phase == DemoPhase.Main && !duel.TryQueueChangePosition(0, out _) && duel.CanChangePosition(1),
                "Goblin's position is locked in Main Phase 2");
            EndTurn(duel, queue);
            EndTurn(duel, queue);
            check(duel.ActivePlayer == side && !duel.CanChangePosition(0) && !duel.TryQueueChangePosition(0, out _),
                "Goblin's position stays locked during the next turn of the same player");
            EndTurn(duel, queue);
            EndTurn(duel, queue);
            check(duel.TryQueueChangePosition(0, out _), "Goblin's position can change on the following turn");
            Drain(queue);
            check(!duel.DefensePosition[side, 0], "Expired lock restores normal manual position change");
        }

        var worm = Monster(22, 1400);
        var end = DemoDuel.FromPartialObservation(1, [], Slots(), Slots(goblin, worm), 8000);
        var endQueue = end.CreatePrimitiveActionQueue();
        check(end.BeginBattle(out _) && end.TryQueueDirectAttack(0, out _), "End Turn shortcut fixture attacks");
        Drain(endQueue);
        EndTurn(end, endQueue);
        var commands = endQueue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed)
            .Select(entry => entry.Entry.Command.GetType().Name).ToList();
        int defense = commands.IndexOf("GoblinAttackForceDefenseCommand");
        check(end.ActivePlayer == 0 && end.DefensePosition[1, 0] && end.HandFor(1).Single() == worm &&
            defense >= 0 && commands.IndexOf(nameof(ReturnToHandCommand)) > defense,
            "End Turn from Battle Phase resolves Goblin before the Wicked Worm Beast End Phase return");

        ushort id = AttackTrapRules.NegateAttack;
        var trap = new Card(id, id, "Negate Attack", "", 21u << 20);
        var negated = DemoDuel.FromPartialObservation(0, [trap], Slots(), Slots(goblin), 8000);
        var negateQueue = negated.CreatePrimitiveActionQueue();
        check(negated.TryQueueSpellTrapSet(0, out _), "Negate Attack is Set for Goblin's declaration");
        Drain(negateQueue);
        EndTurn(negated, negateQueue);
        check(negated.BeginBattle(out _) && negated.TryQueueDirectAttack(0, out _), "Goblin opens a response window");
        Drain(negateQueue);
        var response = negated.PendingAttackResponse ?? throw new InvalidOperationException("No pending Negate Attack.");
        check(negated.TryQueueAttackResponse(response, response.EligibleTraps.Single(), out _),
            "Negate Attack ends Battle Phase using the same queue");
        Drain(negateQueue);
        check(negated.Phase == DemoPhase.Main && negated.DefensePosition[1, 0] && negated.Life[0] == 8000,
            "A negated declaration counts for Goblin's position change under the sandbox policy");
    }
}
