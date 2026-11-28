using Joey.Core;

internal static class DuelConsoleTargetedChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    private static Card Spell(ushort id, int cardClass = 22) =>
        new(id, id, "Targeted console fixture", "", (uint)cardClass << 20);

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 32 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Targeted console fixture did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        {
            var targetZone = fromSet ? DuelZone.SpellTrap : DuelZone.FieldSpell;
            var targetCard = fromSet ? Spell(1056, 21) : Spell(332);
            var duel = DemoDuel.FromPartialObservation(side, [Spell(1079), targetCard], new Card?[5], new Card?[5], 8000);
            check(duel.SetFromHand(1, out _), "A06 targeted console registers a target occurrence");
            var gate = new Gate();
            var session = new DuelConsoleSession(duel, gate);
            string command = fromSet ? $"activate set 1 {side} spell 0" : $"activate hand 0 {side} field";
            if (fromSet)
            {
                check(duel.SetFromHand(0, out _) && !session.Execute(command).Success && !session.Queue.IsBusy,
                    "A06 targeted console rejects activating a Quick-Play spell in the turn it was Set");
                gate.Release = true;
                TurnFixture.Advance(duel);
                TurnFixture.Advance(duel);
                gate.Release = false;
            }
            int baseline = session.Queue.Trace.Count;

            check(duel.TryCaptureCardTarget(side, targetZone, 0, out var expected) &&
                !session.Execute($"activate hand 0 {side} spell 5").Success &&
                !session.Execute($"activate hand 0 {side} field 0").Success && session.Queue.Trace.Count == baseline,
                "A06 invalid target syntax cannot admit work");
            check(session.Execute(command).Enqueued && duel.Graveyard(side).Count == 0 &&
                duel.TryCaptureCardTarget(side, targetZone, 0, out var stillPresent) && stillPresent == expected,
                "A06 targeted activation captures its occurrence without mutation");
            session.Queue.Tick();
            check(session.Queue.Active?.Origin == ActionOrigin.Console && duel.Graveyard(side).Count == 0 &&
                session.Queue.IsBusy && !session.Execute(command).Success,
                "A06 the shared presentation gate holds the targeted console activation before source spend");
            gate.Release = true;
            Drain(session.Queue);
            check(duel.Graveyard(side).Select(card => card.Id).SequenceEqual(new ushort[] { 1079, targetCard.Id }) &&
                session.Queue.Trace.Any(entry => entry is
                    { Stage: ActionTraceStage.Executed, Entry.Command: DestroyCommand destroy } && destroy.Target == expected),
                "A06 targeted console resolves the exact occurrence after spending the spell");
        }

        var original = Spell(332);
        var stale = DemoDuel.FromPartialObservation(0, [Spell(1079), original], new Card?[5], new Card?[5], 8000);
        check(stale.SetFromHand(1, out _), "A06 stale-target setup");
        var console = new DuelConsoleSession(stale);
        check(console.Execute("activate hand 0 0 field").Enqueued, "A06 targeted command admitted before replacement");
        var replacement = original with { };
        stale.FieldSpells[0] = replacement;
        Drain(console.Queue);
        check(ReferenceEquals(stale.FieldSpells[0], replacement) && stale.Graveyard(0).Count == 0 &&
            stale.Hand.Count == 1 && console.Queue.Trace.Last().Result?.Outcome == CommandOutcome.Skipped,
            "A06 a same-resource replacement cannot inherit a queued target");
    }
}
