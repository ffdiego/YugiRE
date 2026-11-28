using Joey.Core;

internal static class ActionQueueRemovalChecks
{
    private static Card Monster(ushort id) => new(id, id, $"Monster {id}", "", (3u << 20) | (4u << 25) | (100u << 9));
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0, 5).Select(i => i < cards.Length ? cards[i] : null).ToArray();
    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new Exception("Removal pilot failed to drain.");
    }

    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not DestroyCommand || Release;
    }

    public static void Run(Action<bool, string> check)
    {
        var normal = Monster(24);
        var sangan = Monster(47);
        var raigeki = new Card(336, 336, "Raigeki", "", 22u << 20);
        foreach (int caster in new[] { 0, 1 })
        {
            int opponent = 1 - caster;
            DemoDuel State() => DemoDuel.FromPartialObservation(caster, [raigeki, raigeki],
                caster == 0 ? Slots(normal) : Slots(normal, sangan, normal),
                caster == 1 ? Slots(normal) : Slots(normal, sangan, normal), 8000,
                side0Deck: [normal], side1Deck: [normal]);
            (DemoDuel Duel, ActionQueue Queue, List<DemoMonsterDestruction> Events) RunPilot(bool delayed)
            {
                var duel = State();
                duel.MonsterFaceDown[opponent, 2] = true;
                List<DemoMonsterDestruction> events = [];
                duel.MonsterDestroyed += events.Add;
                var gate = new Gate();
                var queue = duel.CreatePrimitiveActionQueue(delayed ? gate : null);
                bool admitted = duel.TryQueueRaigekiFromHand(1, out _);
                check(admitted && duel.Hand.Count == 2 && duel.Graveyard(caster).Count == 0 &&
                    duel.Monsters[opponent, 0] is not null && !duel.Play(0, out _) &&
                    !duel.TryQueueRaigekiFromHand(0, out _),
                    $"A02 Raigeki P{caster}: admission is nonmutating and legacy input blocked");
                bool capturedFirst = duel.TryCaptureCardTarget(caster, DuelZone.Hand, 0, out var first);
                bool capturedSecond = duel.TryCaptureCardTarget(caster, DuelZone.Hand, 1, out var second);
                bool capturedLeft = duel.TryCaptureCardTarget(opponent, DuelZone.Monster, 0, out var left);
                bool capturedRight = duel.TryCaptureCardTarget(opponent, DuelZone.Monster, 2, out var right);
                check(capturedFirst && capturedSecond && first != second && capturedLeft && capturedRight && left != right,
                    $"A02 P{caster}: repeated catalogue copies have independent occurrence IDs");
                queue.Tick(); // activation only
                check(duel.Hand.Count == 2 && events.Count == 0 && queue.PendingCount == 4,
                    $"A02 P{caster}: activation emits source movement plus three destroys");
                queue.Tick(); // spent spell movement
                check(duel.Hand.Count == 1 && duel.Graveyard(caster).Single() == raigeki &&
                    duel.TryCaptureCardTarget(caster, DuelZone.Hand, 0, out var survivor) && survivor == first,
                    $"A02 P{caster}: only selected Raigeki is spent");
                if (delayed)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        gate.Release = false;
                        queue.Tick(); queue.Tick(); queue.Tick();
                        check(events.Count == i + 1 && duel.PendingCardSelection is null &&
                            duel.Graveyard(opponent).Count == i + 1,
                            $"A02 P{caster}: destruction {i + 1} applied once, no premature choice");
                        gate.Release = true;
                        queue.Tick();
                    }
                    check(duel.PendingCardSelection is null && queue.IsBusy,
                        $"A02 P{caster}: post-removal continuation is a separate passage");
                }
                Drain(queue);
                check(duel.Monsters[caster, 0] == normal && events.Count == 3 &&
                    Enumerable.Range(0, 5).All(i => duel.Monsters[opponent, i] is null) &&
                    duel.Graveyard(opponent).SequenceEqual([normal, sangan, normal]) &&
                    duel.LastMonsterWipe is { TotalDestroyed: 3, DestroyedFaceDown: 1 } &&
                    duel.PendingCardSelection is { EffectResourceId: 47 } choice && choice.Owner == opponent,
                    $"A02 P{caster}: real wipe preserves own field, then exposes Sangan search");
                check(duel.ResolveCardSelection(duel.PendingCardSelection!.EligibleCards.Single().SourceIndex, out _) &&
                    duel.HandFor(opponent).Single() == normal,
                    $"A02 P{caster}: legacy choice bridge still resolves");
                return (duel, queue, events);
            }
            var immediate = RunPilot(false);
            var delayed = RunPilot(true);
            check(immediate.Queue.Trace.SequenceEqual(delayed.Queue.Trace) &&
                immediate.Events.SequenceEqual(delayed.Events) &&
                immediate.Duel.Graveyard(opponent).SequenceEqual(delayed.Duel.Graveyard(opponent)),
                $"A02 P{caster}: equal trace, events and graveyard with immediate/delayed gates");
        }

        // Source identities survive real hand-to-field movement; catalogue
        // equality is unchanged and identical copies cannot alias in the duel.
        var movement = DemoDuel.FromPartialObservation(0, [normal, normal], Slots(), Slots(), 8000);
        check(movement.TryCaptureCardTarget(0, DuelZone.Hand, 1, out var before) &&
            TurnFixture.Place(movement, 1, 0, false, out _) &&
            movement.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var after) &&
            before.Occurrence == after.Occurrence &&
            movement.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var remaining) &&
            remaining.Occurrence != before.Occurrence,
            "A02 occurrence survives hand to field; identical copy keeps separate ID");

        var stale = DemoDuel.FromPartialObservation(0, [], Slots(), Slots(normal, normal), 8000);
        var staleQueue = stale.CreatePrimitiveActionQueue();
        bool capturedDoomed = stale.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var doomed);
        bool capturedReplacement = stale.TryCaptureCardTarget(1, DuelZone.Monster, 1, out var replacement);
        check(capturedDoomed && capturedReplacement, "A02 stale fixture identities");
        staleQueue.Enqueue(new DestroyCommand(1, doomed));
        staleQueue.Enqueue(new DestroyCommand(1, doomed)); // duplicate must skip
        staleQueue.Tick();
        // Fixture-only move of another registered occurrence into vacated slot.
        stale.Monsters[1, 0] = stale.Monsters[1, 1];
        stale.Monsters[1, 1] = null;
        staleQueue.Tick();
        Drain(staleQueue);
        check(stale.Monsters[1, 0] == normal && stale.Graveyard(1).Count == 1 &&
            stale.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var survivorTarget) &&
            survivorTarget == replacement &&
            staleQueue.Trace.Any(t => t.Stage == ActionTraceStage.Executed &&
                t.Result?.Outcome == CommandOutcome.Skipped),
            "A02 duplicate destruction skips stale target, preserves replacement in same slot");

        var aliased = DemoDuel.FromPartialObservation(0, [], Slots(normal), Slots(), 8000);
        aliased.Monsters[0, 1] = aliased.Monsters[0, 0];
        check(!aliased.TryCaptureCardTarget(0, DuelZone.Monster, 0, out _) &&
            !aliased.TryCaptureCardTarget(0, DuelZone.Monster, 1, out _),
            "A02 direct fixture alias fails closed");

        var discard = DemoDuel.FromPartialObservation(0, [normal, normal], Slots(), Slots(), 8000);
        var discardQueue = discard.CreatePrimitiveActionQueue();
        discard.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var discardFirst);
        discard.TryCaptureCardTarget(0, DuelZone.Hand, 1, out var sendSecond);
        int discards = 0;
        discard.HandTransferred += x => { if (!x.FromDeck) discards++; };
        discardQueue.Enqueue(new DiscardCommand(0, discardFirst));
        discardQueue.Enqueue(new SendToGraveyardCommand(0, sendSecond));
        Drain(discardQueue);
        check(discard.Hand.Count == 0 && discard.Graveyard(0).Count == 2 && discards == 1,
            "A02 hand compaction preserves target IDs; discard distinct from ordinary send");

        ActionQueuePlacementChecks.Run(check);

        ActionQueueLinkedChecks.Run(check);
    }
}
