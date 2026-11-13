using System.Security.Cryptography;
using Joey.Core;
using System.Text.Json;

namespace Joey.Lab;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args is ["--help"])
        {
            output.WriteLine("Joey.Lab — offline JSONL analysis (.NET 10)");
            output.WriteLine("Usage: dotnet run --project src/Joey.Lab -- summarize capture.jsonl [--json] [--expected-sha256 HASH]");
            output.WriteLine("       dotnet run --project src/Joey.Lab -- compare LEFT RIGHT [--strict-raw] [--strict-unverified]");
            output.WriteLine("       dotnet run --project src/Joey.Lab -- combat-contexts capture.jsonl [--expected-sha256 HASH]");
            output.WriteLine("       dotnet run --project src/Joey.Lab -- combat-attribute-triage capture.jsonl [--expected-sha256 HASH] [--dat PATH] [--json]");
            output.WriteLine("       dotnet run --project src/Joey.Lab -- experiment --exe PATH --duels N [--workers N] [--python PATH] [--trace-ai]");
            output.WriteLine("Reads ring-buffer and live_duels JSONL. Analysis does not evaluate missing game context; experiment delegates execution to yugilab.");
            return 0;
        }
        if (args[0] == "experiment")
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += handler;
            try { return Experiment.Run(args[1..], output, error, cancellation.Token); }
            finally { Console.CancelKeyPress -= handler; }
        }
        if (args[0] == "compare") return RunCompare(args, output, error);
        if (args[0] == "combat-contexts") return RunCombatContexts(args, output, error);
        if (args[0] == "combat-attribute-triage") return RunAttributeTriage(args, output, error);
        if (args.Length < 2 || args[0] != "summarize") { error.WriteLine("Expected summarize capture.jsonl; use --help"); return 2; }
        bool json = false; string? hash = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--json" && !json) json = true;
            else if (args[i] == "--expected-sha256" && hash is null && i + 1 < args.Length) hash = args[++i];
            else { error.WriteLine($"Unknown/repeated/incomplete option: {args[i]}"); return 2; }
        }
        if (hash is not null && (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))))
        { error.WriteLine("Expected a 64-digit SHA256"); return 2; }
        try
        {
            var report = CaptureAnalyzer.Analyze(CaptureReader.Read(args[1], hash));
            if (json) output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            else
            {
                output.WriteLine($"Capture: {report.Source}\nSHA256: {report.Sha256}");
                output.WriteLine($"Events: {report.EventCount}; kinds: {string.Join(", ", report.CountsByKind.Select(k => $"{k.Key}={k.Value}"))}");
                output.WriteLine($"AI: {report.Selectors.Count(s => s.Function == "select_normal" && s.Paired)} normal selectors ({report.Selectors.Count(s => s.Function == "select_normal" && s.SelectedHandIndex < 0)} negative), {report.Selectors.Count(s => s.Function == "select_priority" && s.Paired)} priority selectors");
                output.WriteLine($"Stats: {report.ComputedStatsCount}; combat outputs: {report.CombatAssessments.Count}; paired stats: {report.PairedCombatStats}");
                output.WriteLine($"Terminal commands: {report.TerminalCommandCount} (not known winners); live completion: {report.LiveCaptureComplete?.ToString() ?? "unknown"}");
                foreach (var c in report.CombatAssessments)
                    output.WriteLine($"  seq={c.Sequence} call={c.CallId} side={c.Side} slots={c.AttackerSlotArgument}/{c.TargetSlotArgument} output={c.OutputHex} score={c.Output.Score} own={c.AttackerStats?.CardIndex.ToString() ?? "?"} rival={c.DefenderStats?.CardIndex.ToString() ?? "?"} {c.Pairing}");
                foreach (var issue in report.Issues) output.WriteLine($"{issue.Severity}: line={issue.Line?.ToString() ?? "-"} {issue.Message}");
                output.WriteLine(report.Scope);
            }
            return report.HasIntegrityErrors ? 2 : 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { error.WriteLine("Invalid/unsupported capture: " + ex.Message); return 2; }
    }


    private static int RunAttributeTriage(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length < 2) { error.WriteLine("Expected combat-attribute-triage capture.jsonl [--expected-sha256 HASH] [--dat PATH] [--json]"); return 2; }
        string? hash = null, dat = null; bool json = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--expected-sha256" && hash is null && i + 1 < args.Length) hash = args[++i];
            else if (args[i] == "--dat" && dat is null && i + 1 < args.Length) dat = args[++i];
            else if (args[i] == "--json" && !json) json = true;
            else { error.WriteLine($"Unknown/repeated/incomplete option: {args[i]}"); return 2; }
        }
        if (hash is not null && (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))))
        { error.WriteLine("Expected a 64-digit capture SHA256"); return 2; }
        try
        {
            IReadOnlyList<Card>? catalog = null;
            if (dat is not null)
            {
                const string pinnedDatSha256 = CpuDeckTraceVerifier.DatSha256;
                using (var source = File.OpenRead(dat))
                {
                    string actual = Convert.ToHexString(SHA256.HashData(source));
                    if (!actual.Equals(pinnedDatSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("DAT SHA256 does not match the pinned Joey catalog");
                }
                using var archive = new Archive(dat);
                catalog = Catalog.Load(archive);
            }
            var report = CombatAttributeTriage.Analyze(CaptureReader.Read(args[1], hash), catalog);
            if (json)
                output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions {
                    WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            else
            {
                output.WriteLine($"Capture SHA256: {report.CaptureSha256}");
                output.WriteLine($"V3 F40 frames: {report.TotalFrames}; neutral local snapshots: {report.NeutralCandidates}");
                output.WriteLine($"Neutral defender visible: {report.NeutralVisibleDefenders}; hidden heuristic: {report.HiddenDefendersInNeutral}");
                output.WriteLine($"Pinned DAT supplied: {report.PinnedCatalogProvided}");
                output.WriteLine($"Conditional Core matches/mismatches: {report.ConditionalMatches}/{report.ConditionalMismatches}; independently validated contexts: 0");
                foreach (var row in report.Cases.Where(c => c.NeutralLocalSnapshot))
                    output.WriteLine($"  seq={row.EnterSequence} call={row.CombatCallId} indices={row.AttackerIndex}/{row.DefenderIndex} " +
                        $"attacker={row.Attacker.Status} defender={row.Defender.Status}");
                output.WriteLine(report.Scope);
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or OverflowException or FormatException)
        { error.WriteLine("Invalid/unsupported capture or catalog: " + ex.Message); return 2; }
    }

    private static int RunCombatContexts(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 2 && !(args.Length == 4 && args[2] == "--expected-sha256" && args[3].Length == 64 && args[3].All(Uri.IsHexDigit)))
        { error.WriteLine("Expected combat-contexts capture.jsonl [--expected-sha256 HASH]"); return 2; }
        try
        {
            var report = CombatContextReader.Read(CaptureReader.Read(args[1], args.Length == 4 ? args[3] : null));
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return report.Issues.Count == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException or FormatException)
        { error.WriteLine("Invalid/unsupported capture: " + ex.Message); return 2; }
    }

    private static int RunCompare(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length < 3 || args.Skip(3).Any(x => x != "--strict-raw" && x != "--strict-unverified"))
        { error.WriteLine("Expected compare LEFT RIGHT [--strict-raw] [--strict-unverified]"); return 2; }
        try
        {
            var report = RunComparison.Compare(args[1], args[2]);
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return !report.ObservedMatch || (args.Contains("--strict-raw") && !report.RawCommandArgsMatch) ||
                (args.Contains("--strict-unverified") && report.UnverifiedRawDifferences > 0) ? 1 : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException or FormatException)
        { error.WriteLine("Invalid/unsupported capture: " + ex.Message); return 2; }
    }
}
