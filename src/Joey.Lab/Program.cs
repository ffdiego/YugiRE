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
            output.WriteLine("Reads ring-buffer and live_duels JSONL. Does not launch Joey, load Frida or evaluate missing game context.");
            return 0;
        }
        if (args[0] == "compare") return RunCompare(args, output, error);
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
