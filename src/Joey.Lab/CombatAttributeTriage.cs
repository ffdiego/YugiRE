using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

// V3 selection is a local snapshot filter, NOT a manifest of absent modifiers.
// Native wrapper buffers are used only as expected results after the Core
// independently reconstructs attributes from the pinned DAT and an explicitly
// hypothetical empty modifier context.
public sealed record AttributeTriageParticipant(
    int InternalCardIndex, bool FaceUp, int? ResourceId, string? CardName,
    string Status, string? ConditionalCoreHex, string? ObservedNativeHex,
    IReadOnlyList<string> Reasons);

public sealed record CombatAttributeTriageCase(
    long CombatCallId, long? EnterSequence, long? SelectorCallId,
    bool NeutralLocalSnapshot, int AttackerIndex, int DefenderIndex,
    bool DefenderFaceUp, AttributeTriageParticipant Attacker,
    AttributeTriageParticipant Defender);

public sealed record CombatAttributeTriageReport(
    string Scope, string SourceCapture, string CaptureSha256, string? ExeSha256,
    bool PinnedCatalogProvided, int TotalFrames, int NeutralCandidates,
    int HiddenDefendersInNeutral, int NeutralVisibleDefenders,
    int ConditionalMatches, int ConditionalMismatches, int ValidatedIndependentContexts,
    IReadOnlyList<CombatAttributeTriageCase> Cases);

public static class CombatAttributeTriage
{
    private static readonly string PinnedExeSha256 = CpuDeckTraceVerifier.ExeSha256;

