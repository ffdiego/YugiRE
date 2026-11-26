using Joey.Core;

internal static class QueuedStandbyPositionChecks
{
    private sealed class DrawGate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            Release || command.Command is not TurnDrawCommand;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 40; tick++) queue.Tick();
        if (queue.IsBusy || queue.Trace.Any(e => e.Result?.Outcome == CommandOutcome.Rejected))
            throw new InvalidOperationException("Standby position fixture did not resolve.");
    }

    public static void Run(Action<bool, string> check)
    {
        var zebra = new Card(1113, 1113, "Dark Zebra", "", (11u << 20) | (4u << 25) | (180u << 9) | 40u);
        var normal = new Card(24, 24, "Normal monster", "", (3u << 20) | (4u << 25) | (100u << 9) | 80u);
        foreach (int owner in new[] { 0, 1 })
        foreach (var (defense, hidden, companion) in new[]
        {
            (false, false, false), (true, false, false), (true, true, false), (false, false, true)
        })
        {
            Card?[] own = [null, zebra, companion ? normal : null, null, null];
            Card?[] other = [normal, null, null, null, null];
            var duel = DemoDuel.FromPartialObservation(owner, [], owner == 0 ? own : other, owner == 1 ? own : other,
                8000, side0Deck: owner == 0 ? [normal, normal] : [], side1Deck: owner == 1 ? [normal, normal] : []);
            duel.DefensePosition[owner, 1] = defense;
            duel.MonsterFaceDown[owner, 1] = hidden;
            duel.MonsterFaceDown[owner, 2] = companion;
            var gate = new DrawGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            int positionEvents = 0;
            duel.MonsterPositionChanged += _ => positionEvents++;
            check(duel.TryQueueTurnDraw(out _), "Dark Zebra fixture admits the turn draw");
            queue.Tick();
            queue.Tick();
            check(duel.DefensePosition[owner, 1] == defense && positionEvents == 0,
                "Dark Zebra waits for the draw gate before rotating");
            gate.Release = true;
            Drain(queue);
            bool triggers = !hidden && !companion;
            check(duel.DefensePosition[owner, 1] == (triggers || defense) &&
                duel.MonsterFaceDown[owner, 1] == hidden &&
                duel.CanChangePosition(1) == !triggers &&
                positionEvents == (triggers && !defense ? 1 : 0),
                "A lone revealed Dark Zebra goes to defense and stays locked for the turn");

            if (!triggers) continue;
            check(duel.TryQueueNormalPlacement(0, 2, false, out _), "Dark Zebra fixture adds a companion");
            Drain(queue);
            check(duel.TryQueueEndTurn(out _), "End Dark Zebra's locked turn");
            Drain(queue);
            check(duel.TryQueueEndTurn(out _), "Advance to its owner's next turn");
            Drain(queue);
            check(duel.TryQueueTurnDraw(out _), "Draw while Dark Zebra has a companion");
            Drain(queue);
            check(duel.CanChangePosition(1) && duel.TryQueueChangePosition(1, out _),
                "The lock expires and another monster prevents a new lock");
            Drain(queue);
            check(!duel.DefensePosition[owner, 1], "Unlocked Dark Zebra can change to attack");
        }
    }
}
