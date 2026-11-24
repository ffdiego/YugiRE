using System.Text.Json;
using Joey.Core;

internal static class QueuedEndTurnChecks
{
    private sealed class DiscardGate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not EndTurnDiscardCommand || Release;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 12 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("End turn did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        Card card = new(24, 24, "Turn fixture", "", (3u << 20) | (4u << 25));
        foreach (int side in new[] { 0, 1 })
        {
            var duel = DemoDuel.FromPartialObservation(side, Enumerable.Repeat(card, 8).ToArray(),
                new Card?[5], new Card?[5], 8000);
            int turn = duel.Turn;
            var gate = new DiscardGate();
            var session = new DuelConsoleSession(duel, gate);
            check(duel.TryCaptureCardTarget(side, DuelZone.Hand, 7, out var last) &&
                session.Execute("turn end").Enqueued && duel.Phase == DemoPhase.Main,
                "A07 end-turn admission captures the turn without mutation");
            Drain(session.Queue);
            check(duel.Turn == turn && duel.Phase == DemoPhase.End && duel.PendingEndDiscards == 2 &&
                !duel.DiscardForEndTurn(0, out _) && !duel.EndTurn(out _) && !session.Execute("discard 0 0").Success,
                "A07 end phase requests typed discards and blocks legacy or unrelated producers");
            var incomplete = new DuelConsoleScript(session, []);
            using (var trace = JsonDocument.Parse(session.Execute("trace").Message))
                check(incomplete.Advance().Status == DuelScriptStatus.WaitingForDecision &&
                    !trace.RootElement.GetProperty("complete").GetBoolean(),
                    "A07 a mandatory discard keeps script and trace incomplete");
            check(session.Execute("discard-end 7").Enqueued && duel.Hand.Count == 8,
                "A07 discard choice is read-only at admission");
            session.Queue.Tick();
            check(duel.Turn == turn && duel.Hand.Count == 7 && duel.PendingEndDiscards == 1 &&
                duel.TryCaptureCardTarget(side, DuelZone.Graveyard, 0, out var discarded) &&
                discarded.Occurrence == last.Occurrence && session.Queue.IsBusy,
                "A07 discard preserves the chosen occurrence while the gate is active");
            gate.Release = true;
            Drain(session.Queue);

            gate.Release = false;
            check(session.Execute("discard-end 0").Enqueued, "A07 accept the last required discard");
            session.Queue.Tick();
            check(duel.Turn == turn && duel.Phase == DemoPhase.End &&
                duel.Hand.Count == 6 && session.Queue.PendingCount == 1,
                "A07 hand reaches the limit before the queued turn advance");
            gate.Release = true;
            session.Queue.Tick();
            check(duel.Turn == turn && !duel.CanDraw, "A07 releasing discard leaves the advance for another tick");
            Drain(session.Queue);
            check(duel.Turn == turn + 1 && duel.ActivePlayer == 1 - side && duel.Phase == DemoPhase.Main &&
                !duel.HasQueuedEndDiscard && duel.CanDraw,
                "A07 turn advance releases the next player's actions");
        }

        var plain = DemoDuel.FromPartialObservation(0, [card], new Card?[5], new Card?[5], 8000);
        var plainQueue = plain.CreatePrimitiveActionQueue();
        plainQueue.Enqueue(new EndTurnCommand(1, 1));
        Drain(plainQueue);
        check(plain.Turn == 1 && plainQueue.Trace.Last().Result?.Outcome == CommandOutcome.Rejected,
            "A07 wrong side cannot enter End Phase");
        check(plain.TryQueueEndTurn(out _), "A07 admit an ordinary end turn");
        plainQueue.Tick();
        check(plain.Turn == 1 && plain.Phase == DemoPhase.End && plainQueue.PendingCount == 1,
            "A07 ordinary end turn keeps phase entry and side change separate");
        Drain(plainQueue);
        check(plain.Turn == 2 && plain.ActivePlayer == 1 && plain.Phase == DemoPhase.Main,
            "A07 ordinary end turn advances through the second command");

        var legacy = DemoDuel.FromPartialObservation(0, [], new Card?[5], new Card?[5], 8000);
        ushort id = DestinyBoardRules.BoardResourceId;
        legacy.Spells[1, 0] = new Card(id, id, "End effect fixture", "", 21u << 20);
        var legacyQueue = legacy.CreatePrimitiveActionQueue();
        check(!legacy.TryQueueEndTurn(out _) && legacyQueue.Trace.Count == 0 && legacy.Phase == DemoPhase.Main &&
            legacy.EndTurn(out _) && legacy.ActivePlayer == 1,
            "A07 an unmigrated end-phase effect stays on the existing resolver");
    }
}
