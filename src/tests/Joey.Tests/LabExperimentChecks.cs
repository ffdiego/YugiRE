using System.Security.Cryptography;
using System.Text.Json;
using Joey.Lab;

internal static class LabExperimentChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (OperatingSystem.IsWindows()) return; // The simulated executable below is a POSIX shell script.
        string root = Path.Combine(Path.GetTempPath(), "joey-experiment-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "yugilab"));
        File.WriteAllText(Path.Combine(root, "yugilab", "__main__.py"), "");
        string exe = Path.Combine(root, "joey_pc.exe"); File.WriteAllText(exe, "synthetic");
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe))).ToLowerInvariant();
        string script = Path.Combine(root, "fake-python");
        File.WriteAllText(script, """
#!/bin/sh
while [ "$#" -gt 0 ]; do
  case "$1" in
    --output-dir) out="$2"; shift ;;
    --cancel-file) stop="$2"; shift ;;
  esac
  shift
done
echo 'Spawned suspended Joey PID 123'
if [ -f "$TEST_WAIT" ]; then while [ ! -f "$stop" ]; do sleep 0.05; done; exit 130; fi
if [ -f "$TEST_FAIL" ]; then echo failure >&2; exit 7; fi
cat > "$out/duels-test-pid123.jsonl" <<'DATA'
{"kind":"metadata","mode":"live_duels","processId":123,"requestedDuels":1}
{"kind":"future_probe","seq":1,"unknown":42}
{"kind":"command","seq":2,"args":[4]}
{"kind":"capture_summary","complete":true,"capturedDuels":1,"eventsWritten":2,"overwritten":0}
DATA
echo '[yugilab] Owned Joey PID 123 closed (verified by Windows process handle).'
""");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string[] args = ["--exe", exe, "--duels", "1", "--repo", root, "--python", script];
        string results = Path.Combine(root, "scratch", "experiments");
        try
        {
            var output = new StringWriter(); var error = new StringWriter();
            int result = Experiment.Run(args, output, error, expectedExeHash: hash);
            if (result != 0) throw new Exception($"experiment exit {result}: {output} {error} " + string.Join(" ", Directory.GetFiles(results, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText)));
            check(result == 0, "Experiment supervises simulated backend and verifies completion");
            string run = Directory.GetDirectories(results).Single();
            using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json"))))
                check(manifest.RootElement.GetProperty("complete").GetBoolean() && File.ReadAllText(Path.Combine(run, "report.json")).Contains("future_probe"), "Experiment discovers capture and preserves unknown event in analysis");
            string fail = Path.Combine(root, "fail"); File.WriteAllText(fail, ""); Environment.SetEnvironmentVariable("TEST_FAIL", fail);
            check(Experiment.Run(args, new StringWriter(), new StringWriter(), expectedExeHash: hash) == 7, "Experiment propagates backend error");
            var failureManifest = Directory.GetFiles(results, "manifest.json", SearchOption.AllDirectories)
                .Select(path => JsonDocument.Parse(File.ReadAllText(path)))
                .Single(doc => doc.RootElement.GetProperty("exitCode").GetInt32() == 7);
            using (failureManifest)
                check(!failureManifest.RootElement.GetProperty("complete").GetBoolean(), "Experiment writes an incomplete manifest after backend startup failure");
            Environment.SetEnvironmentVariable("TEST_FAIL", null);
            string wait = Path.Combine(root, "wait"); File.WriteAllText(wait, ""); Environment.SetEnvironmentVariable("TEST_WAIT", wait);
            using var cancel = new CancellationTokenSource(); cancel.CancelAfter(500);
            check(Experiment.Run(args, new StringWriter(), new StringWriter(), cancel.Token, hash) == 130, "Experiment requests graceful cancellation and waits for backend");
            check(Experiment.Run(["--exe", exe, "--duels", "0", "--repo", root], new StringWriter(), new StringWriter(), expectedExeHash: hash) == 2, "Experiment rejects invalid duel count");
        }
        finally { Environment.SetEnvironmentVariable("TEST_FAIL", null); Environment.SetEnvironmentVariable("TEST_WAIT", null); Directory.Delete(root, true); }
    }
}
