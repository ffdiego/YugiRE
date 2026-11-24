using Joey.Core;

namespace Joey.Lab;

/// <summary>Headless laboratory frontend; fixture or local DAT catalogue.</summary>
public static class DuelConsoleRunner
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        const string traceOption = "--trace-output=";
        var traceOptions = args.Where(a => a.StartsWith(traceOption, StringComparison.Ordinal)).ToArray();
        string? tracePath = traceOptions.Length == 1 ? traceOptions[0][traceOption.Length..] : null;
        if (traceOptions.Length > 0 && string.IsNullOrWhiteSpace(tracePath))
        {
            error.WriteLine("Expected one nonempty --trace-output=path.");
            return 2;
        }
        args = args.Where(a => !a.StartsWith(traceOption, StringComparison.Ordinal)).ToArray();
        bool fixture = args.Length is 1 or 2 && args[0] == "--fixture";
        bool dat = args.Length is 2 or 3 && args[0] == "--dat";
        if (!fixture && !dat)
        {
            error.WriteLine("Usage: duel-console (--fixture | --dat data.dat) [script.txt] [--trace-output=path]");
            return 2;
        }
        int scriptIndex = fixture ? 1 : 2;
        if (tracePath is not null && args.Length == scriptIndex)
        {
            error.WriteLine("--trace-output requires a script file.");
            return 2;
        }
        try
        {
            DemoDuel duel;
            IReadOnlyList<Card> catalog;
            if (fixture) { duel = Fixture(); catalog = [FixtureCard()]; }
            else
            {
                using var archive = new Archive(args[1]);
                catalog = Catalog.Load(archive).ToArray();
                // Start from an empty ongoing duel, not invented catalogue cards.
                duel = DuelConsoleSession.EmptyLabDuel();
            }
            if (args.Length == scriptIndex)
                return RunLines(duel, catalog, input, output, error);
            using var script = File.OpenText(args[scriptIndex]);
            var session = new DuelConsoleSession(duel, cards: catalog);
            // Reserve new output before executing; never overwrite existing data.
            using var traceOutput = tracePath is null ? null :
                new StreamWriter(new FileStream(tracePath, FileMode.CreateNew, FileAccess.Write, FileShare.None));
            int result = RunScript(session, script, output, error);
            if (traceOutput is not null)
            {
                var document = ActionTraceJson.Capture(session.Queue, "laboratory-script");
                traceOutput.WriteLine(ActionTraceJson.Serialize(document with { Complete = document.Complete && result == 0 }));
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { error.WriteLine(ex.Message); return 2; }
    }

    private static int RunScript(DuelConsoleSession session, TextReader input, TextWriter output, TextWriter error)
    {
        IEnumerable<string> Lines()
        {
            while(input.ReadLine() is string line) yield return line;
        }
        var script=new DuelConsoleScript(session,Lines());
        int traceCursor=0;
        for(int passage=0;passage<Math.Max(512L,512L*(script.Count+1));passage++)
        {
            var step=script.Advance();
            if(step.Status is DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision)
            { error.WriteLine($"Line {step.Line}: {step.Message}"); return 1; }
            if(step.Message.Length>0) output.WriteLine(step.Message);
            if(step.Status==DuelScriptStatus.Completed) return 0;
            if(session.Queue.IsBusy)
            {
                try { session.Queue.Tick(); }
                catch(InvalidOperationException ex) { error.WriteLine(ex.Message); return 1; }
                var trace=session.Queue.Trace;
                for(int i=traceCursor;i<trace.Count;i++)
                    if(trace[i].Stage==ActionTraceStage.Executed)
                        output.WriteLine($"{trace[i].Entry.Sequence}: {trace[i].Entry.Command.GetType().Name} {trace[i].Result?.Outcome} {trace[i].Result?.Detail}");
                traceCursor=trace.Count;
            }
        }
        error.WriteLine("Script passage limit exceeded."); return 1;
    }

    public static Card FixtureCard() => new(24, 24, "Console fixture monster", "", (3u << 20) | (4u << 25));

    public static DemoDuel Fixture()
    {
        var monster = FixtureCard();
        Card?[] slots = [monster, null, null, null, null];
        return DemoDuel.FromPartialObservation(0, [], slots, slots, 8000,
            side0Deck: Enumerable.Repeat(monster, 10).ToArray(),
            side1Deck: Enumerable.Repeat(monster, 10).ToArray());
    }

    private static int RunLines(DemoDuel duel, IReadOnlyList<Card> catalog, TextReader input, TextWriter output, TextWriter error)
    {
        var session = new DuelConsoleSession(duel, cards: catalog);
        output.WriteLine("YugiRE laboratory console (zero-based sides/slots; spawn/move are setup edits).");
        output.WriteLine(DuelConsoleSession.Help);
        bool failed = false;
        while (input.ReadLine() is string line)
        {
            if (line.Trim() is "quit" or "exit") break;
            var result = session.Execute(line);
            if (result.Message.Length > 0) (result.Success ? output : error).WriteLine(result.Message);
            failed |= !result.Success;
            if (!result.Success) continue;
            int start = session.Queue.Trace.Count;
            try
            {
                for (int i = 0; i < 512 && session.Queue.IsBusy; i++)
                {
                    var tick = session.Queue.Tick();
                    if (tick is ActionTickResult.WaitingForDecision or ActionTickResult.Blocked) break;
                }
                foreach (var entry in session.Queue.Trace.Skip(start).Where(t => t.Stage == ActionTraceStage.Executed))
                {
                    output.WriteLine($"{entry.Entry.Sequence}: {entry.Entry.Command.GetType().Name} {entry.Result?.Outcome} {entry.Result?.Detail}");
                    failed |= entry.Result?.Outcome == CommandOutcome.Rejected;
                }
                if (session.Queue.PendingDecision is { } choice)
                    output.WriteLine($"Decision {choice.Id}: {choice.Source.Name} accept|decline");
                else if (session.Queue.IsBusy)
                { error.WriteLine("Queue did not drain."); return 1; }
            }
            catch (InvalidOperationException ex)
            { error.WriteLine("Queue fault: " + ex.Message); return 1; }
        }
        return failed || session.Queue.IsBusy || session.HasPendingChoice ? 1 : 0;
    }
}
