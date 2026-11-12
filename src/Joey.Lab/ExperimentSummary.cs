namespace Joey.Lab;

public sealed record ExperimentCaptureAnalysis(string Capture, bool Complete, CaptureReport? Summary,
    CombatContextReport? CombatContexts, string? Error);

public sealed record ExperimentOverview(int RequestedDuels, int? ObservedTerminalDuels, int? UnconfirmedDuels,
    int ReadableCaptures, int UnreadableOrMissingCaptures, int Events, int PairedAiSelectors,
    int UnpairedAiSelectors, int CombatAssessments, int CombatContexts,
    IReadOnlyDictionary<string, int> CoreComparison, int CoreEvaluated,
    IReadOnlyDictionary<string, int> ComparisonGaps, IReadOnlyList<string> Issues, string Scope);

public static class ExperimentSummary
{
    public static ExperimentOverview Create(int requested, int workers, IReadOnlyList<ExperimentCaptureAnalysis> captures)
    {
        var readable = captures.Where(c => c.Summary is not null && c.CombatContexts is not null).ToArray();
        int unavailable = Math.Max(0, workers - captures.Count) + captures.Count - readable.Length;
        var issues = new List<string>();
        foreach (var c in captures)
        {
            if (c.Error is not null) issues.Add($"{c.Capture}: {c.Error}");
            if (c.Summary is not null)
                issues.AddRange(c.Summary.Issues.Select(i => $"{c.Capture}: {i.Severity}: {i.Message}"));
            if (c.CombatContexts is not null)
            {
                issues.AddRange(c.CombatContexts.Issues.Select(i => $"{c.Capture}: correlation: {i}"));
                if (c.CombatContexts.UncorrelatedEvents.Count > 0)
                    issues.Add($"{c.Capture}: {c.CombatContexts.UncorrelatedEvents.Count} uncorrelated combat context events");
            }
        }
        if (unavailable > 0) issues.Add($"{unavailable} expected capture(s) missing or unreadable");
        if (captures.Count > workers) issues.Add($"Expected {workers} capture(s), found {captures.Count}");
        // A missing capture makes the total unknown; malformed/inconsistent captures cannot certify a duel count.
        bool countsReliable = unavailable == 0 && captures.Count == workers && readable.All(c => !c.Summary!.HasIntegrityErrors);
        int terminals = readable.Sum(c => c.Summary!.TerminalCommandCount);
        int? observed = countsReliable ? terminals : null;
        int? unconfirmed = observed is int n && n <= requested ? requested - n : null;
        if (observed > requested) issues.Add("Observed terminal command count exceeds requested duels");
        var comparisons = new Dictionary<string, int> { ["Match"] = 0, ["Mismatch"] = 0, ["Incomplete"] = 0, ["Unsupported"] = 0 };
        var gaps = new Dictionary<string, int>();
        void Gap(string reason) => gaps[reason] = gaps.GetValueOrDefault(reason) + 1;
        foreach (var c in readable)
        {
            // The ordinary AI probes lack the state required by the validated Core adapter.
            foreach (var assessment in c.Summary!.CombatAssessments)
            {
                comparisons["Incomplete"]++;
                foreach (var reason in assessment.UnknownContext.Distinct()) Gap(reason);
            }
            // Context events are reported separately: no adapter is yet permitted to evaluate them.
            foreach (var context in c.CombatContexts!.Contexts)
            {
                comparisons[context.Status == "Unsupported" ? "Unsupported" : "Incomplete"]++;
                foreach (var reason in context.MissingOrInvalid.Distinct()) Gap(reason);
            }
        }
        return new(requested, observed, unconfirmed, readable.Length, unavailable,
            readable.Sum(c => c.Summary!.EventCount), readable.Sum(c => c.Summary!.Selectors.Count(s => s.Paired)),
            readable.Sum(c => c.Summary!.Selectors.Count(s => !s.Paired)),
            readable.Sum(c => c.Summary!.CombatAssessments.Count), readable.Sum(c => c.CombatContexts!.Contexts.Count),
            comparisons, 0, gaps, issues, "Terminal commands count observed duels, not winners. Unconfirmed means requested without a reliably counted terminal; it does not prove a duel started. Selector calls are observations, not necessarily completed game actions. Core evaluation is unavailable until a validated complete-context adapter exists; probe assessments and context frames are separate observations and may overlap.");
    }

    public static void Write(ExperimentOverview overview, TextWriter output)
    {
        static string Value(int? n) => n?.ToString() ?? "unknown";
        output.WriteLine($"Duels: {Value(overview.ObservedTerminalDuels)}/{overview.RequestedDuels} terminal commands observed; {Value(overview.UnconfirmedDuels)} unconfirmed");
        output.WriteLine($"Events: {overview.Events}; AI selector calls: {overview.PairedAiSelectors} paired, {overview.UnpairedAiSelectors} unpaired; combat assessments: {overview.CombatAssessments}; combat context frames: {overview.CombatContexts}");
        output.WriteLine($"Core comparisons: Match {overview.CoreComparison["Match"]}, Mismatch {overview.CoreComparison["Mismatch"]}, Incomplete {overview.CoreComparison["Incomplete"]}, Unsupported {overview.CoreComparison["Unsupported"]}; evaluated {overview.CoreEvaluated}");
        if (overview.CoreEvaluated == 0) output.WriteLine("Core comparison unavailable: required observed context/validated adapter is missing.");
        foreach (var gap in overview.ComparisonGaps.OrderByDescending(g => g.Value).ThenBy(g => g.Key).Take(3))
            output.WriteLine($"  Missing/unsupported: {gap.Key} ({gap.Value} observations)");
        if (overview.Issues.Count == 0) output.WriteLine("Integrity/correlation issues: none detected.");
        else foreach (var issue in overview.Issues) output.WriteLine("Integrity/correlation: " + issue);
    }
}
