using Joey.Lab;
using System.Text.Json;

internal static class LabCaptureChecks
{
    public static void Run(Action<bool, string> check)
    {
        LabExperimentChecks.Run(check);
        LabCompareChecks.Run(check);
        LabCombatContextChecks.Run(check);
        CaptureReport Analyze(string text) => CaptureAnalyzer.Analyze(CaptureReader.Read(new StringReader(text), "synthetic", "unhashed-test-input"));
        void Reject(string text, string name)
        {
            try { Analyze(text); } catch (InvalidDataException) { check(true, name); return; }
            throw new Exception("Expected invalid capture: " + name);
        }
        const string header = "{\"kind\":\"metadata\",\"seen\":1,\"retained\":1,\"overwritten\":0}";
        const string command = "{\"kind\":\"command\",\"seq\":1,\"args\":[32772,4294967295]}";
        var report = Analyze(header + "\n" + command);
        check(!report.HasIntegrityErrors && report.TerminalCommandCount == 1 && report.CommandsByMaskedOpcode["0x0004"] == 1,
            "Lab counts masked 8004 terminal while accepting full uint32 command words");
        check(report.Metadata.ExeSha256 is null && report.LiveCaptureComplete is null && report.Issues.Any(i => i.Severity == "warning"),
            "Lab does not invent missing executable hash or live completion");
        Reject("", "Lab rejects empty capture");
        Reject(command, "Lab requires metadata first");
        Reject(header + "\n{", "Lab rejects malformed JSON with source line");
        Reject(header + "\n" + header, "Lab rejects duplicate metadata");
        Reject(header + "\n{\"kind\":\"ai_probe\",\"function\":\"combat_event\",\"phase\":\"leave\",\"outputHex\":\"00\"}",
            "Lab validates native combat buffer size through Core");
        Reject(header + "\n{\"kind\":\"ai_probe\",\"function\":\"computed_stats\",\"phase\":\"leave\",\"outputHex\":\"XX\"}",
            "Lab rejects malformed native stat hex");
        check(Analyze(header.Replace("\"seen\":1", "\"seen\":3") + "\n" + command).HasIntegrityErrors,
            "Lab detects metadata count mismatch");
        check(Analyze(header.Replace("\"overwritten\":0", "\"overwritten\":1") + "\n" + command).HasIntegrityErrors,
            "Lab detects lost events");
        check(Analyze(header + "\n" + command.Replace("\"seq\":1", "\"seq\":2")).HasIntegrityErrors,
            "Lab detects a sequence gap");
        var unknown = CaptureReader.Read(new StringReader(header + "\n{\"kind\":\"future_probe\",\"seq\":1,\"future\":42}"), "test", "test");
        check(unknown.Events[0].Raw.GetProperty("future").GetInt32() == 42 && CaptureAnalyzer.Analyze(unknown).CountsByKind["future_probe"] == 1,
            "Lab preserves unfamiliar events and fields rather than silently dropping them");
        var live = Analyze("{\"kind\":\"metadata\",\"mode\":\"live_duels\",\"formatVersion\":2}\n" + command +
            "\n{\"kind\":\"capture_summary\",\"eventsWritten\":1,\"capturedDuels\":1,\"overwritten\":0,\"complete\":true}");
        check(!live.HasIntegrityErrors && live.LiveCaptureComplete == true && live.EventCount == 1,
            "Lab distinguishes a live-duels footer from sequenced events");
        var unfinished = Analyze("{\"kind\":\"metadata\",\"mode\":\"live_duels\"}\n" + command);
        check(unfinished.LiveCaptureComplete is null && unfinished.Issues.Any(i => i.Message.Contains("lacks final")),
            "Lab preserves unfinished live capture as unknown completion");
        var output = new StringWriter(); var error = new StringWriter();
        check(Joey.Lab.Program.Run(["--help"], output, error) == 0 && output.ToString().Contains("summarize"), "Lab CLI help needs no capture or Frida");
        check(Joey.Lab.Program.Run(["summarize", "unused", "--expected-sha256", "bad"], output, error) == 2,
            "Lab CLI rejects malformed fingerprint before opening a file");

        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "lab-capture-prefix.jsonl");
        string left = Path.Combine(AppContext.BaseDirectory, "Fixtures", "compare-synthetic-left.jsonl");
        string right = Path.Combine(AppContext.BaseDirectory, "Fixtures", "compare-synthetic-right.jsonl");
        var comparison = RunComparison.Compare(left, right);
        check(comparison.ObservedMatch && !comparison.RawCommandArgsMatch && comparison.Duels.Count == 2 &&
            comparison.Duels.All(d => d.Differences.Single().Origin == "verified_card_id_pointer_residue"),
            "Lab compare agrees with Python synthetic high-word fixture without hiding raw differences");
        check(Joey.Lab.Program.Run(["compare", left, right, "--strict-raw"], new StringWriter(), new StringWriter()) == 1,
            "Lab compare strict raw detects differing words");
        string damaged = Path.Combine(Path.GetTempPath(), $"joey-compare-{Guid.NewGuid()}.jsonl");
        try
        {
            File.WriteAllText(damaged, File.ReadAllText(right).Replace("\"complete\": true", "\"complete\": false"));
            check(Joey.Lab.Program.Run(["compare", left, damaged], new StringWriter(), new StringWriter()) == 2,
                "Lab compare rejects incomplete footer");
        }
        finally { File.Delete(damaged); }
        var document = CaptureReader.Read(fixture);
        var prefix = CaptureAnalyzer.Analyze(document);
        check(!prefix.HasIntegrityErrors && prefix.EventCount == 30 && prefix.ComputedStatsCount == 14 && prefix.CombatAssessments.Count == 1,
            "Lab imports unchanged events1..30 from the historical capture with adjusted fixture metadata");
        var first = prefix.CombatAssessments.Single();
        check(first.Sequence == 25 && first.OutputHex == "0808C800" && first.Output.Score == 200 &&
            first.AttackerStats?.CardIndex == 550 && first.DefenderStats?.CardIndex == 24 && first.SelectorCallId == 1597,
            "Lab pairs observed Gemini Elf/Axe Raider buffers inside the original selector call");
        check(first.UnknownContext.Length > 0 && first.Pairing == "paired_computed_stats" && prefix.Metadata.ExeSha256 is null,
            "Paired native stats do not become a complete combat context");
        try { CaptureReader.Read(fixture, new string('0', 64)); throw new Exception("Expected SHA mismatch"); }
        catch (InvalidDataException) { check(true, "Lab verifies capture fingerprint before analysis"); }
        check(CaptureReader.Read(fixture, document.Sha256).Sha256 == document.Sha256, "Lab supports explicit capture SHA verification");
        check(Joey.Lab.Program.Run(["summarize", fixture, "--expected-sha256", new string('0', 64)], new StringWriter(), new StringWriter()) == 2,
            "Lab CLI reports capture fingerprint mismatch without an unhandled exception");
        output = new(); error = new();
        check(Joey.Lab.Program.Run(["summarize", fixture, "--json"], output, error) == 0 &&
            JsonDocument.Parse(output.ToString()).RootElement.GetProperty("pairedCombatStats").GetInt32() == 1,
            "Lab CLI exports the same typed report as its reusable analyzer");

