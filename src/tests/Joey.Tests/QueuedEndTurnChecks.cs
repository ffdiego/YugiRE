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

        var draws = DemoDuel.FromPartialObservation(0, [], new Card?[5], new Card?[5], 8000,
            side0Deck: [card, card], side1Deck: [card]);
        var drawSession = new DuelConsoleSession(draws);
        check(drawSession.Execute("draw 1").Enqueued, "A07 privileged draw remains available");
        Drain(drawSession.Queue);
        check(draws.HandFor(0).Count == 1 && draws.CanDraw && drawSession.Execute("turn draw").Enqueued &&
            draws.HandFor(0).Count == 1, "A07 legal draw admits without mutation after a privileged draw");
        Drain(drawSession.Queue);
        check(draws.HandFor(0).Count == 2 && !draws.CanDraw && !drawSession.Execute("turn draw").Success,
            "A07 legal draw consumes the once-per-turn permission");
        check(drawSession.Execute("turn end").Enqueued, "A07 end the first player's turn");
        Drain(drawSession.Queue);
        check(drawSession.Execute("turn draw").Enqueued, "A07 the next player gets a new legal draw");
        Drain(drawSession.Queue);
        check(draws.HandFor(1).Count == 1 && draws.HandFor(0).Count == 2,
            "A07 the legal draw operates on the active side's deck");
        check(drawSession.Execute("turn end").Enqueued, "A07 return to the empty deck");
        Drain(drawSession.Queue);
        check(drawSession.Execute("turn draw").Enqueued, "A07 admit the empty deck draw");
        Drain(drawSession.Queue);
        check(draws.Winner == 1 && drawSession.Queue.Trace.Last().Result?.Outcome == CommandOutcome.Applied,
            "A07 a legal empty deck draw applies the sandbox loss");

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

        CheckEndPhaseMessage(check, card);
    }

    private static void CheckEndPhaseMessage(Action<bool, string> check, Card filler)
    {
        ushort boardId = DestinyBoardRules.BoardResourceId;
        Card board = new(boardId, boardId, "End effect fixture", "", 21u << 20);
        var messages = DestinyBoardRules.MessageResourceIds
            .Select(id => new Card(id, id, $"Message {id}", "", 22u << 20)).ToArray();
        var duel = DemoDuel.FromPartialObservation(0, Enumerable.Repeat(filler, 7).ToArray(),
            new Card?[5], new Card?[5], 8000, side1Deck: [filler, messages[0], filler, messages[3]]);
        duel.Spells[1, 0] = board;
        var gate = new DiscardGate();
        var session = new DuelConsoleSession(duel, gate);
        check(duel.TryCaptureCardTarget(1, DuelZone.Deck, 1, out var source) &&
            session.Execute("turn end").Enqueued, "A07 admit an end-phase effect without mutation");
        Drain(session.Queue);
        check(duel.Turn == 1 && duel.Spells[1, 1] is null && duel.PendingEndDiscards == 1 &&
            session.Execute("discard-end 0").Enqueued, "A07 End Phase requests its mandatory discard before placement");
        session.Queue.Tick();
        check(duel.Spells[1, 1] is null && duel.PendingEndDiscards == 0 && session.Queue.PendingCount == 1,
            "A07 End Phase placement waits for the mandatory discard gate");
        gate.Release = true;
        session.Queue.Tick();
        session.Queue.Tick();
        check(duel.Turn == 1 && !duel.SpellFaceDown[1, 1] &&
            duel.TryCaptureCardTarget(1, DuelZone.SpellTrap, 1, out var placed) &&
            placed.Occurrence == source.Occurrence && duel.DeckSnapshot(1).SequenceEqual(new[] { filler, filler, messages[3] }),
            "A07 placement preserves the occurrence and remaining deck order before advance");
        Drain(session.Queue);
        check(duel.Turn == 2 && session.Execute("turn end").Enqueued, "A07 advance to the other side");
        Drain(session.Queue);
        check(duel.Turn == 3 && duel.Spells[1, 2] is null,
            "A07 no placement during the owner's End Phase");

        duel.Spells[1, 2] = messages[1];
        duel.Spells[1, 3] = messages[2];
        check(session.Execute("turn end").Enqueued, "A07 admit standing end-phase win condition");
        Drain(session.Queue);
        check(duel.Winner == 1 && duel.DestinyBoardWinner == 1 && duel.Turn == 3 &&
            duel.Spells[1, 4]?.Id == messages[3].Id && !session.Queue.IsBusy,
            "A07 completed end-phase win condition stops turn advance");
    }
}
