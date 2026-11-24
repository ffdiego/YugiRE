using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

public sealed record ActionTraceDocument(string Schema, bool Complete, string Scope,
    IReadOnlyList<ProjectedAction> Rows);

public static class ActionTraceExport
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

    // A real rule-produced trace. Not a claim of native parity; synthetic
    // catalogue entries for a deterministic pilot; no original DAT required.
    public static int RunPilot(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 0) { error.WriteLine("Usage: action-trace-pilot"); return 2; }
        Card monster = new(24,24,"Fixture monster","",(3u<<20)|(4u<<25));
        Card upstart = new(1065,1065,"Upstart Goblin","",22u<<20);
        Card sparks = new(342,342,"Sparks","",22u<<20);
        var duel = DemoDuel.FromPartialObservation(0,[upstart,sparks,monster],new Card?[5],new Card?[5],8000,
            side0Deck:[monster]);
        var queue = duel.CreatePrimitiveActionQueue();
        bool Drain()
        {
            for (int i=0;i<128 && queue.IsBusy;i++) queue.Tick();
            return !queue.IsBusy;
        }
        if (!duel.TryQueueDrawSpell(DuelZone.Hand,0,out _) || !Drain() ||
            !duel.TryQueueLifePointSpell(DuelZone.Hand,0,out _) || !Drain() ||
            !duel.TryQueueNormalPlacement(0,0,false,out _) || !Drain())
        { error.WriteLine("Synthetic action trace pilot did not complete."); return 1; }
        output.WriteLine(Serialize(Capture(queue,"synthetic-rule-pilot-upstart-sparks-summon")));
        return 0;
    }
}