        // Corrupt correlation inputs without replacing missing data by older outputs.
        string text = File.ReadAllText(fixture);
        var nodes = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => System.Text.Json.Nodes.JsonNode.Parse(line)!).ToArray();
        var defender = nodes.Single(n => n["seq"]?.GetValue<int>() == 24);
        defender["rawArgs"] = System.Text.Json.Nodes.JsonNode.Parse("[1,4]");
        var missing = Analyze(string.Join('\n', nodes.Select(n => n.ToJsonString()))).CombatAssessments.Single();
        check(missing.DefenderStats is null && missing.Pairing == "incomplete_computed_stats",
            "Lab does not borrow an unrelated defender stat buffer");
        defender["threadId"] = 999;
        check(Analyze(string.Join('\n', nodes.Select(n => n.ToJsonString()))).HasIntegrityErrors,
            "Lab detects cross-thread output outside its selector");
        var leave = nodes.Single(n => n["seq"]?.GetValue<int>() == 26);
        leave["callId"] = 999;
        check(Analyze(string.Join('\n', nodes.Select(n => n.ToJsonString()))).HasIntegrityErrors,
            "Lab detects unmatched selector returns and open windows");

        string real = Path.Combine("scratch", "traces", "commands-20261111-022940-355228-pid36348.jsonl");
        if (File.Exists(real))
        {
            var native = CaptureAnalyzer.Analyze(CaptureReader.Read(real, "dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90"));
            check(!native.HasIntegrityErrors && native.EventCount == 1120 && native.CountsByKind["command"] == 649 && native.ComputedStatsCount == 402 &&
                native.CombatAssessments.Count == 22 && native.PairedCombatStats == 14 &&
                native.Selectors.Count(s => s.Function == "select_normal") == 13 && native.Selectors.Count(s => s.Function == "select_priority") == 10 &&
                native.Selectors.Count(s => s.Function == "select_normal" && s.SelectedHandIndex < 0) == 7,
                "Real capture Lab counts agree with independent Python audit and versioned combat corpus");
        }
    }
}
