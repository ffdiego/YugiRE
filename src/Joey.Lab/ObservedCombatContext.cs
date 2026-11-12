using System.Buffers.Binary;
using System.Text.Json;

namespace Joey.Lab;

// Values are observations at their recorded instant, not complete engine state.
public sealed record ObservedSlot(int? Side, int? Slot, uint? IdentityRaw, int? InternalCardIndex,
    int? ResourceId, int? PositionRaw, bool? DefensePosition, bool? FaceUp,
    uint? MonsterFlagsRaw, uint? PlayerFlagsRaw, int? LifePoints, JsonElement Raw);
public sealed record CombatProvenance(long? Session, int? ThreadId, long? CombatCallId, long? SelectorCallId,
    long? EnterSequence, long? LeaveSequence, long? EnterTimestampMs, long? LeaveTimestampMs,
    string? StateKind, string? CaptureSha256, string? ExeSha256);
// A wrapper output may be a hidden-card estimate; this is not a real slot identity.
public sealed record ObservedStats(ushort HeaderIndex, byte TypeCode, byte AttributeCode, int Attack, int Defense);
public sealed record ObservedService(long? Sequence, long? TimestampMs, long? CallId, string? Function,
    string? FunctionVa, string? Caller, uint[]? ArgsRaw, string? OutputHex, uint? ReturnRaw,
    int? ReturnSigned, int? EffectiveResourceId, ObservedStats? StatsOutput, JsonElement Raw);
public sealed record ObservedCombatContext(int? SchemaVersion, CombatProvenance Provenance,
    uint[]? ArgsRaw, ObservedSlot? Attacker, ObservedSlot? Defender, string? SeedHex, string? OutputHex,
    IReadOnlyList<ObservedService> Services, string Status, IReadOnlyList<string> MissingOrInvalid,
    JsonElement? EnterRaw, JsonElement? LeaveRaw);
public sealed record CombatContextReport(string Scope, string Source, string CaptureSha256, string? ExeSha256,
    IReadOnlyList<ObservedCombatContext> Contexts, IReadOnlyList<string> Issues, IReadOnlyList<JsonElement> UncorrelatedEvents);

