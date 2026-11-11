using Joey.Core;

namespace Joey.Lab;

public static class CaptureAnalyzer
{
    private static readonly string[] UnknownCombatContext =
    [
        "Defender position / attack-versus-defense branch not resolved by this importer",
        "Player protection flags and eligible effect links not resolved",
        "Damage suppression predicates not resolved",
        "Initial descriptor bits preserved by mask F005 not resolved"
    ];

    private static bool IsSelector(CaptureEntry e) => e.Kind == "ai_probe" && e.Function is "select_normal" or "select_priority";

    public static CaptureReport Analyze(CaptureDocument capture)
    {
        var issues = new List<CaptureIssue>();
        void Error(CaptureEntry? e, string message) => issues.Add(new("error", e?.Line, message));
        var entries = capture.Events;
        bool live = capture.Metadata.Mode == "live_duels";
        if (capture.Metadata.ExeSha256 is null)
            issues.Add(new("warning", null, "EXE fingerprint absent: importing observations does not validate the executable"));
        else if (!capture.Metadata.ExeSha256.Equals(CpuDeckTraceVerifier.ExeSha256, StringComparison.OrdinalIgnoreCase))
            Error(null, "EXE fingerprint differs from the supported Joey reference");
        if (capture.Metadata.Overwritten is not null && capture.Metadata.Overwritten != 0)
            Error(null, "Metadata reports lost/overwritten events");
        foreach (var count in new[] { capture.Metadata.Seen, capture.Metadata.Retained })
            if (count is not null && count != entries.Count) Error(null, "Metadata event count disagrees with imported records");
        if (!live && (capture.Metadata.Seen is null || capture.Metadata.Retained is null || capture.Metadata.Overwritten is null))
            issues.Add(new("warning", null, "Ring-buffer integrity counters are incomplete"));
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Sequence != i + 1L) Error(e, $"Noncontiguous/missing seq: expected {i + 1}, observed {e.Sequence?.ToString() ?? "null"}");
            foreach (string name in new[] { "readError", "handReadError", "ceNativeReadError", "terminalNativeReadError" })
                if (Fields.HasValue(e.Raw, name)) Error(e, $"Captured {name}; the missing observation is not replaced");
            if (e.Kind == "command" && (e.Arguments is null || e.Arguments.Length == 0)) Error(e, "Command has no arguments");
        }
        var stacks = new Dictionary<int, Stack<CaptureEntry>>();
        var windows = new List<(CaptureEntry Enter, CaptureEntry Leave)>();
        var selectors = new List<SelectorSummary>();
        foreach (var e in entries.Where(e => e.Kind == "ai_probe"))
        {
            if (IsSelector(e))
            {
                if (e.ThreadId is null || e.CallId is null) { Error(e, "Selector lacks threadId/callId"); continue; }
                if (!stacks.TryGetValue(e.ThreadId.Value, out var stack)) stacks[e.ThreadId.Value] = stack = new();
                if (e.Phase == "enter") { stack.Push(e); continue; }
                if (e.Phase != "leave") { Error(e, "Unknown selector phase"); continue; }
                if (stack.Count == 0 || stack.Peek().CallId != e.CallId)
                {
                    Error(e, "Unpaired/cross-thread selector leave");
                    selectors.Add(new(e.CallId, e.ThreadId, e.Function!, e.Side, null, null, e.Sequence, null,
                        Fields.Int(e.Raw, "selectedHandIndex"), null, false));
                    continue;
                }
                var start = stack.Pop();
                if (start.Function != e.Function || start.Side != e.Side) Error(e, "Selector entry/leave identity mismatch");
                windows.Add((start, e));
                int[]? hand = Fields.Ints(start.Raw, "handIndices");
                uint[]? words = Fields.UInts(start.Raw, "handWords");
                if (hand is not null && (hand.Length != Fields.Int(start.Raw, "handCount") ||
                    (words is not null && !hand.SequenceEqual(words.Select(w => (int)(w & 0xfff))))))
                    Error(start, "Inconsistent hand snapshot");
                int? index = Fields.Int(e.Raw, "selectedHandIndex"), selected = null;
                if (index is null) Error(e, "Selector return is missing");
                if (index >= 0 && hand is not null)
                {
                    if (index >= hand.Length) Error(e, "Selector index exceeds captured hand");
                    else
                    {
                        selected = hand[index.Value];
                        if (Fields.Int(e.Raw, "selectedCardIndexAtEntry") != selected) Error(e, "Selected card disagrees with entry hand");
                    }
                }
                selectors.Add(new(e.CallId, e.ThreadId, e.Function!, start.Side, Fields.Int(start.Raw, "pass"),
                    start.Sequence, e.Sequence, hand, index, selected, true));
            }
            else if (Fields.Bool(e.Raw, "withinSelector") == true &&
                (e.ThreadId is null || !stacks.TryGetValue(e.ThreadId.Value, out var stack) || stack.Count == 0))
                Error(e, "Output outside recorded selector window");
        }
        foreach (var start in stacks.Values.SelectMany(s => s))
        {
            Error(start, "Unclosed selector window");
            selectors.Add(new(start.CallId, start.ThreadId, start.Function!, start.Side, Fields.Int(start.Raw, "pass"),
                start.Sequence, null, Fields.Ints(start.Raw, "handIndices"), null, null, false));
        }
        var combat = new List<CombatAssessment>();
        foreach (var e in entries.Where(e => e.CombatEvent is not null))
        {
            var args = e.Arguments;
            bool validArgs = args is { Length: 3 } && args[0] <= 1 && args[1] < 5 && args[2] < 5;
            if (!validArgs) Error(e, "Combat arguments missing or outside side/monster-slot domain");
            var parents = windows.Where(w => w.Enter.ThreadId == e.ThreadId && w.Enter.Line < e.Line && e.Line < w.Leave.Line).ToArray();
            NativeAiStats? own = null, rival = null;
            long? parentId = null;
            string pairing = "no_unambiguous_selector_window";
            if (validArgs && e.CallId is not null && parents.Length == 1)
            {
                var window = parents[0]; parentId = window.Enter.CallId;
                // Same bounded child relation as build_ai_combat_corpus.py.
                var children = entries.Where(c => c.Stats is not null && c.ThreadId == e.ThreadId &&
                    c.CallId > e.CallId && c.Line > window.Enter.Line && c.Line < e.Line).ToArray();
                var attacker = children.Where(c => c.Arguments is { Length: 2 } a && a[0] == args![0] && a[1] == args[1]).ToArray();
                var defender = children.Where(c => c.Arguments is { Length: 2 } a && a[0] == 1 - args![0] && a[1] == args[2]).ToArray();
                if (attacker.Length == 1) own = attacker[0].Stats;
                pairing = children.Length == 2 && attacker.Length == 1 && defender.Length == 1 ? "paired_computed_stats" : "incomplete_computed_stats";
                if (pairing == "paired_computed_stats") rival = defender[0].Stats;
            }
            combat.Add(new(e.Sequence, e.CallId, e.ThreadId, validArgs ? (int)args![0] : null,
                validArgs ? (int)args![1] : null, validArgs ? (int)args![2] : null,
                e.CombatEvent!, Convert.ToHexString(e.CombatEvent!.ToBytes()), own, rival, parentId,
                pairing, [.. UnknownCombatContext,
                    .. own is null ? new[] { "Attacker stats unavailable/ambiguous" } : Array.Empty<string>(),
                    .. rival is null ? new[] { "Defender stats unavailable/ambiguous; no hidden-card estimate substituted" } : Array.Empty<string>()]));
        }
        int terminals = entries.Count(e => e.Kind == "command" && e.Arguments is { Length: > 0 } a && (a[0] & 0x7fff) == 4);
        bool? complete = null;
        if (capture.Footer is { } footer)
        {
            complete = Fields.Bool(footer, "complete");
            if (Fields.Long(footer, "eventsWritten") != entries.Count) Error(null, "Footer eventsWritten disagrees with records");
            if (Fields.Long(footer, "capturedDuels") != terminals) Error(null, "Footer capturedDuels disagrees with terminal commands");
            if (Fields.Long(footer, "overwritten") != 0) Error(null, "Footer overwritten absent or nonzero");
            if (complete != true) issues.Add(new("warning", null, "Live capture is not marked complete"));
        }
        else if (live) issues.Add(new("warning", null, "Live capture lacks final capture_summary; completion is unknown"));
        return new("Offline observations only; combat buffers can describe hypothetical states. No decision replay or legal-action inference.",
            capture.Source, capture.Sha256, capture.Metadata, entries.Count,
            entries.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count()),
            entries.Where(e => e.Kind == "ai_probe").GroupBy(e => $"{e.Function ?? "unknown"}/{e.Phase ?? "unknown"}").ToDictionary(g => g.Key, g => g.Count()),
            entries.Where(e => e.Kind == "command" && e.Arguments is { Length: > 0 }).GroupBy(e => $"0x{e.Arguments![0] & 0x7fff:X4}").ToDictionary(g => g.Key, g => g.Count()),
            terminals, entries.Count(e => e.Stats is not null), selectors.AsReadOnly(), combat.AsReadOnly(), issues.AsReadOnly(), complete);
    }
}
