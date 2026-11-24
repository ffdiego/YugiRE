using Joey.Core;
using Joey.Lab;

internal static class DuelConsoleChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }
    public static void Run(Action<bool,string> check)
    {
        string[] script = ["draw 2", "destroy 1 0", "damage 300 1", "heal 100 0"];
        (string State, ActionTraceEntry[] Trace) Pilot(bool delayed)
        {
            var d = DuelConsoleRunner.Fixture();
            var gate = new Gate();
            var session = new DuelConsoleSession(d, delayed ? gate : null);
            check(session.Execute("state").Success && session.Execute("diff").Message == "No changes.",
                "A04 state/diff are read-only");
            foreach (string bad in new[] { "draw 0", "draw 81", "draw 2 2", "damage -1", "destroy 1 4", "spawn 24" })
                check(!session.Execute(bad).Success && session.Queue.Trace.Count == 0, "A04 rejects invalid " + bad);
            foreach (string line in script)
            {
                check(session.Execute(line).Enqueued, "A04 admit " + line);
                if (delayed)
                {
                    gate.Release = false;
                    session.Queue.Tick();
                    check(!session.Execute("heal 1").Success, "A04 busy console admission rejected");
                    session.Queue.Tick();
                    gate.Release = true;
                }
                for (int i = 0; i < 100 && session.Queue.IsBusy; i++) session.Queue.Tick();
                check(!session.Queue.IsBusy, "A04 command batch drains");
            }
            check(d.Hand.Count == 2 && d.Monsters[1,0] is null && d.Graveyard(1).Count == 1 &&
                d.Life.SequenceEqual([8100,7700]) &&
                session.Queue.Trace.Where(t => t.Stage == ActionTraceStage.Admitted &&
                    t.Entry.Command is DrawCommand or DestroyCommand or DamageCommand or HealCommand)
                    .All(t => t.Entry.Origin == ActionOrigin.Console),
                "A04 console uses actual executor and console origin");
            check(session.Execute("diff").Message.Contains("lp=7700"), "A04 diff reports changed state");
            return (session.Execute("state").Message, session.Queue.Trace.ToArray());
        }
        var a = Pilot(false); var b = Pilot(true);
        check(a.State == b.State && a.Trace.SequenceEqual(b.Trace), "A04 same script immediate/gated trace and state");
        DuelConsoleLabEditChecks.Run(check);
        using var input = new StringReader("state\ndraw 2\ndestroy 1 0\ndamage 300 1\nheal 100 0\ndiff\n");
        using var output = new StringWriter(); using var error = new StringWriter();
        check(DuelConsoleRunner.Run(["--fixture"], input, output, error) == 0 &&
            output.ToString().Contains("lp=7700") && error.ToString().Length == 0,
            "A04 stdin runner shares the parser and executor");
    }
}
