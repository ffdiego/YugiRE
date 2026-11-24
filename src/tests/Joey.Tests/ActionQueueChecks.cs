using Joey.Core;

internal static class ActionQueueChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance { get; set; } = true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    // Deliberately isolated queue test, not a second implementation of card rules.
    // The production adapter is exercised separately on DemoDuel below.
    private sealed class RecordingExecutor : IDuelCommandExecutor
    {
        public List<DuelCommand> Executed { get; } = [];
        public HashSet<CardTarget> Present { get; } = [];
        public List<CardTarget> Removed { get; } = [];
        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            Executed.Add(command);
            if (command is DestroyCommand destroy)
            {
                if (!Present.Remove(destroy.Target)) return new(CommandOutcome.Skipped, "Occurrence gone.");
                Removed.Add(destroy.Target);
            }
            return new(CommandOutcome.Applied);
        }
    }

    private sealed class ThrowingExecutor : IDuelCommandExecutor
    {
        public int Calls { get; private set; }
        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            Calls++;
            throw new InvalidOperationException("Simulated partially applied command.");
        }
    }

    private sealed class FollowupExecutor : IDuelCommandExecutor
    {
        public int Calls { get; private set; }
        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            Calls++;
            if (command is DrawCommand) followups.Enqueue(new DamageCommand(1, 100));
            return new(CommandOutcome.Applied);
        }
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new Exception("Queue failed to drain within bounded test.");
    }

    public static void Run(Action<bool, string> check)
    {
        var a = new CardTarget(new(1), 0, DuelZone.Monster);
        var b = a with { Occurrence = new(2) };
        var c = a with { Occurrence = new(3) };
        var replacement = a with { Occurrence = new(4) };
        var executor = new RecordingExecutor();
        executor.Present.UnionWith([a, b, c]);
        var gate = new Gate { CanAdvance = false };
        var queue = new ActionQueue(executor, gate);
        DuelCommand[] wipe = [new DestroyCommand(0, a), new DestroyCommand(0, b), new DestroyCommand(0, c)];
        queue.EnqueueBatch(wipe);
        check(executor.Removed.Count == 0 && queue.PendingCount == 3 && queue.IsBusy,
            "A02 admission does not mutate state");
        check(queue.Tick() == ActionTickResult.Blocked && queue.Active is null && executor.Executed.Count == 0,
            "A02 global gate prevents start");
        gate.CanAdvance = true;
        check(queue.Tick() == ActionTickResult.Blocked && queue.Active?.Command == wipe[0] &&
            queue.PendingCount == 2 && executor.Removed.SequenceEqual([a]), "A02 first destruction mutates once");
        queue.Tick(); queue.Tick();
        check(executor.Executed.Count == 1 && queue.PendingCount == 2,
            "A02 completion gate stalls without repeated mutation");
        gate.Release = true;
        check(queue.Tick() == ActionTickResult.Completed && executor.Executed.Count == 1 && queue.Active is null,
            "A02 releasing active command does not dequeue another");
        executor.Present.Remove(b); executor.Present.Add(replacement);
        check(queue.Tick() == ActionTickResult.Completed && executor.Removed.SequenceEqual([a]) &&
            queue.Trace.Last().Result?.Outcome == CommandOutcome.Skipped && executor.Present.Contains(replacement),
            "A02 stale occurrence skipped without touching replacement");
        gate.Release = false;
        queue.Tick();
        check(queue.PendingCount == 0 && queue.IsBusy && queue.Active?.Command == wipe[2] &&
            executor.Removed.SequenceEqual([a, c]), "A02 empty FIFO is still busy while last command waits");
        gate.Release = true;
        check(queue.Tick() == ActionTickResult.Completed && !queue.IsBusy && executor.Executed.SequenceEqual(wipe),
            "A02 exactly one FIFO command per target");

        var clean = new RecordingExecutor();
        clean.Present.UnionWith([a, b, c]);
        queue = new ActionQueue(clean);
        queue.EnqueueBatch(wipe);
        Drain(queue);
        check(clean.Removed.SequenceEqual([a, b, c]) &&
            queue.Trace.Count(x => x.Stage == ActionTraceStage.Executed) == 3,
            "A02 three live targets produce three separate mutations");

        try
        {
            queue.EnqueueBatch([new DrawCommand(0), new DamageCommand(1, 0)]);
            check(false, "A02 invalid batch rejected");
        }
        catch (ArgumentException)
        {
            check(queue.PendingCount == 0 && queue.Trace.Count == 12, "A02 invalid batch is atomic");
        }

        var followup = new FollowupExecutor();
        queue = new ActionQueue(followup);
        queue.Enqueue(new DrawCommand(0));
        queue.Tick();
        check(followup.Calls == 1 && queue.PendingCount == 1 && queue.Active is null,
            "A02 executor can append but not recursively execute");
        Drain(queue);
        check(followup.Calls == 2, "A02 appended command executes next passage");

        var source = new Card(1, 1, "Decision fixture", "", 1u << 20);
        var request = new PendingEffectDecision(1, 0, source, EffectDecisionKind.DrawOneOnFlip, 0, 1);
        var decisionExecutor = new RecordingExecutor();
        gate = new Gate();
        queue = new ActionQueue(decisionExecutor, gate);
        queue.Enqueue(new DrawCommand(0));
        queue.ScheduleContinuation(_ => new([], answer =>
            new(answer == EffectDecisionAnswer.Accept ? [new DrawCommand(0)] : []), request));
        queue.Tick(); queue.Tick();
        check(queue.PendingDecision is null, "A02 decision waits for preceding visual gate");
        gate.Release = true; queue.Tick();
        queue.Tick();
        check(queue.PendingDecision == request && queue.IsBusy &&
            queue.Tick() == ActionTickResult.WaitingForDecision, "A02 pending decision blocks advancement");
        check(!queue.SubmitDecision(2, EffectDecisionAnswer.Accept) &&
            queue.SubmitDecision(1, EffectDecisionAnswer.Accept) &&
            !queue.SubmitDecision(1, EffectDecisionAnswer.Decline) && decisionExecutor.Executed.Count == 1,
            "A02 wrong/repeated answer rejected; valid answer does not execute");
        queue.Tick();
        check(decisionExecutor.Executed.Count == 1 && queue.PendingCount == 1,
            "A02 continuation emits work on separate tick");
        Drain(queue);
        check(decisionExecutor.Executed.SequenceEqual([new DrawCommand(0), new DrawCommand(0)]),
            "A02 accepted choice resumes through FIFO");

        var throwing = new ThrowingExecutor();
        queue = new ActionQueue(throwing);
        queue.Enqueue(new DrawCommand(0));
        try { queue.Tick(); } catch (InvalidOperationException) { }
        try { queue.Tick(); } catch (InvalidOperationException) { }
        check(queue.Fault is not null && queue.IsBusy && throwing.Calls == 1,
            "A02 faults instead of retrying possibly applied command");

        // Same admitted operations and initial state, different gate and origin.
        // Use actual DemoDuel helpers, including the observable draw notifications.
        (DemoDuel Duel, ActionQueue Queue, List<DemoHandTransfer> Draws) RunPilot(bool delayed)
        {
            Card?[] empty = [null, null, null, null, null];
            var duel = DemoDuel.FromPartialObservation(0, [], empty, empty, 8000,
                side0Deck: [source, source], side1Deck: [source]);
            List<DemoHandTransfer> draws = [];
            duel.HandTransferred += draws.Add;
            var manual = new Gate();
            var q = duel.CreatePrimitiveActionQueue(delayed ? manual : null);
            q.EnqueueBatch([new DrawCommand(0), new DrawCommand(1), new DamageCommand(1, 300),
                new HealCommand(0, 500)], delayed ? ActionOrigin.Console : ActionOrigin.Effect);
            check(duel.HandFor(0).Count == 0 && duel.Life.SequenceEqual([8000, 8000]),
                "A02 pilot admission leaves real duel unchanged");
            if (delayed)
            {
                for (int i = 0; i < 4; i++)
                {
                    manual.Release = false;
                    q.Tick(); q.Tick(); q.Tick();
                    manual.Release = true;
                    q.Tick();
                }
            }
            else Drain(q);
            return (duel, q, draws);
        }
        var fast = RunPilot(false);
        var slow = RunPilot(true);
        check(!fast.Queue.IsBusy && !slow.Queue.IsBusy &&
            fast.Duel.Life.SequenceEqual([8500, 7700]) &&
            fast.Duel.Life.SequenceEqual(slow.Duel.Life) &&
            fast.Duel.HandFor(0).SequenceEqual(slow.Duel.HandFor(0)) &&
            fast.Duel.HandFor(1).SequenceEqual(slow.Duel.HandFor(1)) &&
            fast.Duel.DeckCount(0) == 1 && fast.Duel.DeckCount(1) == 0 &&
            slow.Duel.DeckCount(0) == 1 && slow.Duel.DeckCount(1) == 0 &&
            fast.Draws.Count == 2 && fast.Draws.SequenceEqual(slow.Draws) &&
            fast.Queue.Trace.Select(x => (x.Entry.Sequence, x.Entry.Command, x.Stage, x.Result))
                .SequenceEqual(slow.Queue.Trace.Select(x => (x.Entry.Sequence, x.Entry.Command, x.Stage, x.Result))),
            "A02 real draw/LP pilot has identical logical trace and state with delayed or immediate gate");
        fast.Queue.Enqueue(new SetCommand(0, new(new(1), 0, DuelZone.Hand), DuelZone.Monster, 0));
        Drain(fast.Queue);
        check(fast.Queue.Trace.Last().Result?.Outcome == CommandOutcome.Rejected,
            "A02 non-migrated DemoDuel command explicitly rejects");
        ActionQueueRemovalChecks.Run(check);
    }
}
