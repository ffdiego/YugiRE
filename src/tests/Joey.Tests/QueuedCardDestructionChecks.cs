using Joey.Core;

internal static class QueuedCardDestructionChecks
{
    private static Card Spell(ushort id) => new(id, id, $"Spell {id}", "", 22u << 20);
    private static readonly Card Destruction = Spell(DrawSpellRules.CardDestruction);
    private static readonly Card Filler = Spell(342);

    private sealed class Gate : IPresentationGate
    {
        public bool HoldDiscards { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            !HoldDiscards || command.Command is not DiscardCommand;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Card Destruction did not finish.");
    }

    private static (DemoDuel Duel, ActionQueue Queue) Setup(int side, bool set, Card[] ownHand,
        Card[] opposingHand, Card[] ownDraws, Card[] opposingDraws, Gate? gate = null)
    {
        Card[] ownDeck = [Destruction, .. ownHand, .. ownDraws];
        var duel = DemoDuel.FromPartialObservation(1 - side, opposingHand, new Card?[5], new Card?[5], 8000,
            side0Deck: side == 0 ? ownDeck : opposingDraws, side1Deck: side == 1 ? ownDeck : opposingDraws);
        if (!duel.EndTurn(out _) || !duel.Draw(out _)) throw new InvalidOperationException("Card Destruction setup draw failed.");
        var queue = duel.CreatePrimitiveActionQueue(gate);
        queue.EnqueueBatch(Enumerable.Range(0, ownHand.Length).Select(_ => new DrawCommand(side)));
        Drain(queue);
        if (set)
        {
            if (!duel.TryQueueSpellTrapSet(0, out _)) throw new InvalidOperationException("Card Destruction setup Set failed.");
            Drain(queue);
        }
        return (duel, queue);
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool set in new[] { false, true })
        {
            var gate = new Gate { HoldDiscards = true };
            var (duel, queue) = Setup(side, set, [Destruction, Filler], [Filler], [Filler, Filler], [Filler], gate);
            DuelZone zone = set ? DuelZone.SpellTrap : DuelZone.Hand;
            int traceStart = queue.Trace.Count;
            check(duel.TryCaptureCardTarget(side, zone, 0, out var source), "Card Destruction has a tracked source");
            check(!(set ? duel.ActivateSetSpell(0, out _) : duel.Play(0, out _)) &&
                duel.CanQueueFixedSpellActivation(zone, 0) &&
                duel.TryQueueActivation(0, set, out _), "Card Destruction uses the common queue producer exclusively");

            for (int tick = 0; queue.Active?.Command is not DiscardCommand && tick < 15; tick++) queue.Tick();
            check(queue.Active?.Command is DiscardCommand first && first.Side == side &&
                duel.HandFor(side).Count == 1 && duel.HandFor(1 - side).Count == 1 &&
                duel.DeckCount(side) == 2 && duel.DeckCount(1 - side) == 1,
                "Card Destruction waits at the first discard before discarding again or drawing");
            gate.HoldDiscards = false;
            Drain(queue);
            var commands = queue.Trace.Skip(traceStart).Where(entry => entry.Stage == ActionTraceStage.Executed)
                .Select(entry => entry.Entry.Command).ToArray();
            check(commands is [_, SendToGraveyardCommand spend, DiscardCommand, DiscardCommand, DiscardCommand,
                    DrawCommand, DrawCommand, DrawCommand, _] &&
                spend.Target == source && duel.TryCaptureCardTarget(side, DuelZone.Graveyard, 0, out var spent) &&
                spent.Occurrence == source.Occurrence &&
                commands.OfType<DiscardCommand>().Select(command => command.Side).SequenceEqual([side, side, 1 - side]) &&
                commands.OfType<DrawCommand>().Select(command => command.Side).SequenceEqual([side, side, 1 - side]) &&
                duel.Graveyard(side).Select(card => card.Id).SequenceEqual([Destruction.Id, Destruction.Id, Filler.Id]) &&
                duel.HandFor(side).Count == 2 && duel.HandFor(1 - side).Count == 1 && !duel.GameOver,
                "Card Destruction spends its occurrence and replaces only the successfully discarded cards");

            var (empty, emptyQueue) = Setup(side, set, [], [], [], []);
            check(!empty.TryQueueActivation(0, set, out _) && !emptyQueue.IsBusy && empty.Graveyard(side).Count == 0,
                "Card Destruction retains its source with both hands empty after activation");
            var (shortDeck, shortQueue) = Setup(side, set, [Filler], [Filler], [], []);
            check(!shortDeck.TryQueueActivation(0, set, out _) && !shortQueue.IsBusy && shortDeck.Graveyard(side).Count == 0,
                "Card Destruction retains its source when the caster cannot replace its hand");
            var (lethal, lethalQueue) = Setup(side, set, [], [Filler, Filler], [], [Filler]);
            check(new CpuDuelController(side).Step(lethal) is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell } &&
                lethalQueue.IsBusy && !lethal.GameOver, "CPU queues Card Destruction when it can exhaust the opponent's deck");
            Drain(lethalQueue);
            check(lethal.Winner == side && lethal.Graveyard(1 - side).Count == 2 &&
                lethal.HandFor(1 - side).Count == 1 && lethal.DeckCount(1 - side) == 0,
                "Card Destruction finishes the available draws before deciding deck-out");
        }

        Card[] pieces = ExodiaRules.RequiredNames.Select((name, index) =>
            new Card(index, (ushort)index, name, "", 3u << 20)).ToArray();
        Card[] fillers = Enumerable.Repeat(Filler, pieces.Length).ToArray();
        var (exodia, exodiaQueue) = Setup(0, false, fillers, fillers, pieces, pieces);
        check(exodia.TryQueueActivation(0, false, out _), "Card Destruction can replace both full hands");
        Drain(exodiaQueue);
        check(exodia.IsDraw && exodia.Winner is null && exodia.HandFor(0).Count == 5 && exodia.HandFor(1).Count == 5,
            "Card Destruction adjudicates simultaneous Exodia after both replacement batches");
    }
}
