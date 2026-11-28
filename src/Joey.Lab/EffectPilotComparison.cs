using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Joey.Core;

namespace Joey.Lab;

/// <summary>Offline comparison of reviewed, pinned extracts. Selectors do not infer
/// causal windows, decode unknown memory, or certify a caller's provenance labels.</summary>
public static class EffectPilotComparison
{
    public record FieldResult(string Field, string Status, JsonNode? Expected, JsonNode? Observed, string Reason);
    public record CaseResult(string Id, string Scenario, string EvidenceKind, string Status, List<FieldResult> Fields);
    public record Report(int PilotVersion, string Scope, string RecipeSha256, List<CaseResult> Cases);
    private record Cell(JsonNode? Value, string Origin, string? Problem);
    private record Source(JsonNode[] Rows, string? Problem);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Serialize(Report report) => JsonSerializer.Serialize(report, Options);
    private static string? Text(JsonNode? value) => value?.GetValue<string>();
    private static JsonNode? Clone(JsonNode? value) => value?.DeepClone();
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool HasReadFailure(JsonNode? node)
    {
        if (node is JsonArray array) return array.Any(HasReadFailure);
        if (node is not JsonObject obj) return false;
        foreach (var pair in obj)
        {
            bool errorKey = pair.Key.EndsWith("ReadError", StringComparison.OrdinalIgnoreCase) || pair.Key == "readErrors";
            if (errorKey && pair.Value is not null && pair.Value is not JsonArray { Count: 0 } && pair.Value.ToJsonString() != "false") return true;
            if (HasReadFailure(pair.Value)) return true;
        }
        return false;
    }

