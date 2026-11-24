using Joey.Core;

namespace Joey.Lab;

/// <summary>Headless laboratory frontend; fixture or local DAT catalogue.</summary>
public static class DuelConsoleRunner
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        bool fixture = args.Length is 1 or 2 && args[0] == "--fixture";
        bool dat = args.Length is 2 or 3 && args[0] == "--dat";
        if (!fixture && !dat)
        {
            error.WriteLine("Usage: duel-console --fixture [script.txt] | --dat data.dat [script.txt]");
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
                duel = DemoDuel.FromPartialObservation(0, [], new Card?[5], new Card?[5], 8000);
            }
            int scriptIndex = fixture ? 1 : 2;
            if (args.Length == scriptIndex) return RunLines(duel, catalog, input, output, error);
            using var script = File.OpenText(args[scriptIndex]);
            return RunLines(duel, catalog, script, output, error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { error.WriteLine(ex.Message); return 2; }
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
        return failed || session.Queue.IsBusy ? 1 : 0;
    }
}
