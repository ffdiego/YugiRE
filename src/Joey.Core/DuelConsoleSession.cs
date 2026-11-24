using System.Globalization;

namespace Joey.Core;

public sealed record DuelConsoleResult(bool Success, string Message, bool Enqueued = false);

/// <summary>Explicit laboratory producer, not an ordinary player input API.
/// The host must opt in and drive Queue.Tick; parsing never mutates duel state.</summary>
public sealed class DuelConsoleSession
{
    public const string Help = "draw N [side] | damage N [side] | heal N [side] | destroy side slot | state | diff | decision ID accept|decline | help";
    private readonly DemoDuel duel;
    private string[]? baseline;
    public ActionQueue Queue { get; }

    public DuelConsoleSession(DemoDuel duel, IPresentationGate? gate = null)
    {
        this.duel = duel ?? throw new ArgumentNullException(nameof(duel));
        Queue = duel.CreatePrimitiveActionQueue(gate);
    }

    public DuelConsoleResult Execute(string line)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words[0].StartsWith('#')) return new(true, "");
        string op = words[0].ToLowerInvariant();
        if (op == "help" && words.Length == 1) return new(true, Help);
        if (op == "state" && words.Length == 1)
        {
            baseline = Snapshot();
            return new(true, string.Join("\n", baseline));
        }
        if (op == "diff" && words.Length == 1)
        {
            if (baseline is null) return new(false, "Use state to capture a baseline first.");
            var now = Snapshot();
            var changes = baseline.Zip(now).Where(p => p.First != p.Second)
                .Select(p => "- " + p.First + "\n+ " + p.Second);
            return new(true, string.Join("\n", changes.DefaultIfEmpty("No changes.")));
        }
        if (op == "decision" && words.Length == 3)
        {
            if (!long.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out long id) ||
                words[2] is not ("accept" or "decline"))
                return new(false, "Expected decision ID accept|decline.");
            if (Queue.Fault is not null) return new(false, "Queue faulted; do not retry.");
            bool ok = Queue.SubmitDecision(id, words[2] == "accept" ? EffectDecisionAnswer.Accept : EffectDecisionAnswer.Decline);
            return new(ok, ok ? "Decision submitted; waiting for scheduler." : "No matching unanswered queue decision.");
        }
        if (Queue.IsBusy) return new(false, "Queue busy; wait or answer its pending decision.");
        if (duel.GameOver) return new(false, "Duel ended.");
        DuelCommand[] batch;
        switch (op)
        {
            case "draw":
            case "damage":
            case "heal":
                if (words.Length is < 2 or > 3 || !Positive(words[1], out int amount) ||
                    (words.Length == 3 && !Side(words[2], out _)))
                    return new(false, $"Expected {op} N [side]; side is 0 or 1.");
                int side = words.Length == 3 ? int.Parse(words[2], CultureInfo.InvariantCulture) : 0;
                if (op == "draw")
                {
                    // Bounded admission; no partially accepted input. Deck-out
                    // experiments can still request more than the remaining deck.
                    if (amount > 80) return new(false, "Draw count must be 1–80.");
                    batch = Enumerable.Range(0, amount).Select(_ => (DuelCommand)new DrawCommand(side)).ToArray();
                }
                else batch = [op == "damage" ? new DamageCommand(side, amount) : new HealCommand(side, amount)];
                break;
            case "destroy":
                if (words.Length != 3 || !Side(words[1], out int owner) ||
                    !int.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4)
                    return new(false, "Expected destroy side slot (0–4, Monster Zone).");
                if (!duel.TryCaptureCardTarget(owner, DuelZone.Monster, slot, out var target))
                    return new(false, "No unique tracked monster in that slot.");
                batch = [new DestroyCommand(owner, target)];
                break;
            default: return new(false, "Unsupported command. " + Help);
        }
        try
        {
            Queue.EnqueueBatch(batch, ActionOrigin.Console);
            return new(true, $"Admitted {batch.Length} command(s).", true);
        }
        catch (InvalidOperationException ex) { return new(false, ex.Message); }
    }

    private static bool Positive(string text, out int n) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n > 0;
    private static bool Side(string text, out int side) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out side) && side is 0 or 1;

    private string[] Snapshot()
    {
        var rows = new List<string> {
            $"turn={duel.Turn} active={duel.ActivePlayer} phase={duel.Phase} main2={duel.IsMainPhase2} winner={duel.Winner?.ToString() ?? "-"} draw={duel.IsDraw}",
            $"queue busy={Queue.IsBusy} pending={Queue.PendingCount} active={Queue.Active?.Sequence.ToString() ?? "-"} decision={Queue.PendingDecision?.Id.ToString() ?? "-"} fault={Queue.Fault is not null}",
            $"legacy choice: card={duel.PendingCardSelection is not null} flip={duel.PendingFlipTarget is not null} flipSpell={duel.PendingFlipSpellTrapTargets is not null} summon={duel.PendingSummonResponse is not null} attack={duel.PendingAttackResponse is not null} effect={duel.PendingEffectDecision is not null} spell={duel.PendingSpellTrapTarget is not null} fusion={duel.PendingFusionSummon is not null} endDiscards={duel.PendingEndDiscards}"
        };
        static string CardKey(Card? card) => card is null ? "-" : $"{card.Id}/{card.Index}";
        for (int p = 0; p < 2; p++)
        {
            rows.Add($"side {p} lp={duel.Life[p]}");
            rows.Add($"side {p} hand={string.Join(",", duel.HandFor(p).Select(CardKey))}");
            rows.Add($"side {p} deck={string.Join(",", duel.DeckSnapshot(p).Select(CardKey))}");
            rows.Add($"side {p} grave={string.Join(",", duel.Graveyard(p).Select(CardKey))}");
            rows.Add($"side {p} banished={string.Join(",", duel.Banished(p).Select(CardKey))}");
            rows.Add($"side {p} fusion={string.Join(",", duel.FusionDeck(p).Select(CardKey))}");
            rows.Add($"side {p} field={CardKey(duel.FieldSpells[p])}:{duel.FieldSpellFaceDown[p]}");
            for (int z = 0; z < 5; z++)
            {
                rows.Add($"side {p} monster {z}={CardKey(duel.Monsters[p,z])}:{duel.MonsterFaceDown[p,z]}:{duel.DefensePosition[p,z]}");
                rows.Add($"side {p} spell {z}={CardKey(duel.Spells[p,z])}:{duel.SpellFaceDown[p,z]}");
            }
        }
        return rows.ToArray();
    }
}
