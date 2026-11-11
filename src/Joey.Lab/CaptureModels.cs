using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

// Capture types belong to the lab. Native buffer semantics belong to Core.
public sealed record CaptureEntry(int Line, string Kind, long? Sequence, long? CallId,
    int? ThreadId, int? Side, string? Function, string? Phase, uint[]? Arguments,
    NativeAiStats? Stats, NativeAiCombatEvent? CombatEvent, JsonElement Raw);

public sealed record CaptureMetadata(string? Mode, int? FormatVersion, string? ExeSha256,
    long? Seen, long? Retained, long? Overwritten, JsonElement Raw);

public sealed record CaptureDocument(string Source, string Sha256, CaptureMetadata Metadata,
    IReadOnlyList<CaptureEntry> Events, JsonElement? Footer);

public sealed record CaptureIssue(string Severity, int? Line, string Message);
public sealed record SelectorSummary(long? CallId, int? ThreadId, string Function, int? Side,
    int? Pass, long? EnterSequence, long? LeaveSequence, int[]? HandIndices,
    int? SelectedHandIndex, int? SelectedCardIndex, bool Paired);

public sealed record CombatAssessment(long? Sequence, long? CallId, int? ThreadId,
    int? Side, int? AttackerSlotArgument, int? TargetSlotArgument,
    NativeAiCombatEvent Output, string OutputHex, NativeAiStats? AttackerStats,
    NativeAiStats? DefenderStats, long? SelectorCallId, string Pairing,
    string[] UnknownContext);

public sealed record CaptureReport(string Scope, string Source, string Sha256,
    CaptureMetadata Metadata, int EventCount, IReadOnlyDictionary<string, int> CountsByKind,
    IReadOnlyDictionary<string, int> AiCountsByFunctionPhase,
    IReadOnlyDictionary<string, int> CommandsByMaskedOpcode, int TerminalCommandCount,
    int ComputedStatsCount, IReadOnlyList<SelectorSummary> Selectors,
    IReadOnlyList<CombatAssessment> CombatAssessments, IReadOnlyList<CaptureIssue> Issues,
    bool? LiveCaptureComplete)
{
    public bool HasIntegrityErrors => Issues.Any(i => i.Severity == "error");
    public int PairedCombatStats => CombatAssessments.Count(c => c.Pairing == "paired_computed_stats");
}
