using System.Text.Json;
using Joey.Core;
using Joey.Lab;

internal static class DuelConsoleTraceChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Open { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Open;
    }

    public static void Run(Action<bool, string> check)
    {
        string CaptureAfterDrain(Gate? gate)
        {
            var session = new DuelConsoleSession(DuelConsoleRunner.Fixture(), gate);
            check(session.Execute("draw 1 0").Enqueued, "A05 console admits draw");
            session.Queue.Tick();
            if (gate is not null)
            {
                int count = session.Queue.Trace.Count;
                var query = session.Execute("trace");
                using var snapshot = JsonDocument.Parse(query.Message);
                check(query.Success && !query.Enqueued && !snapshot.RootElement.GetProperty("complete").GetBoolean() &&
                    snapshot.RootElement.GetProperty("rows").GetArrayLength() == 1 && session.Queue.Trace.Count == count,
                    "A05 read-only trace includes executed draw while presentation remains active");
                gate.Open = true;
                session.Queue.Tick();
            }
            check(session.Execute("summon 0 3").Enqueued, "A05 console admits summon");
            for (int i = 0; i < 8 && session.Queue.IsBusy; i++)
                session.Queue.Tick();
            return session.Execute("trace").Message;
        }

        string immediate = CaptureAfterDrain(null);
        check(immediate == CaptureAfterDrain(new Gate()), "A05 console trace independent of presentation gate");
        using (var snapshot = JsonDocument.Parse(immediate))
        {
            var doc = snapshot.RootElement;
            var rows = doc.GetProperty("rows");
            check(doc.GetProperty("schema").GetString() == "yugire-action-projection-v1" &&
                doc.GetProperty("complete").GetBoolean() &&
                rows.EnumerateArray().Select(r => r.GetProperty("command").GetString()).SequenceEqual(
                    ["DrawCommand", "PrepareNormalPlacementCommand", "SummonCommand", "FinishNormalPlacementCommand"]) &&
                rows[1].GetProperty("mapping").GetString() == "unmapped" &&
                rows[2].GetProperty("args").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual([0xCE, 24, 0x300, 1]),
                "A05 console trace contains observed placement in the established schema");
        }

        string directory = Directory.CreateTempSubdirectory("yugire-trace-").FullName;
        try
        {
            string script = Path.Combine(directory, "script.txt");
            string trace = Path.Combine(directory, "trace.json");
            using var output = new StringWriter();
            using var error = new StringWriter();
            int Run(params string[] args) => DuelConsoleRunner.Run(args, TextReader.Null, output, error);
            File.WriteAllText(script, "draw 1 0\nsummon 0 3\ndamage 200 1\n");
            check(Run("--fixture", script, $"--trace-output={trace}") == 0, "A05 Lab exports script trace");
            string exported = File.ReadAllText(trace);
            using (var snapshot = JsonDocument.Parse(exported))
                check(snapshot.RootElement.GetProperty("complete").GetBoolean() &&
                    snapshot.RootElement.GetProperty("rows").GetArrayLength() == 5,
                    "A05 successful script exports all executed rows");
            check(Run("--fixture", script, $"--trace-output={trace}") == 2 && File.ReadAllText(trace) == exported,
                "A05 trace export preserves existing files");

            File.WriteAllText(script, "draw 1 0\ninvalid-command\ndamage 200 1\n");
            string partial = Path.Combine(directory, "partial.json");
            check(Run("--fixture", script, $"--trace-output={partial}") == 1, "A05 Lab rejects invalid script");
            using (var snapshot = JsonDocument.Parse(File.ReadAllText(partial)))
                check(!snapshot.RootElement.GetProperty("complete").GetBoolean() &&
                    snapshot.RootElement.GetProperty("rows").GetArrayLength() == 1,
                    "A05 failed script exports its incomplete executed prefix");

            string unused = Path.Combine(directory, "unused.json");
            check(Run("--fixture", $"--trace-output={unused}") == 2 &&
                Run("--fixture", script, $"--trace-output={unused}", $"--trace-output={unused}") == 2 &&
                Run("--fixture", script, "--trace-output= ") == 2 && !File.Exists(unused),
                "A05 output path must be unique and nonempty and requires a script");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