public static class CombatContextReader
{
    private sealed class Frame(CaptureEntry entry)
    {
        public CaptureEntry Enter = entry;
        public CaptureEntry? Leave;
        public List<ObservedService> Services = [];
        public List<string> Problems = [];
    }
    private static JsonElement? Prop(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;
    private static string? Str(JsonElement e, string key) => Prop(e, key) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;
    private static long? Long(JsonElement e, string key) => Prop(e, key) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out long n) ? n : null;
    private static int? Int(JsonElement e, string key) => Prop(e, key) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out int n) ? n : null;
    private static uint? Uint(JsonElement e, string key) => Prop(e, key) is { ValueKind: JsonValueKind.Number } v && v.TryGetUInt32(out uint n) ? n : null;
    private static bool? Bool(JsonElement e, string key) => Prop(e, key) is { ValueKind: JsonValueKind.True } ? true : Prop(e, key) is { ValueKind: JsonValueKind.False } ? false : null;
    private static uint[]? Args(JsonElement e) { var v = Prop(e, "argsRaw"); if (v is not { ValueKind: JsonValueKind.Array }) return null; var a = new List<uint>(); foreach (var x in v.Value.EnumerateArray()) { if (x.ValueKind != JsonValueKind.Number || !x.TryGetUInt32(out var n)) return null; a.Add(n); } return a.ToArray(); }
    private static ObservedSlot? Slot(JsonElement e, string key)
    {
        if (Prop(e, key) is not { ValueKind: JsonValueKind.Object } v) return null;
        return new(Int(v,"side"), Int(v,"slot"), Uint(v,"identityRaw"), Int(v,"internalCardIndex"),
            Int(v,"resourceId"), Int(v,"positionRaw"), Bool(v,"defensePosition"), Bool(v,"faceUp"),
            Uint(v,"monsterFlagsRaw"), Uint(v,"playerFlagsRaw"), Int(v,"lifePoints"), v);
    }
    private static ObservedStats? Stats(CaptureEntry e)
    {
        string? hex = Str(e.Raw,"outputHex");
        if (e.Function is not ("own_stats" or "opponent_stats") || !Hex(hex, 12)) return null;
        byte[] bytes = Convert.FromHexString(hex!);
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes), bytes[2], bytes[3], BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
    }
    private static ObservedService Service(CaptureEntry e) => new(e.Sequence, Long(e.Raw,"timestampMs"), e.CallId, e.Function,
        Str(e.Raw,"functionVa"), Str(e.Raw,"caller"), Args(e.Raw), Str(e.Raw,"outputHex"), Uint(e.Raw,"returnRaw"),
        Int(e.Raw,"returnSigned"), Int(e.Raw,"resourceId"), Stats(e), e.Raw);
    private static bool Hex(string? text, int bytes) => text is not null && text.Length == bytes * 2 && text.All(Uri.IsHexDigit);
    private static bool Errors(JsonElement e) => e.EnumerateObject().Any(p => (p.Name.Contains("error", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("overflow", StringComparison.OrdinalIgnoreCase)) && p.Value.ValueKind switch { JsonValueKind.Null or JsonValueKind.False => false, JsonValueKind.Array => p.Value.GetArrayLength() > 0, _ => true });

    public static CombatContextReport Read(CaptureDocument capture)
    {
        var issues = new List<string>(); var uncorrelated = new List<JsonElement>(); var frames = new List<Frame>();
        var stacks = new Dictionary<int, Stack<Frame>>(); var selectors = new Dictionary<int, Stack<long>>();
        var seen = new HashSet<(long?, int, long)>();
        var captureIssues = CaptureAnalyzer.Analyze(capture).Issues.Where(i => i.Severity == "error").Select(i => i.Message).ToArray();
        issues.AddRange(captureIssues);
        foreach (var e in capture.Events)
        {
            if (e.Kind == "ai_probe" && e.Function is "select_normal" or "select_priority" && e.ThreadId is int st && e.CallId is long sc)
            {
                if (!selectors.TryGetValue(st, out var ss)) selectors[st] = ss = new();
                if (e.Phase == "enter") ss.Push(sc);
                else if (e.Phase == "leave" && ss.Count > 0 && ss.Peek() == sc) ss.Pop();
            }
            if (e.Kind != "combat_context") continue;
            var raw = e.Raw; int? thread = e.ThreadId; long? combat = Long(raw,"combatCallId"), session = Long(raw,"session"), selector = Long(raw,"selectorCallId");
            if (thread is null || combat is null) { issues.Add($"seq {e.Sequence}: missing threadId/combatCallId"); uncorrelated.Add(raw); continue; }
            if (!stacks.TryGetValue(thread.Value, out var stack)) stacks[thread.Value] = stack = new();
            if (e.Function == "combat_event" && e.Phase == "enter")
            {
                var f = new Frame(e); frames.Add(f); stack.Push(f);
                if (!seen.Add((session,thread.Value,combat.Value))) f.Problems.Add("duplicate combat identity");
                if (e.CallId != combat) f.Problems.Add("entry callId differs from combatCallId");
                if (selector is null || !selectors.TryGetValue(thread.Value, out var ss) || ss.Count == 0 || ss.Peek() != selector) f.Problems.Add("selector parent missing or mismatched");
                if (session is null || session != Long(capture.Metadata.Raw,"session")) f.Problems.Add("session missing or mismatched");
                continue;
            }
            if (stack.Count == 0 || Long(stack.Peek().Enter.Raw,"combatCallId") != combat || Long(stack.Peek().Enter.Raw,"session") != session)
            { issues.Add($"seq {e.Sequence}: orphan or cross-thread combat record"); uncorrelated.Add(raw); continue; }
            var frame = stack.Peek();
            if (selector != Long(frame.Enter.Raw,"selectorCallId")) frame.Problems.Add("child selector mismatch");
            if (Int(raw,"schemaVersion") != Int(frame.Enter.Raw,"schemaVersion")) frame.Problems.Add("child schema mismatch");
            if (Errors(raw)) frame.Problems.Add($"seq {e.Sequence}: read error or overflow");
            if (e.Function == "combat_event" && e.Phase == "leave")
            {
                frame.Leave = e; stack.Pop();
                if (e.CallId != frame.Enter.CallId) frame.Problems.Add("leave callId mismatch");
                if (Int(raw,"childCount") != frame.Services.Count) frame.Problems.Add("child count missing or mismatched");
            }
            else { if (e.Phase != "leave") frame.Problems.Add("unexpected service phase"); frame.Services.Add(Service(e)); }
        }
        var contexts = new List<ObservedCombatContext>();
        foreach (var frame in frames)
        {
            var e = frame.Enter; var raw = e.Raw; var leave = frame.Leave; var args = Args(raw); var attacker = Slot(raw,"attacker"); var defender = Slot(raw,"defender");
            if (Errors(raw)) frame.Problems.Add("entry read error or overflow");
            if (leave is null) frame.Problems.Add("missing combat leave");
            if (args is not { Length: 3 } || args[0] > 1 || args[1] >= 5 || args[2] >= 5) frame.Problems.Add("missing or invalid side/slots");
            if (attacker is null || defender is null) frame.Problems.Add("missing slot snapshot");
            void ValidateSlot(ObservedSlot? slot, string role, uint? side, uint? index)
            {
                if (slot is null) return;
                if (slot.Side is null || slot.Slot is null || slot.IdentityRaw is null || slot.InternalCardIndex is null ||
                    slot.PositionRaw is null || slot.DefensePosition is null || slot.FaceUp is null || slot.MonsterFlagsRaw is null || slot.PlayerFlagsRaw is null)
                    frame.Problems.Add($"{role} snapshot fields missing");
                if ((side is not null && slot.Side != side) || (index is not null && slot.Slot != index) ||
                    (slot.IdentityRaw is uint identity && slot.InternalCardIndex is int card && (identity & 0xfff) != card) ||
                    (slot.PositionRaw is int position && slot.DefensePosition is bool defense && defense != ((position & 1) != 0)) ||
                    (slot.PositionRaw is int visibility && slot.FaceUp is bool faceUp && faceUp != ((visibility & 2) != 0)))
                    frame.Problems.Add($"{role} snapshot inconsistent");
            }
            bool validArgs = args is { Length: 3 } && args[0] <= 1 && args[1] < 5 && args[2] < 5;
            ValidateSlot(attacker, "attacker", validArgs ? args![0] : null, validArgs ? args![1] : null);
            ValidateSlot(defender, "defender", validArgs ? 1 - args![0] : null, validArgs ? args![2] : null);
            if (frame.Services.Count(s => s.Function == "own_stats" && Hex(s.OutputHex, 12)) != 1 || frame.Services.Count(s => s.Function == "opponent_stats" && Hex(s.OutputHex, 12)) != 1)
                frame.Problems.Add("stats outputs missing, malformed or ambiguous");
            if (!Hex(Str(raw,"seedHex"),4)) frame.Problems.Add("missing or invalid descriptor seed");
            if (!Hex(leave is null ? null : Str(leave.Raw,"outputHex"),4)) frame.Problems.Add("missing or invalid descriptor output");
            if (captureIssues.Length > 0) frame.Problems.Add("capture integrity errors");
            // S3: entry flags are not observed at their later native consumption; no independent attributes/links.
            frame.Problems.Add("player flags at consumption not observed");
            frame.Problems.Add("independent attributes and effect links not established");
            if (capture.Metadata.ExeSha256 is null) frame.Problems.Add("capture executable attestation absent");
            int? version = Int(raw,"schemaVersion");
            contexts.Add(new(version, new(Long(raw,"session"),e.ThreadId,Long(raw,"combatCallId"),Long(raw,"selectorCallId"),e.Sequence,leave?.Sequence,Long(raw,"timestampMs"),leave is null ? null : Long(leave.Raw,"timestampMs"),Str(raw,"stateKind"),capture.Sha256,capture.Metadata.ExeSha256),
                args,attacker,defender,Str(raw,"seedHex"),leave is null ? null : Str(leave.Raw,"outputHex"),frame.Services.AsReadOnly(),version == 1 ? "Incomplete" : "Unsupported",frame.Problems.AsReadOnly(),raw,leave?.Raw));
        }
        return new("Observed native records only; no Core evaluation or claim of complete state.",capture.Source,capture.Sha256,capture.Metadata.ExeSha256,contexts.AsReadOnly(),issues.AsReadOnly(),uncorrelated.AsReadOnly());
    }
}