    private static uint RequiredU32(JsonElement raw, string name)
    {
        if (!raw.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint result))
            throw new InvalidDataException($"Missing/invalid v3 slot field: {name}");
        return result;
    }

    private static bool Neutral(ObservedSlot slot) =>
        RequiredU32(slot.Raw, "linkCountRaw") == 0 &&
        RequiredU32(slot.Raw, "monsterFlagsRaw") == 0 &&
        RequiredU32(slot.Raw, "playerFlagsRaw") == 0;

    private static bool IsIntegrityProblem(ObservedCombatContext frame) =>
        frame.MissingOrInvalid.Any(reason => reason is not
            ("player flags at consumption not observed" or
             "independent attributes and effect links not established"));

    private static void RequireWrapper(ObservedCombatContext frame, ObservedService service, string function)
    {
        if (service.Function != function || service.StatsOutput is null ||
            service.ArgsRaw is not { Length: 3 })
            throw new InvalidDataException($"Incomplete {function} output at call {frame.Provenance.CombatCallId}");
        var raw = service.Raw;
        int? esiIn = Fields.Int(raw, "esiAtEnterRaw");
        int? esiOut = Fields.Int(raw, "esiAtLeaveRaw");
        int? esiExpected = Fields.Int(raw, "esiExpectedRaw");
        int? espIn = Fields.Int(raw, "espAtEnterRaw");
        int? espOut = Fields.Int(raw, "espAtLeaveRaw");
        if (Fields.Bool(raw, "esiPreserved") != true ||
            esiIn is null || esiOut is null || esiExpected is null ||
            espIn is null || espOut is null ||
            esiIn != esiOut || esiIn != esiExpected || espIn != espOut)
            throw new InvalidDataException($"Unverified wrapper ESI/ESP at call {frame.Provenance.CombatCallId}");
        if (Fields.String(raw, "provenance") != "wrapper-boundary-snapshot")
            throw new InvalidDataException("Wrapper provenance mismatch");
    }

    private static AttributeTriageParticipant Participant(ObservedSlot slot, ObservedService service,
        bool neutral, bool opponent, IReadOnlyDictionary<int, Card>? catalog)
    {
        int index = slot.InternalCardIndex ??
            throw new InvalidDataException("Missing internal card index");
        var reasons = new List<string>();
        string? observed = service.OutputHex;
        string? name = null;
        int? resource = null;
        if (catalog is not null && catalog.TryGetValue(index, out var known))
        {
            name = known.Name;
            resource = known.Id;
        }
        if (!neutral)
            return new(index, slot.FaceUp == true, resource, name, "NonNeutralLocalSnapshot", null,
                observed, ["Nonzero local link or flags; eligibility not investigated"]);
        if (opponent && slot.FaceUp != true)
            return new(index, false, resource, name, "HiddenOpponentEstimate", null,
                observed, ["ADDC0 hides identity/attributes: buffer is heuristic, not real stats"]);
        if (catalog is null)
            return new(index, slot.FaceUp == true, null, null, "CatalogUnavailable", null,
                observed, ["Pinned data.dat was not supplied"]);
        if (!catalog.TryGetValue(index, out var card))
            return new(index, slot.FaceUp == true, null, null, "CatalogIndexMissing", null,
                observed, ["Internal index not found in pinned card catalog"]);
        var calculated = NativeAiAttributes.FromPlainCard(card, NativeAiAttributeContext.Resolved());
        if (!calculated.IsSupported)
            return new(index, slot.FaceUp == true, card.Id, card.Name, "UnsupportedIntrinsicPolicy",
                null, observed, calculated.UnsupportedReasons.ToArray());

        string expected = Convert.ToHexString(calculated.Value.ToBytes());
        bool same = string.Equals(expected, observed, StringComparison.OrdinalIgnoreCase);
        reasons.Add("Hypothetical empty modifier manifest only; not observed/resolved");
        reasons.Add("No consumed flags, field effects, suppression or complete link eligibility proof");
        return new(index, slot.FaceUp == true, card.Id, card.Name,
            same ? "ConditionalMatch" : "ConditionalMismatch", expected, observed,
            reasons.AsReadOnly());
    }

    public static CombatAttributeTriageReport Analyze(CaptureDocument capture, IReadOnlyList<Card>? cards = null)
    {
        if (!string.Equals(capture.Metadata.ExeSha256, PinnedExeSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Capture lacks the pinned executable attestation");
        if (capture.Metadata.Seen != capture.Events.Count ||
            capture.Metadata.Retained != capture.Events.Count ||
            capture.Metadata.Overwritten != 0)
            throw new InvalidDataException("Capture metadata counters are missing, inconsistent or lossy");
        var captureReport = CaptureAnalyzer.Analyze(capture);
        if (captureReport.HasIntegrityErrors)
            throw new InvalidDataException("Capture analyzer reported integrity errors");
        var contexts = CombatContextReader.Read(capture);
        if (contexts.Issues.Count != 0 || contexts.UncorrelatedEvents.Count != 0)
            throw new InvalidDataException("Uncorrelated or orphan F40 observations");
        var catalog = cards?.ToDictionary(card => card.Index);
        var cases = new List<CombatAttributeTriageCase>();
        foreach (var frame in contexts.Contexts)
        {
            if (frame.SchemaVersion != 3 ||
                frame.Provenance.StateKind != "selector-window" ||
                frame.Provenance.EnterSequence is null ||
                frame.Provenance.LeaveSequence is null ||
                frame.Provenance.CombatCallId is null ||
                frame.Provenance.SelectorCallId is null ||
                frame.Attacker is null || frame.Defender is null ||
                IsIntegrityProblem(frame))
                throw new InvalidDataException("Incomplete or unsupported combat v3 frame");
            if (frame.EnterRaw is not { } enter ||
                Fields.String(enter,"provenance") != "F40-entry-snapshot" ||
                Fields.HasValue(enter,"consumedFlagsRaw") ||
                Fields.HasValue(enter,"consumedPositionRaw") ||
                Fields.HasValue(enter,"consumedDescriptorSeedRaw"))
                throw new InvalidDataException("v3 provenance or unknown-consumption contract failed");
            if (frame.Services.Count != 2 ||
                frame.Services[0].Function != "own_stats" ||
                frame.Services[1].Function != "opponent_stats")
                throw new InvalidDataException("Expected one ordered ADD60 and ADDC0 wrapper per F40");
            var own = frame.Services[0]; var rival = frame.Services[1];
            RequireWrapper(frame, own, "own_stats");
            RequireWrapper(frame, rival, "opponent_stats");
            var args = frame.ArgsRaw;
            if (args is not { Length: 3 } || args[0] > 1 ||
                own.ArgsRaw![0] != args[0] || own.ArgsRaw[1] != args[1] ||
                rival.ArgsRaw![0] != 1u - args[0] || rival.ArgsRaw[1] != args[2] ||
                Fields.Int(own.Raw, "esiExpectedRaw") != (int)((1u - args[0]) * 0xD44) ||
                Fields.Int(rival.Raw, "esiExpectedRaw") != (int)((1u - args[0]) * 0xD44))
                throw new InvalidDataException("Wrapper ABI, side/slot or ESI offset differs from F40 frame");
            bool neutral = Neutral(frame.Attacker) && Neutral(frame.Defender);
            int attackerIndex = frame.Attacker.InternalCardIndex!.Value;
            int defenderIndex = frame.Defender.InternalCardIndex!.Value;
            var first = Participant(frame.Attacker, own, neutral, false, catalog);
            var second = Participant(frame.Defender, rival, neutral, true, catalog);
            cases.Add(new(frame.Provenance.CombatCallId.Value, frame.Provenance.EnterSequence,
                frame.Provenance.SelectorCallId, neutral, attackerIndex, defenderIndex,
                frame.Defender.FaceUp == true, first, second));
        }
        int count = cases.Count(c => c.NeutralLocalSnapshot);
        var participants = cases.SelectMany(c => new[] { c.Attacker, c.Defender });
        return new(
            "V3 snapshot triage only. Conditional matches assume an empty modifier manifest; " +
            "no native stats feed the Core calculation. Independent complete F40 contexts: zero.",
            capture.Source, capture.Sha256, capture.Metadata.ExeSha256, catalog is not null,
            cases.Count, count, cases.Count(c => c.NeutralLocalSnapshot && !c.DefenderFaceUp),
            cases.Count(c => c.NeutralLocalSnapshot && c.DefenderFaceUp),
            participants.Count(p => p.Status == "ConditionalMatch"),
            participants.Count(p => p.Status == "ConditionalMismatch"),
            0, cases.AsReadOnly());
    }
}
