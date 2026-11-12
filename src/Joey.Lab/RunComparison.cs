using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Joey.Lab;

// A deliberately bounded comparison of seeded, complete live-duel captures.
public static class RunComparison
{
    public record Difference(string? Opcode, string? Caller, int Argument, uint Left, uint Right, string Origin);
    public record Counts(int ChangedRawCommands, int High16OnlyChangedCommands, int UnverifiedRawDifferenceCommands, Dictionary<string, int> HighWordOrigins);
    public record Duel(uint Index, Dictionary<string, bool> Checks, Counts Counts, List<Difference> Differences);
    public record Report(string Scope, bool ObservedMatch, bool RawCommandArgsMatch, int UnverifiedRawDifferences, int TotalChangedRawCommands, int TotalHigh16OnlyChangedCommands, Dictionary<string, int> HighWordOrigins, List<Duel> Duels);
    private record Source(JsonObject Header, List<JsonObject> Rows, uint Start, int Count);
    private static JsonNode? V(JsonObject o, string key) => o[key];
    private static string? S(JsonObject o, string key) => V(o, key)?.GetValue<string>();
    private static bool Eq(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(a, b);
    private static uint U(JsonObject o, string key) => V(o, key)?.GetValue<uint>() ?? throw new InvalidDataException($"Missing {key}");
    private static JsonObject Obj(string s) => JsonNode.Parse(s) as JsonObject ?? throw new InvalidDataException("Expected JSON object");
    private static bool Terminal(JsonObject e) => S(e, "kind") == "command" && string.Equals(S(e, "opcodeMasked"), "0x0004", StringComparison.OrdinalIgnoreCase);
    private static uint Seed(uint b, uint i) { unchecked { uint x = b + i; x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; return x ^ (x >> 16); } }
    private static Source Read(TextReader reader)
    {
        var header = Obj(reader.ReadLine() ?? throw new InvalidDataException("Missing metadata"));
        if (S(header, "kind") != "metadata" || header["rngSeedOverride"] is not JsonObject seed || S(seed, "algorithm") != "mix32-v1") throw new InvalidDataException("Seeded mix32-v1 metadata required");
        uint start = U(seed, "globalDuelStartIndex"), count = U(header, "requestedDuels");
        if (count == 0 || (ulong)start + count > 0x100000000UL || count > int.MaxValue) throw new InvalidDataException("Invalid seed range");
        var rows = new List<JsonObject>(); string? line; JsonObject? footer = null;
        while ((line = reader.ReadLine()) != null)
        {
            var row = Obj(line);
            if (S(row, "kind") == "capture_summary") { footer = row; if (reader.ReadLine() != null) throw new InvalidDataException("Trailing content after footer"); break; }
            if (U(row, "seq") != rows.Count + 1) throw new InvalidDataException("Noncontiguous sequence");
            rows.Add(row);
        }
        if (footer is null || footer["complete"]?.GetValue<bool>() != true || U(footer, "overwritten") != 0 || U(footer, "capturedDuels") != count || U(footer, "eventsWritten") != rows.Count || U(footer, "rngSeedOverridesApplied") != count) throw new InvalidDataException("Incomplete or inconsistent footer");
        return new(header, rows, start, (int)count);
    }
    private static List<Source> Sources(string path)
    {
        var sources = new List<Source>();
        StreamReader StrictReader(Stream stream) => new(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        if (Directory.Exists(path)) foreach (string f in Directory.GetFiles(path, "*.jsonl", SearchOption.AllDirectories).Order()) { using var r = StrictReader(File.OpenRead(f)); sources.Add(Read(r)); }
        else if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) { using var zip = ZipFile.OpenRead(path); foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName)) { using var r = StrictReader(e.Open()); sources.Add(Read(r)); } }
        else if (Path.GetExtension(path).Equals(".jsonl", StringComparison.OrdinalIgnoreCase)) { using var r = StrictReader(File.OpenRead(path)); sources.Add(Read(r)); }
        else throw new InvalidDataException("Expected ZIP, JSONL or directory");
        sources = sources.OrderBy(s => s.Start).ToList();
        if (sources.Count == 0) throw new InvalidDataException("No captures");
        for (int i = 1; i < sources.Count; i++) if ((ulong)sources[i-1].Start + (uint)sources[i-1].Count != sources[i].Start) throw new InvalidDataException("Overlapping or missing duel indices");
        return sources;
    }
    private static bool Settings(JsonObject a, JsonObject b) => Eq(a["speedValue"], b["speedValue"]) && Eq(a["deckPlans"], b["deckPlans"]) && Eq(a["rngSeedOverride"]?["baseSeed"], b["rngSeedOverride"]?["baseSeed"]) && Eq(a["rngSeedOverride"]?["algorithm"], b["rngSeedOverride"]?["algorithm"]);
    private static List<(uint, List<JsonObject>)> Split(List<Source> sources)
    {
        var duels = new List<(uint, List<JsonObject>)>();
        foreach (var s in sources)
        {
            var events = new List<JsonObject>(); int n = 0;
            foreach (var row in s.Rows)
            {
                events.Add(row); if (!Terminal(row)) continue;
                if (n >= s.Count) throw new InvalidDataException("Extra terminal");
                uint index = checked(s.Start + (uint)n++);
                var seeds = events.Where(e => S(e,"kind") == "demo_init" && S(e,"function") == "rng_seed_candidate" && S(e,"phase") == "enter" && e["seedOverrideApplied"]?.GetValue<bool>() == true).ToList();
                if (seeds.Count != 1 || U(seeds[0], "globalDuelIndex") != index || U(seeds[0], "effectiveSeedRaw") != Seed(U((JsonObject)s.Header["rngSeedOverride"]!, "baseSeed"), index)) throw new InvalidDataException("Missing or incorrect applied seed");
                duels.Add((index, events)); events = new();
            }
            if (events.Count != 0 || n != s.Count) throw new InvalidDataException("Missing terminal");
        }
        return duels;
    }
    private static string Origin(JsonObject a, int i, uint x, uint y)
    {
        if ((x & 65535) != (y & 65535)) return "low16_changed";
        string key = $"{S(a,"opcodeMasked")?.ToLowerInvariant()}:{S(a,"caller")?.ToLowerInvariant()}:{i}";
        if (new[]{"0x00ce:0x5abeab:1","0x00e6:0x5abd70:1","0x006f:0x5abf22:1","0x00cf:0x57977f:1","0x0072:0x5783f2:1","0x00df:0x595142:1","0x0075:0x578459:1","0x0072:0x5783bc:1","0x00e6:0x5acf72:1"}.Contains(key)) return "verified_card_id_pointer_residue";
        if (key is "0x0061:0x5ba000:0" or "0x0037:0x58cf96:0") return "verified_partial_register_write";
        return "unresolved_high16";
    }
    private static JsonArray Project(IEnumerable<JsonObject> rows, params string[] fields) => new(rows.Select(e => (JsonNode?)new JsonArray(fields.Select(f => e[f]?.DeepClone()).ToArray())).ToArray());
    private static JsonArray Strip(IEnumerable<JsonObject> rows) => new(rows.Select(e => { var c = (JsonObject)e.DeepClone(); foreach (string k in new[]{"seq","callId","timestampMs","threadId"}) c.Remove(k); return (JsonNode?)c; }).ToArray());
    private static uint[] Args(JsonObject e)
    {
        var values = (e["args"] as JsonArray ?? throw new InvalidDataException("Missing command args")).Select(x => x?.GetValue<uint>() ?? throw new InvalidDataException("Invalid command arg")).ToArray();
        if (values.Length != 4) throw new InvalidDataException("Expected four command args");
        return values;
    }
    public static Report Compare(string left, string right)
    {
        var a = Sources(left); var b = Sources(right);
        if (a.Concat(b).Any(s => !Settings(a[0].Header, s.Header))) throw new InvalidDataException("Incompatible run settings");
        var da = Split(a); var db = Split(b);
        if (da.Count != db.Count || !da.Select(x => x.Item1).SequenceEqual(db.Select(x => x.Item1))) throw new InvalidDataException("Mismatched duel ranges");
        var results = new List<Duel>();
        for (int d = 0; d < da.Count; d++)
        {
            var l = da[d].Item2; var r = db[d].Item2;
            List<JsonObject> Kind(List<JsonObject> es, string kind) => es.Where(e => S(e,"kind") == kind).ToList();
            var lc = Kind(l,"command"); var rc = Kind(r,"command"); var diffs = new List<Difference>();
            bool structure = Eq(Project(lc,"opcodeRaw","opcodeMasked","sideBit15","caller"), Project(rc,"opcodeRaw","opcodeMasked","sideBit15","caller"));
            bool low = structure, raw = structure;
            var origins = new Dictionary<string, int>(StringComparer.Ordinal); int changed = 0, highOnly = 0;
            for (int i = 0; i < Math.Min(lc.Count,rc.Count); i++)
            {
                var x = Args(lc[i]); var y = Args(rc[i]); var commandOrigins = new List<string>(); bool onlyHigh = true;
                for (int j = 0; j < 4; j++) if (x[j] != y[j])
                {
                    raw = false; if ((x[j] & 65535) != (y[j] & 65535)) { low = false; onlyHigh = false; }
                    string origin = Origin(lc[i], j, x[j], y[j]); commandOrigins.Add(origin);
                    diffs.Add(new(S(lc[i],"opcodeMasked"), S(lc[i],"caller"), j, x[j], y[j], origin));
                }
                if (commandOrigins.Count == 0) continue;
                changed++; if (onlyHigh) highOnly++;
                string category = commandOrigins.Count == 1 ? commandOrigins[0] : "multiple:" + string.Join('|', commandOrigins.Order(StringComparer.Ordinal));
                origins[category] = origins.GetValueOrDefault(category) + 1;
            }
            var la = Kind(l,"ai_probe"); var ra = Kind(r,"ai_probe");
            var ls = l.Where(e => S(e,"kind") == "demo_init" && S(e,"function") == "deck_shuffle" && S(e,"phase") == "leave").ToList(); var rs = r.Where(e => S(e,"kind") == "demo_init" && S(e,"function") == "deck_shuffle" && S(e,"phase") == "leave").ToList();
            JsonArray Queue(List<JsonObject> es) => new(es.Select(e => Args(e)).Where(x => (x[0] & 32767) is 0x37 or 0x61 or 0x86).Select(x => (JsonNode?)new JsonArray(x.Select(v => (JsonNode?)JsonValue.Create(v & 65535)).ToArray())).ToArray());
            var lt = lc.Where(Terminal).ToList(); var rt = rc.Where(Terminal).ToList();
            var checks = new Dictionary<string,bool> {
                ["sameEventLayout"] = Eq(Project(l,"kind","function","phase"),Project(r,"kind","function","phase")),
                ["sameDeckShuffles"] = ls.Count > 0 && rs.Count > 0 && Eq(Project(ls,"side","deck","shufflePasses"),Project(rs,"side","deck","shufflePasses")),
                ["sameAiProbeEvents"] = la.Count > 0 && ra.Count > 0 && Eq(Strip(la),Strip(ra)),
                ["sameExecutorEvents"] = Eq(Strip(Kind(l,"demo_executor")),Strip(Kind(r,"demo_executor"))),
                ["sameCommandStructure"] = structure, ["sameCommandLow16"] = low, ["sameRawCommandArgs"] = raw,
                ["sameTerminalNativeState"] = lt.Count > 0 && rt.Count > 0 && lt.All(e => e["terminalNativeState"] is JsonObject) && rt.All(e => e["terminalNativeState"] is JsonObject) && Eq(Project(lt,"terminalNativeState"),Project(rt,"terminalNativeState")),
                ["sameCeNativeStates"] = Eq(Project(lc.Where(e => S(e,"opcodeMasked") == "0x00CE"),"ceNativeState"),Project(rc.Where(e => S(e,"opcodeMasked") == "0x00CE"),"ceNativeState")),
                ["same86WrapperCallers"] = Eq(Project(lc.Where(e => S(e,"opcodeMasked") == "0x0086" && S(e,"caller") == "0x574804"),"wrapper86Caller"),Project(rc.Where(e => S(e,"opcodeMasked") == "0x0086" && S(e,"caller") == "0x574804"),"wrapper86Caller")),
                ["sameDefaultBranchQueuePackets"] = Eq(Queue(lc),Queue(rc)),
                ["sameNonEntryReseeds"] = Eq(Project(l.Where(e => S(e,"kind") == "demo_init" && S(e,"function") == "rng_seed_candidate" && S(e,"phase") == "enter" && e["duelEntrySeedCall"]?.GetValue<bool>() != true),"caller","candidateSeedRaw"),Project(r.Where(e => S(e,"kind") == "demo_init" && S(e,"function") == "rng_seed_candidate" && S(e,"phase") == "enter" && e["duelEntrySeedCall"]?.GetValue<bool>() != true),"caller","candidateSeedRaw")) };
            int unverified = origins.Where(x => !x.Key.StartsWith("verified_", StringComparison.Ordinal)).Sum(x => x.Value);
            results.Add(new(da[d].Item1,checks,new(changed, highOnly, unverified, origins),diffs));
        }
        bool observed = results.Count > 0 && results.All(d => d.Checks.All(c => c.Key == "sameRawCommandArgs" || c.Value));
        var totalOrigins = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in results.SelectMany(d => d.Counts.HighWordOrigins)) totalOrigins[item.Key] = totalOrigins.GetValueOrDefault(item.Key) + item.Value;
        return new("Observed checkpoints only; raw differences preserved; no winner inferred.", observed, observed && results.All(d => d.Checks["sameRawCommandArgs"]), results.Sum(d => d.Counts.UnverifiedRawDifferenceCommands), results.Sum(d => d.Counts.ChangedRawCommands), results.Sum(d => d.Counts.High16OnlyChangedCommands), totalOrigins, results);
    }
}
