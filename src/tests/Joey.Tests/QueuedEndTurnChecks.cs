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

    private sealed class ReturnGate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not ReturnToHandCommand || Release;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 40 && queue.IsBusy; i++) queue.Tick();
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
                !duel.TryQueueEndTurn(out _) && !duel.TryQueueTurnDraw(out _) &&
                !duel.TryQueueEndTurnDiscard(-1, out _) && !duel.TryQueueEndTurnDiscard(8, out _) &&
                !session.Execute("discard 0 0").Success && duel.Hand.Count == 8 &&
                duel.Graveyard(side).Count == 0,
                "A07 end phase accepts only valid typed discard choices, without early mutation");
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
            check(duel.Turn == turn && !duel.CanDraw &&
                ActionTraceProjection.ReadExecuted(session.Queue.Trace).All(row => row.Mapping == "unmapped"),
                "A07 releasing discard leaves the native turn advance for another tick");
            Drain(session.Queue);
            check(duel.Turn == turn + 1 && duel.ActivePlayer == 1 - side && duel.Phase == DemoPhase.Main &&
                !duel.HasQueuedEndDiscard && duel.CanDraw,
                "A07 turn advance releases the next player's actions");
            var advance = ActionTraceProjection.ReadExecuted(session.Queue.Trace).Single(row => row.Mapping == "mapped");
            check(advance.Command == "AdvanceTurnCommand" &&
                advance.Args!.SequenceEqual(new ushort[] { (ushort)((side << 15) | 0x03), 0, 0, 0 }),
                "A07 opcode 03 uses the side ending its turn and zero operands");
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
        CheckCpuTurn(check);
        CheckEndPhaseReturns(check, card);
    }

    private static void CheckEndPhaseReturns(Action<bool, string> check, Card filler)
    {
        var beast = new Card(22, 22, "The Wicked Worm Beast", "", (11u << 20) | (3u << 25) | (140u << 9));
        var equip = new Card(307, 307, "Beast Fangs", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        {
            int opponent = 1 - side;
            Card?[] own = [beast, beast, beast, null, null];
            Card?[] enemy = [beast, null, null, null, null];
            var duel = DemoDuel.FromPartialObservation(side, [filler, filler, filler, filler, filler, equip],
                side == 0 ? own : enemy, side == 1 ? own : enemy, 8000, equipmentContextResolved: true);
            duel.DefensePosition[side, 1] = true;
            duel.MonsterFaceDown[side, 2] = duel.DefensePosition[side, 2] = true;
            check(duel.Equip(5, side, 0, out _), "End Phase return fixture attaches Beast Fangs");
            var gate = new ReturnGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            check(duel.TryCaptureCardTarget(side, DuelZone.Monster, 0, out var first) &&
                duel.TryQueueEndTurn(out _), "The Wicked Worm Beast uses the common queued end turn");
            queue.Tick();
            queue.Tick();
            check(queue.Active?.Command is ReturnToHandCommand &&
                duel.TryCaptureCardTarget(side, DuelZone.Hand, 5, out var returned) && returned.Occurrence == first.Occurrence &&
                duel.Monsters[side, 1]?.Id == beast.Id && duel.Spells[side, 0]?.Id == equip.Id &&
                duel.PendingEndDiscards == 0 && duel.ActivePlayer == side,
                "The first return waits with its occurrence moved, before linked equipment and the next return");
            gate.Release = true;
            Drain(queue);
            check(duel.Phase == DemoPhase.End && duel.ActivePlayer == side && duel.PendingEndDiscards == 1 &&
                duel.HandFor(side).Count == 7 && duel.Graveyard(side).Single().Id == equip.Id &&
                duel.Spells[side, 0] is null && duel.MonsterFaceDown[side, 2] &&
                duel.Monsters[opponent, 0]?.Id == beast.Id,
                "Only the active player's revealed Beasts return and the final hand size opens the discard");
            var outcomes = queue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed).ToArray();
            check(outcomes.Count(entry => entry.Entry.Command is ReturnToHandCommand) == 2 &&
                outcomes.All(entry => entry.Result?.Outcome == CommandOutcome.Applied),
                "Every eligible End Phase return resolves through its own command");
            check(new CpuDuelController(side).Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Discard },
                "The CPU admits the resulting hand-limit discard");
            Drain(queue);
            check(duel.ActivePlayer == opponent && duel.HandFor(side).Count == 6,
                "Turn advance waits until the return and hand-limit discard finish");
        }

        var stale = DemoDuel.FromPartialObservation(0, [], [beast, null, null, null, null], new Card?[5], 8000);
        var staleQueue = stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueEndTurn(out _), "End Phase reserves a tracked return");
        staleQueue.Tick();
        var replacement = beast with { };
        stale.Monsters[0, 0] = replacement;
        Drain(staleQueue);
        check(ReferenceEquals(stale.Monsters[0, 0], replacement) && stale.HandFor(0).Count == 0 &&
            stale.ActivePlayer == 1 && staleQueue.Trace.Any(entry =>
                entry.Entry.Command is ReturnToHandCommand && entry.Result?.Outcome == CommandOutcome.Skipped),
            "A stale End Phase return leaves the replacement alone and lets the turn advance");
    }

    private static void CheckCpuTurn(Action<bool, string> check)
    {
        Card inert = new(25, 25, "Inert CPU fixture", "", 22u << 20);
        var duel = DemoDuel.FromPartialObservation(1, Enumerable.Repeat(inert, 7).ToArray(),
            new Card?[5], new Card?[5], 8000, side1Deck: [inert]);
        var queue = duel.CreatePrimitiveActionQueue();
        var cpu = new CpuDuelController(1);
        CpuAction[] actions = [CpuAction.Draw, CpuAction.BeginBattle, CpuAction.EndBattle, CpuAction.EndTurn,
            CpuAction.Discard, CpuAction.Discard];
        foreach (var action in actions)
        {
            var step = cpu.Step(duel);
            check(step.Status == CpuStepStatus.Acted && step.Action == action && queue.IsBusy &&
                queue.Trace.Last().Entry.Origin == ActionOrigin.Cpu,
                $"A07 CPU admits {action} to the duel's queue");
            Drain(queue);
        }
        check(duel.Turn == 3 && duel.ActivePlayer == 0 &&
            duel.HandFor(1).Count == DemoDuel.EndTurnHandLimit && duel.Graveyard(1).Count == 2,
            "A07 CPU finishes its turn after the actual discards and advances exactly once");
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
