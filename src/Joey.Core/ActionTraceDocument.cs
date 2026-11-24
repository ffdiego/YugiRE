using System.Text.Json;

namespace Joey.Core;

public sealed record ActionTraceDocument(string Schema, bool Complete, string Scope,
    IReadOnlyList<ProjectedAction> Rows);

/// <summary>One trace schema for both graphical and headless laboratory hosts.</summary>
public static class ActionTraceJson
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static ActionTraceDocument Capture(ActionQueue queue, string scope)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Trace scope is required.", nameof(scope));
        return new("yugire-action-projection-v1", !queue.IsBusy && queue.Fault is null, scope,
            ActionTraceProjection.ReadExecuted(queue.Trace));
    }

    public static string Serialize(ActionTraceDocument document) => JsonSerializer.Serialize(document, Json);
}
