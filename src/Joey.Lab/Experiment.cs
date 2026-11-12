using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Joey.Lab;

public static class Experiment
{
    public const string JoeyHash = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken cancellation = default, string expectedExeHash = JoeyHash)
    {
        try { return RunCore(args, output, cancellation, expectedExeHash); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
        { error.WriteLine("Experiment error: " + ex.Message); return 2; }
    }

    private static int RunCore(string[] args, TextWriter output, CancellationToken cancellation, string expectedExeHash)
    {
        var options = new Dictionary<string, string>(); bool trace = false;
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key == "--trace-ai" && !trace) { trace = true; continue; }
            if (!new[] { "--exe", "--duels", "--deck-0", "--deck-1", "--seed", "--speed-value", "--workers", "--python", "--output-dir", "--repo" }.Contains(key) || i + 1 == args.Length || !options.TryAdd(key, args[++i]))
                throw new ArgumentException("Unknown, repeated or incomplete option: " + key);
        }
        string Required(string key) => options.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing " + key);
        int Number(string key, int fallback) => options.TryGetValue(key, out var value) && int.TryParse(value, out int number) ? number : options.ContainsKey(key) ? throw new ArgumentException("Invalid " + key) : fallback;
        int duels = Number("--duels", 0), workers = Number("--workers", 1), speed = Number("--speed-value", 2400);
        if (duels < 1 || workers < 1 || workers > 16 || workers > duels || speed < 1) throw new ArgumentException("Require duels and speed > 0 and 1 <= workers <= min(16, duels)");
        if (options.TryGetValue("--seed", out var seed) && !uint.TryParse(seed, out _)) throw new ArgumentException("Seed must be an unsigned 32-bit decimal integer");
        string exe = Path.GetFullPath(Required("--exe"));
        if (!File.Exists(exe) || !Path.GetFileName(exe).Equals("joey_pc.exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--exe must be an existing joey_pc.exe");
        string exeHash; using (var stream = File.OpenRead(exe)) exeHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!exeHash.Equals(expectedExeHash, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Executable SHA256 mismatch: " + exeHash);
        foreach (string key in new[] { "--deck-0", "--deck-1" }) if (options.TryGetValue(key, out var path)) { path = Path.GetFullPath(path); if (!File.Exists(path)) throw new ArgumentException("Missing deck: " + path); options[key] = path; }
        string repo = Path.GetFullPath(options.GetValueOrDefault("--repo", Directory.GetCurrentDirectory()));
        if (!File.Exists(Path.Combine(repo, "yugilab", "__main__.py"))) throw new ArgumentException("--repo must point to the repository root");
        string root = Path.GetFullPath(options.GetValueOrDefault("--output-dir", Path.Combine(repo, "scratch", "experiments")));
        string relativeRoot = Path.GetRelativePath(repo, root);
        if (!Path.IsPathRooted(relativeRoot) && relativeRoot != ".." && !relativeRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            relativeRoot != "scratch" && !relativeRoot.StartsWith("scratch" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Results inside the repository must be under ignored scratch/");
        Directory.CreateDirectory(root);
        string run = Path.Combine(root, "experiment-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        string backend = Path.Combine(run, "backend"), stop = Path.Combine(run, "STOP"); Directory.CreateDirectory(backend);
        string log = Path.Combine(run, "backend.log");
        var start = new ProcessStartInfo(options.GetValueOrDefault("--python", "python")) { WorkingDirectory = repo, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in new[] { "-u", "-m", "yugilab", "--exe", exe, "--duels", duels.ToString(), "--workers", workers.ToString(), "--speed-value", speed.ToString(), "--output-dir", backend, "--cancel-file", stop }) start.ArgumentList.Add(arg);
        foreach (string key in new[] { "--deck-0", "--deck-1", "--seed" }) if (options.TryGetValue(key, out var value)) { start.ArgumentList.Add(key); start.ArgumentList.Add(value); }
        if (trace) start.ArgumentList.Add("--trace-ai");
        output.WriteLine("Experiment: " + run);
        int exitCode = 2; string? launchError = null; bool cancelled = false;
        using var logWriter = new StreamWriter(log) { AutoFlush = true };
        void Record(string? line) { if (line is null) return; lock (logWriter) { logWriter.WriteLine(line); output.WriteLine(line); output.Flush(); } }
        using (var process = new Process { StartInfo = start })
        {
            try
            {
                if (cancellation.IsCancellationRequested) { cancelled = true; exitCode = 130; }
                else
                {
                    process.Start();
                    using var registration = cancellation.Register(() => { try { File.WriteAllText(stop, "stop\n"); } catch (IOException) { } });
                    var stdout = Task.Run(async () => { string? line; while ((line = await process.StandardOutput.ReadLineAsync()) is not null) Record(line); });
                    var stderr = Task.Run(async () => { string? line; while ((line = await process.StandardError.ReadLineAsync()) is not null) Record(line); });
                    process.WaitForExit(); Task.WaitAll(stdout, stderr); exitCode = process.ExitCode; cancelled = cancellation.IsCancellationRequested;
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { launchError = ex.Message; Record("Backend error: " + ex.Message); }
        }
        logWriter.Dispose(); // Windows disallows reopening the log while StreamWriter owns it.
        var captures = Directory.GetFiles(backend, "*.jsonl", SearchOption.AllDirectories).Order().ToArray();
        var reports = new List<object>(); var documents = new Dictionary<string, CaptureDocument>(); bool capturesComplete = captures.Length == workers;
        foreach (string capture in captures)
        {
            try
            {
                var document = CaptureReader.Read(capture); var report = CaptureAnalyzer.Analyze(document);
                documents[capture] = document;
                bool complete = report.LiveCaptureComplete == true && !report.HasIntegrityErrors;
                capturesComplete &= complete;
                reports.Add(new { capture = Path.GetRelativePath(run, capture), complete, summary = report, combatContexts = CombatContextReader.Read(document) });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
            { capturesComplete = false; reports.Add(new { capture = Path.GetRelativePath(run, capture), complete = false, error = ex.Message }); }
        }
        bool backendVerified = false;
        if (workers == 1)
        {
            string content = File.ReadAllText(log);
            var spawned = Regex.Matches(content, @"Spawned suspended Joey PID (\d+)");
            if (spawned.Count == 1 && captures.Length == 1 && documents.TryGetValue(captures[0], out var doc))
            {
                string pid = spawned[0].Groups[1].Value;
                backendVerified = Regex.IsMatch(content, @"\[yugilab\] Owned Joey PID " + Regex.Escape(pid) + @" closed \(verified by Windows process handle\)\.") && Fields.Int(doc.Metadata.Raw, "processId")?.ToString() == pid && Fields.Int(doc.Metadata.Raw, "requestedDuels") == duels && doc.Footer is { } footer && Fields.Int(footer, "capturedDuels") == duels && Fields.Long(footer, "overwritten") == 0 && Fields.Long(footer, "eventsWritten") is not null;
            }
        }
        else
        {
            var manifests = Directory.GetFiles(backend, "manifest.json", SearchOption.AllDirectories);
            if (manifests.Length == 1) { using var m = JsonDocument.Parse(File.ReadAllText(manifests[0])); backendVerified = Fields.Bool(m.RootElement, "complete") == true && Fields.Int(m.RootElement, "capturedDuels") == duels; }
        }
        bool completeRun = !cancelled && exitCode == 0 && launchError is null && capturesComplete && backendVerified;
        File.WriteAllText(Path.Combine(run, "report.json"), JsonSerializer.Serialize(new { complete = completeRun, captures = reports }, JsonOptions));
        File.WriteAllText(Path.Combine(run, "manifest.json"), JsonSerializer.Serialize(new { complete = completeRun, cancelled, exitCode, launchError, requestedDuels = duels, workers, speedValue = speed, seed, traceAi = trace, executable = exe, exeSha256 = exeHash, deck0 = options.GetValueOrDefault("--deck-0"), deck1 = options.GetValueOrDefault("--deck-1"), captureCount = captures.Length, capturesComplete, backendVerified, report = "report.json", backendLog = "backend.log" }, JsonOptions));
        output.WriteLine($"Experiment complete={completeRun}; results: {run}");
        return cancelled ? 130 : completeRun ? 0 : exitCode != 0 ? exitCode : 2;
    }
}
