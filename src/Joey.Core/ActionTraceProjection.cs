namespace Joey.Core;

/// <summary>Comparable four-word representation, not a native binary log.
/// Unmapped and unsuccessful commands remain visible, with their sequence.</summary>
public sealed record ProjectedAction(long Sequence, string Command, string Origin, string Outcome,
    int Side, string Mapping, ushort[]? Args, string Detail);

public static class ActionTraceProjection
{
    public static IReadOnlyList<ProjectedAction> ReadExecuted(IEnumerable<ActionTraceEntry> trace)
    {
        var rows = new List<ProjectedAction>();
        long previous = 0;
        foreach (var entry in trace.Where(e => e.Stage == ActionTraceStage.Executed))
        {
            var e = entry.Entry;
            var result = entry.Result ?? throw new InvalidOperationException("Executed command has no result.");
            if (e.Sequence <= previous) throw new InvalidOperationException("Executed sequence must increase.");
            previous = e.Sequence;
            var command = e.Command;
            ushort[]? words = null;
            string mapping = result.Outcome == CommandOutcome.Applied ? "unmapped" : "non_applied";
            if (result.Outcome == CommandOutcome.Applied && command.Side is 0 or 1)
            {
                ushort side = (ushort)(command.Side << 15);
                // Ordinary successful transfer. Empty-deck result is a terminal
                // operation, not a successful draw. Do not infer a native word.
                words = command switch
                {
                    DrawCommand when result.Detail.Length == 0 => [(ushort)(side | 0x61), 1, 1, 0],
                    DamageCommand d when d.Amount is > 0 and <= ushort.MaxValue => [(ushort)(side | 0x43), (ushort)d.Amount, 0, 1],
                    HealCommand h when h.Amount is > 0 and <= ushort.MaxValue => [(ushort)(side | 0x42), (ushort)h.Amount, 0, 1],
                    _ => null
                };
                if (words is not null) mapping = "mapped";
            }
            rows.Add(new(e.Sequence, command.GetType().Name, e.Origin.ToString(), result.Outcome.ToString(),
                command.Side, mapping, words, result.Detail));
        }
        return rows.AsReadOnly();
    }
}
