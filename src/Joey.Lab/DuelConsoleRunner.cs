using Joey.Core;

namespace Joey.Lab;

/// <summary>First headless frontend. Synthetic fixture only; no hidden game writes.</summary>
public static class DuelConsoleRunner
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args.Length is < 1 or > 2 || args[0] != "--fixture")
        {
            error.WriteLine("Usage: duel-console --fixture [script.txt]");
            return 2;
        }
        if (args.Length == 1) return RunLines(input, output, error);
        try
        {
            using var script = File.OpenText(args[1]);
            return RunLines(script, output, error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { error.WriteLine(ex.Message); return 2; }
    }

    public static DemoDuel Fixture()
    {
        var monster = new Card(24, 24, "Console fixture monster", "", (3u << 20) | (4u << 25));
        Card?[] slots = [monster, null, null, null, null];
        return DemoDuel.FromPartialObservation(0, [], slots, slots, 8000,
            side0Deck: Enumerable.Repeat(monster, 10).ToArray(),
            side1Deck: Enumerable.Repeat(monster, 10).ToArray());
    }

    private static int RunLines(TextReader input, TextWriter output, TextWriter error)
    {
        var session = new DuelConsoleSession(Fixture());
        output.WriteLine("YugiRE laboratory console (synthetic fixture, zero-based sides/slots).");
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
        return failed || session.Queue.IsBusy ? 1 : 0;
    }
}