    public static Report Compare(string recipePath)
    {
        byte[] recipeBytes = File.ReadAllBytes(recipePath);
        var root = JsonNode.Parse(recipeBytes) as JsonObject ?? throw new InvalidDataException("Expected recipe object");
        if (root["pilotVersion"]?.GetValue<int>() != 1) throw new InvalidDataException("Unsupported pilotVersion");
        string folder = Path.GetDirectoryName(Path.GetFullPath(recipePath))!;
        var sources = new Dictionary<string, Source>(StringComparer.Ordinal);
        foreach (var definition in root["sources"]?.AsArray() ?? throw new InvalidDataException("Missing sources"))
        {
            var s = definition?.AsObject() ?? throw new InvalidDataException("Invalid source");
            string id = Text(s["id"]) ?? throw new InvalidDataException("Missing source id");
            if (sources.ContainsKey(id)) throw new InvalidDataException("Duplicate source id");
            try
            {
                string path = Path.GetFullPath(Path.Combine(folder, Text(s["path"]) ?? throw new InvalidDataException("Missing source path")));
                byte[] bytes = File.ReadAllBytes(path);
                if (Text(s["sha256"]) != Digest(bytes)) throw new InvalidDataException("Source SHA256 absent or different");
                string format = Text(s["format"]) ?? "";
                JsonNode[] rows = format switch
                {
                    "json" => [JsonNode.Parse(bytes) ?? throw new InvalidDataException("Null source")],
                    "jsonl" or "commands" => File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => JsonNode.Parse(l) ?? throw new InvalidDataException("Null record")).ToArray(),
                    _ => throw new InvalidDataException("Unsupported source format")
                };
                if (format == "commands")
                {
                    var capture = CaptureReader.Read(path, Text(s["sha256"]));
                    if (CaptureAnalyzer.Analyze(capture).HasIntegrityErrors || rows.Any(HasReadFailure) ||
                        capture.Metadata.ExeSha256 != Experiment.JoeyHash || capture.Metadata.Overwritten != 0 ||
                        capture.Metadata.Seen != capture.Events.Count || capture.Metadata.Retained != capture.Events.Count)
                        throw new InvalidDataException("Command trace fingerprint/integrity not attested");
                }
                sources.Add(id, new(rows, null));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
            { sources.Add(id, new([], ex is IOException or UnauthorizedAccessException ? "Source file unavailable" : ex.Message)); }
        }
        var cases = new List<CaseResult>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root["cases"]?.AsArray() ?? throw new InvalidDataException("Missing cases"))
        {
            var c = item?.AsObject() ?? throw new InvalidDataException("Invalid case");
            string id = Text(c["id"]) ?? throw new InvalidDataException("Missing case id");
            string scenario = Text(c["scenario"]) ?? "";
            string kind = Text(c["evidenceKind"]) ?? "";
            if (!ids.Add(id) || scenario is not ("Sparks" or "LegendarySword") || kind is not ("Synthetic" or "Native"))
                throw new InvalidDataException("Duplicate case id or unsupported scenario/evidenceKind");
            Cell Read(JsonNode? spec)
            {
                if (spec is null) return new(null, "Missing", "No observation supplied");
                try
                {
                    string origin = Text(spec["origin"]) ?? "Missing";
                    if (origin == "Assumed") return new(Clone(spec["value"]), origin, "Sandbox/reviewer assumption: " + Text(spec["reason"]));
                    if (origin != "Observed") return new(null, origin, "Unknown/missing provenance");
                    string sourceId = Text(spec["source"]) ?? "";
                    if (!sources.TryGetValue(sourceId, out var source)) return new(null, origin, "Unknown source");
                    if (source.Problem is not null) return new(null, origin, source.Problem);
                    if (kind == "Native" && source.Rows.Any(r => Text(r["dataOrigin"]) == "Synthetic"))
                        return new(null, origin, "Synthetic source cannot attest native observations");
                    int line = spec["line"]?.GetValue<int>() ?? 1;
                    if (line < 1 || line > source.Rows.Length) return new(null, origin, "Source record absent");
                    JsonNode? node = source.Rows[line-1];
                    string pointer = Text(spec["pointer"]) ?? throw new InvalidDataException("Missing JSON pointer");
                    if (pointer != "" && !pointer.StartsWith('/')) throw new InvalidDataException("Invalid JSON pointer");
                    foreach (string raw in pointer.Split('/').Skip(1))
                    {
                        string part = raw.Replace("~1", "/").Replace("~0", "~");
                        if (node is JsonArray array)
                            node = int.TryParse(part, out int n) && n >= 0 && n < array.Count ? array[n] : null;
                        else node = (node as JsonObject)?[part];
                    }
                    return new(Clone(node), origin, node is null ? "Selected observation is null/absent" : null);
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or ArgumentException or InvalidDataException)
                { return new(null, "Missing", ex.Message); }
            }
            var inputs = c["inputs"]?.AsObject() ?? new JsonObject();
            Cell Input(string name) => Read(inputs[name]);
            int I(string name) => Input(name).Value?.GetValue<int>() ?? throw new InvalidDataException("Missing " + name);
            bool B(string name) => Input(name).Value?.GetValue<bool>() ?? throw new InvalidDataException("Missing " + name);
            byte Byte(string name) => checked((byte)I(name));
            ushort Word(string name) => checked((ushort)I(name));
            void Bound(bool valid) { if (!valid) throw new InvalidDataException("Inputs outside bounded scenario"); }
            void Drain(ActionQueue queue)
            {
                int cursor = queue.Trace.Count;
                for (int tick = 0; tick < 128 && queue.IsBusy; tick++) queue.Tick();
                Bound(!queue.IsBusy && queue.Fault is null &&
                    !queue.Trace.Skip(cursor).Any(entry => entry.Result?.Outcome == CommandOutcome.Rejected));
            }
            void Identity() => Bound(I("sourceResourceId") == (scenario == "Sparks" ? 342 : 300) &&
                I("sourceInternalIndex") == (scenario == "Sparks" ? 755 : 614) && I("sourcePlayer") is >= 0 and <= 1);
            var fields = new List<FieldResult>();
            void CompareField(string name, string[] dependencies, Func<JsonNode?> predict)
            {
                Cell actual = Read(c["observed"]?[name]);
                var problems = new List<string>();
                foreach (string dependency in dependencies.Concat(["sourceResourceId","sourceInternalIndex","sourcePlayer","exeSha256","windowIsolated"]).Distinct())
                {
                    var cell = Input(dependency);
                    if (cell.Origin != "Observed" || cell.Problem is not null || cell.Value is null)
                        problems.Add(dependency + ": " + (cell.Problem ?? cell.Origin));
                }
                if (actual.Origin != "Observed" || actual.Problem is not null || actual.Value is null)
                    problems.Add(name + ": " + (actual.Problem ?? actual.Origin));
                JsonNode? expected = null;
                try
                {
                    Identity();
                    Bound(Text(Input("exeSha256").Value) == Experiment.JoeyHash && B("windowIsolated"));
                    expected = predict();
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or FormatException)
                { problems.Add(ex.Message); }
                string status = problems.Count > 0 || expected is null ? "Inconclusive" : JsonNode.DeepEquals(expected, actual.Value) ? "Match" : "Mismatch";
                fields.Add(new(name, status, expected, actual.Value, problems.Count > 0 ? string.Join("; ", problems) : "Scalar comparison within reviewed window; no coverage promotion"));
            }
            if (scenario == "Sparks")
            {
                DirectDamageCommand Command()
                {
                    var result = DirectDamageRules.Resolve(342, Byte("requestFlags"), Byte("preparedTarget"));
                    return result.Command ?? throw new InvalidDataException("Sparks did not produce a modeled command");
                }
                DemoDuel SparksDemo()
                {
                    Bound(B("sandboxApplicable") && I("requestFlags") == 0 && I("preparedTarget") == DirectDamageRules.PrepareTarget((byte)I("sourcePlayer")));
                    Bound(I("lifeBefore0") is > 0 and <= 65535 && I("lifeBefore1") is > 0 and <= 65535);
                    var d = DemoDuel.FromPartialObservation(I("sourcePlayer"), [new Card(755,342,"Sparks","",22u<<20)],new Card?[5],new Card?[5],8000);
                    d.Life[0]=I("lifeBefore0"); d.Life[1]=I("lifeBefore1");
                    var queue = d.Actions;
                    Bound(d.TryQueueActivation(0, false, out _, ActionOrigin.Test));
                    Drain(queue);
                    return d;
                }
                CompareField("preparedTarget", [], () => JsonValue.Create(DirectDamageRules.PrepareTarget((byte)I("sourcePlayer"))));
                CompareField("commandTarget", ["requestFlags","preparedTarget"], () => JsonValue.Create(Command().TargetPlayer));
                CompareField("damageAmount", ["requestFlags","preparedTarget"], () => JsonValue.Create(Command().Amount));
                string[] demo = ["requestFlags","preparedTarget","lifeBefore0","lifeBefore1","sandboxApplicable"];
                CompareField("activationAccepted", demo, () => JsonValue.Create(SparksDemo().Hand.Count==0));
                CompareField("lifeAfter0", demo, () => JsonValue.Create(SparksDemo().Life[0]));
                CompareField("lifeAfter1", demo, () => JsonValue.Create(SparksDemo().Life[1]));
                CompareField("sourceInHandAfter", demo, () => JsonValue.Create(SparksDemo().Hand.Any(x=>x.Id==342)));
                CompareField("sourceInGraveyardAfter", demo, () => JsonValue.Create(SparksDemo().Graveyard(I("sourcePlayer")).Any(x=>x.Id==342)));
            }
            else
            {
                EquipmentTargetContext Target() => new(I("targetPlayer"),I("targetZone"),I("targetInternalIndex"),Word("targetResourceId"),B("faceUp"),Byte("dynamicType"),Byte("dynamicAttribute"),B("genericGuardsSatisfied"));
                NativeAiStats Stats()
                {
                    Bound(B("noOtherModifiers"));
                    Bound(I("attackBefore")>=0 && I("defenseBefore")>=0);
                    var before = new NativeAiStats(Word("targetInternalIndex"),Byte("dynamicType"),Byte("dynamicAttribute"),I("attackBefore"),I("defenseBefore"));
                    var delta = NativeAiAttributes.EquipmentDelta(300,before,Word("targetResourceId"));
                    Bound(EquipmentRules.Eligibility(300,Target()).Value);
                    return NativeAiAttributes.ApplyResolvedTail(before,delta.Value,0,0);
                }
                DemoDuel SwordDemo(bool remove)
                {
                    Bound(B("sandboxApplicable") && B("noOtherModifiers"));
                    Bound(EquipmentRules.Eligibility(300,Target()).Value);
                    var sides = new[] { new Card?[5],new Card?[5] };
                    sides[I("targetPlayer")][I("targetZone")]=new Card(I("targetInternalIndex"),Word("targetResourceId"),"Projected target","",((uint)I("dynamicType")<<20)|((uint)I("dynamicAttribute")<<29));
                    var d=DemoDuel.FromPartialObservation(I("sourcePlayer"),[new Card(614,300,"Legendary Sword","",22u<<20)],sides[0],sides[1],8000,equipmentContextResolved:true);
                    var queue = d.Actions;
                    Bound(d.TryQueueEquipment(DuelZone.Hand, 0, I("targetPlayer"), I("targetZone"), out _, ActionOrigin.Test));
                    Drain(queue);
                    if (remove)
                    {
                        Bound(B("equipmentRemovalConfirmed"));
                        Bound(d.TryCaptureCardTarget(I("sourcePlayer"), DuelZone.SpellTrap, 0, out var target));
                        // The reviewed comparison checks only movement/link state.
                        // Destruction is the supported typed linked removal; it is
                        // not evidence of the native removal reason.
                        queue.Enqueue(new DestroyCommand(target.Side, target), ActionOrigin.Test);
                        Drain(queue);
                    }
                    return d;
                }
                string[] target=["targetPlayer","targetZone","targetInternalIndex","targetResourceId","faceUp","dynamicType","dynamicAttribute","genericGuardsSatisfied"];
                CompareField("targetEligible",target,()=>JsonValue.Create(EquipmentRules.Eligibility(300,Target()).Value));
                string[] stats=[..target,"attackBefore","defenseBefore","noOtherModifiers"];
                CompareField("attackAfter",stats,()=>JsonValue.Create(Stats().Attack));
                CompareField("defenseAfter",stats,()=>JsonValue.Create(Stats().Defense));
                string[] link=[..target,"sandboxApplicable","noOtherModifiers"];
                CompareField("activationAccepted",link,()=>JsonValue.Create(SwordDemo(false).Hand.Count==0));
                CompareField("linkCountAfter",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Count));
                CompareField("linkOwner",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Single().Owner));
                CompareField("linkSourceZone",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Single().SourceZone));
                CompareField("linkTargetPlayer",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Single().TargetPlayer));
                CompareField("linkTargetZone",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Single().TargetZone));
                CompareField("equipmentPresentAfter",link,()=>JsonValue.Create(SwordDemo(false).EquipmentCards.Single().Card.Id==300));
                string[] removal=[..link,"equipmentRemovalConfirmed"];
                CompareField("linkCountAfterRemoval",removal,()=>JsonValue.Create(SwordDemo(true).EquipmentCards.Count));
                CompareField("equipmentPresentAfterRemoval",removal,()=>JsonValue.Create(SwordDemo(true).EquipmentCards.Count!=0));
                CompareField("equipmentInGraveyardAfterRemoval",removal,()=>JsonValue.Create(SwordDemo(true).Graveyard(I("sourcePlayer")).Any(x=>x.Id==300)));
            }
            string overall=fields.Any(f=>f.Status=="Mismatch") ? "Mismatch" : fields.Any(f=>f.Status=="Inconclusive") ? "Inconclusive" : "Match";
            cases.Add(new(id,scenario,kind,overall,fields));
        }
        if (cases.Count==0) throw new InvalidDataException("No pilot cases");
        return new(1,"Pinned reviewed extracts only. Synthetic cases are infrastructure tests. Native selectors require human audit of timing, correlation and provenance; no coverage update.",Digest(recipeBytes),cases);
    }
}
