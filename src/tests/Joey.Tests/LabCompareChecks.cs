using System.Text.Json.Nodes;
using Joey.Lab;

internal static class LabCompareChecks
{
    public static void Run(Action<bool, string> check)
    {
        string fixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        string left = Path.Combine(fixtureDir, "compare-synthetic-left.jsonl");
        string original = File.ReadAllText(Path.Combine(fixtureDir, "compare-synthetic-right.jsonl"));
        string temp = Path.Combine(Path.GetTempPath(), "joey-compare-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(temp);
        string right = Path.Combine(temp, "right.jsonl");
        try
        {
            JsonObject[] Rows() => original.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => (JsonObject)JsonNode.Parse(x)!).ToArray();
            void Write(JsonObject[] rows) => File.WriteAllText(right, string.Join('\n', rows.Select(x => x.ToJsonString())) + "\n");
            int Run(params string[] options) => Joey.Lab.Program.Run(["compare", left, right, .. options], new StringWriter(), new StringWriter());
            void Reject(string name, Action<JsonObject[]> mutate)
            {
                var rows = Rows(); mutate(rows); Write(rows);
                check(Run() == 2, "Lab compare rejects " + name);
            }
            Reject("bad metadata", r => r[0]["kind"] = "command");
            Reject("unsupported seed algorithm", r => r[0]["rngSeedOverride"]!["algorithm"] = "other");
            Reject("missing base seed", r => r[0]["rngSeedOverride"]!.AsObject().Remove("baseSeed"));
            Reject("zero duel count", r => r[0]["requestedDuels"] = 0);
            Reject("overflowing seed range", r => r[0]["rngSeedOverride"]!["globalDuelStartIndex"] = uint.MaxValue);
            Reject("different base seed", r => r[0]["rngSeedOverride"]!["baseSeed"] = 12346);
            Reject("different speed", r => r[0]["speedValue"] = 601);
            Reject("different deck plans", r => r[0]["deckPlans"]!["0"]!["main"]![0] = 2);
            Reject("different duel range", r => r[0]["rngSeedOverride"]!["globalDuelStartIndex"] = 2);
            Reject("incorrect effective seed", r => r[2]["effectiveSeedRaw"] = 0);
            Reject("incorrect global duel index", r => r[2]["globalDuelIndex"] = 1);
            Reject("missing seed override", r => r[2]["seedOverrideApplied"] = false);
            Reject("sequence gap", r => r[3]["seq"] = 999);
            Reject("incomplete footer", r => r[^1]["complete"] = false);
            Reject("lost events", r => r[^1]["overwritten"] = 1);
            Reject("incorrect footer count", r => r[^1]["eventsWritten"] = 1);
            Reject("missing terminal", r => r[9]["opcodeMasked"] = "0x0005");
            Reject("empty command arguments", r => r[7]["args"] = new JsonArray());
            Reject("negative command argument", r => r[7]["args"]![0] = -1);
            Reject("noninteger command argument", r => r[7]["args"]![0] = "206");
            File.WriteAllText(right, original + "{}\n");
            check(Run() == 2, "Lab compare rejects trailing content after footer");
            File.WriteAllText(right, "{\n");
            check(Run() == 2, "Lab compare rejects malformed JSON");
            File.WriteAllBytes(right, [0xff, 0xfe, 0xff]);
            check(Run() == 2, "Lab compare rejects invalid UTF-8");
            check(Joey.Lab.Program.Run(["compare", left, Path.Combine(temp, "missing.zip")], new StringWriter(), new StringWriter()) == 2, "Lab compare rejects missing input");
            Write(Rows());
            string workers = Path.Combine(temp, "workers"); Directory.CreateDirectory(workers);
            File.Copy(right, Path.Combine(workers, "one.jsonl")); File.Copy(right, Path.Combine(workers, "two.jsonl"));
            check(Joey.Lab.Program.Run(["compare", left, workers], new StringWriter(), new StringWriter()) == 2,
                "Lab compare rejects overlapping worker duel ranges");
            check(Run("--unknown") == 2 && Run("--strict-raw") == 1 && Run("--strict-unverified") == 0 && Run() == 0,
                "Lab compare CLI returns 0/1/2 for observed match, strict mismatch and invalid options");
            check(Joey.Lab.Program.Run(["compare", left, left, "--strict-raw", "--strict-unverified"], new StringWriter(), new StringWriter()) == 0,
                "Lab compare strict flags accept identical raw input");
            var multiple = Rows(); multiple[7]["args"]![2] = 65536; Write(multiple);
            var report = RunComparison.Compare(left, right);
            string category = "multiple:unresolved_high16|verified_card_id_pointer_residue";
            check(report.ObservedMatch && report.TotalChangedRawCommands == 2 && report.TotalHigh16OnlyChangedCommands == 2 &&
                report.UnverifiedRawDifferences == 1 && report.HighWordOrigins[category] == 1 && report.Duels[0].Differences.Count == 2 && Run("--strict-unverified") == 1,
                "Lab compare counts multiple argument changes once per command and retains each raw value");
            var low = Rows(); low[7]["args"]![2] = 1; Write(low);
            report = RunComparison.Compare(left, right);
            check(!report.ObservedMatch && report.TotalHigh16OnlyChangedCommands == 1 && Run() == 1,
                "Lab compare reports low-word changes as divergence");
            var unresolved = Rows(); unresolved[7]["caller"] = "0x574804"; Write(unresolved);
            // Both callsites must match for the comparison to remain structurally equivalent.
            string otherLeft = Path.Combine(temp, "left.jsonl");
            var leftRows = File.ReadAllLines(left).Select(x => (JsonObject)JsonNode.Parse(x)!).ToArray(); leftRows[7]["caller"] = "0x574804";
            File.WriteAllText(otherLeft, string.Join('\n', leftRows.Select(x => x.ToJsonString())) + "\n");
            report = RunComparison.Compare(otherLeft, right);
            check(report.ObservedMatch && report.UnverifiedRawDifferences == 1 && report.HighWordOrigins["unresolved_high16"] == 1,
                "Lab compare retains unverified high word at an unknown callsite");
        }
        finally { Directory.Delete(temp, true); }
    }
}
