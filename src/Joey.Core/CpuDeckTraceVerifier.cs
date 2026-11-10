using System.Text.Json;

namespace Joey.Core;

/// <summary>Offline validation of observational JSONL. Missing evidence never counts as agreement.</summary>
public static class CpuDeckTraceVerifier
{
    public const string ExeSha256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    public const string DatSha256 = "7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed";
    public sealed record Issue(string Category, string SelectionId, string Detail);
    public sealed record Selection(string SelectionId, string ResourcePath, uint RawRoll, int MainCount, bool MtChecked);
    public sealed record Report(IReadOnlyList<Selection> NormalSelections, IReadOnlyList<Issue> Issues)
    {
        public int ExitCode => Issues.Any(i => i.Category == "mismatch") ? 1 :
            Issues.Count > 0 || NormalSelections.Count == 0 ? 2 : 0;
    }
    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString() ?? "";
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static string Kind(JsonElement e) => Text(e, "kind");
    private static string Phase(JsonElement e) => Text(e, "phase");
    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();

    // resolve returns internal indices, in source order [file0,file2,file1].
    public static Report Validate(JsonElement metadata, IReadOnlyList<JsonElement> events,
        Func<string, IReadOnlyList<IReadOnlyList<int>>> resolve)
    {
        var issues = new List<Issue>();
        var results = new List<Selection>();
        void Problem(string detail, string id = "", string category = "incomplete") => issues.Add(new(category,id,detail));
        try
        {
            if (Kind(metadata) != "metadata" || Text(metadata,"exeSha256") != ExeSha256)
                Problem("Missing or unsupported host EXE fingerprint");
            if (Int(metadata,"overwritten") != 0 || Int(metadata,"seen") != events.Count ||
                Int(metadata,"retained") != events.Count)
                Problem("Lost, drained or truncated events; seen/retained/overwritten disagree");
            for (int i = 0; i < events.Count; i++)
                if (Int(events[i],"seq") != i+1) { Problem("Noncontiguous event sequence"); break; }
            if (!events.Any(e => Kind(e) == "deck_probe" && Phase(e) == "on" &&
                Text(e,"schema") == "joey-cpu-deck-v1" && Text(e,"exeSha256") == ExeSha256))
                Problem("Missing supported deck probe start");
            if (!events.Any(e => Kind(e) == "deck_probe" && Phase(e) == "off")) Problem("Missing deck probe stop");
            foreach (var e in events.Where(e => Kind(e).StartsWith("deck_",StringComparison.Ordinal)))
            {
                if (e.TryGetProperty("readError",out var error)) Problem("Read failure: " + error.GetString());
                if (Kind(e) == "deck_error") Problem("Observer error: " + Phase(e));
            }
            foreach (var scene in events.Where(e => Kind(e) == "deck_scene" && Phase(e) == "enter" && Int(e,"requestedScene") is 0 or 1))
                if (!scene.TryGetProperty("selectionId",out var bound) || bound.ValueKind != JsonValueKind.String)
                    Problem("Normal scene without a bound selection");
            var selectionEvents = events.Where(e => Kind(e) == "deck_selection").ToArray();
            foreach (var orphan in selectionEvents.Where(e => !e.TryGetProperty("selectionId",out var v) || v.ValueKind != JsonValueKind.String))
                Problem("Unbound selector/RNG event at seq " + Int(orphan,"seq"));
            var groups = selectionEvents.Where(e => e.TryGetProperty("selectionId",out var v) && v.ValueKind == JsonValueKind.String)
                .GroupBy(e => Text(e,"selectionId"));
            foreach (var group in groups)
            {
                string id = group.Key;
                try
                {
                    JsonElement One(IEnumerable<JsonElement> values, string label)
                    {
                        var array = values.ToArray();
                        if (array.Length != 1) throw new InvalidDataException($"Expected one {label}, got {array.Length}");
                        return array[0];
                    }
                    var enter = One(group.Where(e => Phase(e) == "enter"),"selector enter");
                    var rng = One(group.Where(e => Phase(e) == "rng"),"RNG");
                    var leave = One(group.Where(e => Phase(e) == "leave"),"selector leave");
                    var linkedScenes = events.Where(e => Kind(e) == "deck_scene" && Phase(e) == "enter" &&
                        e.TryGetProperty("selectionId",out var v) && v.GetString() == id).ToArray();
                    // Demo's inner selection is explicitly identified by its native caller.
                    if (Int(enter,"caller") == 0x44bf50) continue;
                    var scene = One(linkedScenes,"bound scene");
                    int requested = Int(scene,"requestedScene");
                    if (requested == 7) continue;
                    if (requested is not (0 or 1)) throw new InvalidDataException("Unsupported scene " + requested);
                    int expectedCaller = Int(scene,"caller") switch {
                        0x48a3c5 when requested == 0 => 0x48a3a8,
                        0x48ad71 when requested == 0 => 0x48ad50,
                        0x48ae2d when requested == 1 => 0x48ae0a,
                        _ => throw new InvalidDataException("Unverified normal scene caller")
                    };
                    if (Int(enter,"caller") != expectedCaller ||
                        Text(scene,"correlationProof") != "caller+thread+pointer+path" ||
                        Text(scene,"argumentPointer") != Text(leave,"resourceDataPointer"))
                        throw new InvalidDataException("Selector/scene correlation proof failed");
                    string sceneId = Text(scene,"sceneId");
                    var sceneLeave = One(events.Where(e => Kind(e) == "deck_scene" && Phase(e) == "leave" &&
                        Text(e,"sceneId") == sceneId),"scene leave");
                    var load = One(events.Where(e => Kind(e) == "deck_loader" && Phase(e) == "enter" &&
                        e.TryGetProperty("selectionId",out var v) && v.GetString() == id),"loader enter");
                    var loaded = One(events.Where(e => Kind(e) == "deck_loader" && Phase(e) == "leave" &&
                        Text(e,"loadId") == Text(load,"loadId")),"loader leave");
                    if (Int(load,"caller") != 0x44c0e8 || Int(load,"loaderType") != 1 ||
                        Int(loaded,"caller") != 0x44c0e8 || Int(loaded,"loaderType") != 1 ||
                        Text(loaded,"selectionId") != id || Text(load,"sceneId") != sceneId ||
                        Text(loaded,"sceneId") != sceneId || Text(sceneLeave,"selectionId") != id || Int(loaded,"returnCode") != 0)
                        throw new InvalidDataException("Normal source1 load missing, failed or unbound");
                    var ordered = new[] {enter,rng,leave,scene,load,loaded,sceneLeave};
                    if (ordered.Any(e => Int(e,"threadId") != Int(enter,"threadId")) ||
                        ordered.Zip(ordered.Skip(1)).Any(pair => Int(pair.First,"seq") >= Int(pair.Second,"seq")))
                        throw new InvalidDataException("Cross-thread or out-of-order selection chain");
                    if (!leave.GetProperty("returnObjectMatches").GetBoolean())
                        throw new InvalidDataException("Unexpected hidden std::string result pointer");
                    var profile = enter.GetProperty("profile");
                    byte baseValue = profile.GetProperty("baseDifficulty").GetByte(), pool = profile.GetProperty("pool").GetByte();
                    int site = Int(rng,"site");
                    if (site != (pool == 0 ? 0x44bda5 : 0x44bc84)) Problem("RNG site disagrees with profile pool",id,"mismatch");
                    uint roll = rng.GetProperty("rawRoll").GetUInt32();
                    string predicted = CpuDeckSelection.Select(baseValue,pool,roll), actual = Text(leave,"resourcePath");
                    if (Normalize(actual) != Normalize(predicted)) Problem($"Path: expected {predicted}, observed {actual}",id,"mismatch");
                    if (Normalize(Text(scene,"resourcePath")) != Normalize(actual) ||
                        Normalize(Text(load,"resourcePath")) != Normalize(actual))
                        Problem("Loader/scene path differs from selector result",id,"mismatch");
                    bool mtChecked = enter.TryGetProperty("mtBefore",out var mt);
                    if (mtChecked)
                    {
                        uint[] words = mt.GetProperty("words").EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
                        uint next = NativeDeckRng.FromSnapshot(words,Int(mt,"index")).Next32();
                        if (next != roll) Problem($"MT: predicted {next}, observed {roll}; check concurrent MT consumers",id,"mismatch");
                    }
                    // If snapshot was requested, its absence must not become a silent pass.
                    var probe = events.Last(e => Kind(e) == "deck_probe" && Phase(e) == "on" && Int(e,"seq") < Int(enter,"seq"));
                    if (probe.GetProperty("captureMt").GetBoolean() && !mtChecked) Problem("Requested MT snapshot missing",id);
                    var expectedGroups = resolve(actual);
                    var source = loaded.GetProperty("sourceSlot1");
                    string[] names = ["main","second","third"];
                    for (int g = 0; g < 3; g++)
                    {
                        int[] observed = source.GetProperty(names[g]).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                        if (Int(source,names[g]+"Count") != observed.Length || !observed.SequenceEqual(expectedGroups[g]))
                        {
                            int first = Enumerable.Range(0,Math.Min(observed.Length,expectedGroups[g].Count))
                                .FirstOrDefault(i => observed[i] != expectedGroups[g][i], -1);
                            Problem($"{names[g]} differs: DAT count {expectedGroups[g].Count}, observed {observed.Length}, first differing index {first}",id,"mismatch");
                        }
                    }
                    results.Add(new(id,actual,roll,Int(source,"mainCount"),mtChecked));
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or KeyNotFoundException or
                    FormatException or OverflowException or ArgumentException)
                { Problem(ex.Message,id); }
            }
            // A loader with no captured selection is useful evidence of an incomplete window.
            foreach (var load in events.Where(e => Kind(e) == "deck_loader" && Phase(e) == "enter" && Int(e,"caller") == 0x44c0e8))
                if (!load.TryGetProperty("selectionId",out var v) || v.ValueKind != JsonValueKind.String ||
                    !selectionEvents.Any(e => Text(e,"selectionId") == v.GetString()))
                    Problem("Normal loader without captured selection");
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { Problem("Malformed trace: " + ex.Message); }
        if (results.Count == 0) Problem("No complete normal duel selection");
        return new(results,issues);
    }
}
