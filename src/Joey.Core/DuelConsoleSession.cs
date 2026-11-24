using System.Globalization;

namespace Joey.Core;

public sealed record DuelConsoleResult(bool Success, string Message, bool Enqueued = false);

/// <summary>Explicit laboratory producer, not an ordinary player input API.
/// The host must opt in and drive Queue.Tick; parsing never mutates duel state.</summary>
public sealed class DuelConsoleSession
{
    public const string Help = "draw N [side] | damage N [side] | heal N [side] | destroy side slot | spawn ID side zone [slot [attack|defense|set]] | move side zone index dest [slot [attack|defense|set]] | move-id ID side zone index dest [slot [attack|defense|set]] | summon INDEX SLOT [tribute...] | set INDEX [SLOT [tribute...]] | flip SLOT | position SLOT attack|defense | activate hand|set INDEX | equip hand|set INDEX SIDE SLOT | battle begin|end | attack SLOT direct|TARGET_SLOT | discard SIDE INDEX | send SIDE INDEX | state | diff | trace | decision ID accept|decline | help";
    private readonly DemoDuel duel;
    private readonly Dictionary<ushort, Card> catalog;
    private string[]? baseline;
    public ActionQueue Queue { get; }

    public static DemoDuel EmptyLabDuel() => DemoDuel.FromPartialObservation(0, [], new Card?[5],
        new Card?[5], 8000, equipmentContextResolved: true);

    public DuelConsoleSession(DemoDuel duel, IPresentationGate? gate = null, IEnumerable<Card>? cards = null)
    {
        this.duel = duel ?? throw new ArgumentNullException(nameof(duel));
        catalog = (cards ?? []).GroupBy(c => c.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        Queue = duel.CreatePrimitiveActionQueue(gate, allowLabEdits: true);
    }

    public DuelConsoleResult Execute(string line)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words[0].StartsWith('#')) return new(true, "");
        string op = words[0].ToLowerInvariant();
        if (op == "help" && words.Length == 1) return new(true, Help);
        if (op == "trace" && words.Length == 1)
            return new(true, ActionTraceJson.Serialize(ActionTraceJson.Capture(Queue, "laboratory-console")));
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
        // Legal inputs use the same producer methods as normal play, with
        // the source labelled Console only for tracing, not for rule bypass.
        if (op is "summon" or "set" or "flip" or "position" or "activate" or "equip" or "battle" or "attack")
            return ExecuteLegal(op, words);
        // Replay projection is a setup edit, not a legal input. Capture the
        // occurrence only if the resource at the recorded source index matches.
        ushort? expectedMoveId = null;
        if (op == "move-id")
        {
            if (words.Length < 6 || !ushort.TryParse(words[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out ushort expected) || expected == 0)
                return new(false, "Expected move-id ID side zone index dest [slot [position]].");
            expectedMoveId = expected;
            words = ["move", .. words.Skip(2)];
            op = "move";
        }
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
            case "discard":
            case "send":
                if (words.Length != 3 || !Side(words[1], out int handSide) || !Index(words[2], out int handIndex) ||
                    !duel.TryCaptureCardTarget(handSide, DuelZone.Hand, handIndex, out var handTarget))
                    return new(false, $"Expected {op} SIDE INDEX with a tracked hand card.");
                batch = [op == "discard" ? new DiscardCommand(handSide, handTarget) : new SendToGraveyardCommand(handSide, handTarget)];
                break;
            case "spawn":
                if (words.Length < 4 ||
                    !ushort.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out var resource) ||
                    !Side(words[2], out int spawnSide) ||
                    !Destination(words, 3, out var spawnZone, out int spawnSlot, out bool spawnDown, out bool spawnDefense))
                    return new(false, "Expected spawn ID side zone [slot [attack|defense|set]].");
                if (!catalog.TryGetValue(resource, out var template))
                    return new(false, "Unknown or ambiguous resource ID in the selected catalogue.");
                batch = [new LabSpawnCommand(spawnSide, template, spawnZone, spawnSlot, spawnDown, spawnDefense)];
                break;
            case "move":
                if (words.Length < 5 || !Side(words[1], out int moveSide) ||
                    !Zone(words[2], out var sourceZone) ||
                    !int.TryParse(words[3], NumberStyles.None, CultureInfo.InvariantCulture, out int sourceIndex) ||
                    !Destination(words, 4, out var moveZone, out int moveSlot, out bool moveDown, out bool moveDefense) ||
                    sourceZone == moveZone)
                    return new(false, "Expected move side sourceZone index destZone [slot [attack|defense|set]] (distinct zones).");
                if (!duel.TryCaptureCardTarget(moveSide, sourceZone, sourceIndex, out var sourceTarget))
                    return new(false, "Source is not a unique tracked occurrence.");
                if (expectedMoveId is ushort expectedId &&
                    !duel.MatchesOccurrenceResource(sourceTarget, expectedId))
                    return new(false, $"Replay source mismatch: expected resource {expectedId}.");
                batch = [new LabMoveCommand(moveSide, sourceTarget, moveZone, moveSlot, moveDown, moveDefense)];
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

    private DuelConsoleResult ExecuteLegal(string op, string[] words)
    {
        bool ok;
        string message;
        if (op is "summon" or "set")
        {
            if (words.Length < 2 || !Index(words[1], out int index) || index >= duel.Hand.Count)
                return new(false, "Choose an active hand index.");
            if (op == "set" && !duel.Hand[index].IsMonster)
            {
                if (words.Length != 2) return new(false, "Spell/Trap Set uses its usual zone: set INDEX.");
                ok = duel.TryQueueSpellTrapSet(index, out message, ActionOrigin.Console);
            }
            else
            {
                if (words.Length is < 3 or > 5 || !Slot(words[2], out int slot))
                    return new(false, "Expected summon|set INDEX SLOT [tribute slots].");
                var tributes = new List<int>();
                foreach (string token in words.Skip(3))
                {
                    if (!Slot(token, out int tribute)) return new(false, "Invalid tribute slot.");
                    tributes.Add(tribute);
                }
                ok = duel.TryQueueNormalPlacement(index, slot, op == "set", out message, tributes, ActionOrigin.Console);
            }
        }
        else if (op == "equip")
        {
            if (words.Length != 5 || words[1] is not ("hand" or "set") || !Index(words[2], out int sourceIndex) ||
                !Side(words[3], out int targetSide) || !Slot(words[4], out int targetSlot))
                return new(false, "Expected equip hand|set INDEX SIDE SLOT.");
            ok = duel.TryQueueEquipment(words[1] == "set" ? DuelZone.SpellTrap : DuelZone.Hand,
                sourceIndex, targetSide, targetSlot, out message, ActionOrigin.Console);
        }
        else if (op == "battle")
        {
            if (words.Length != 2 || words[1] is not ("begin" or "end"))
                return new(false, "Expected battle begin|end.");
            ok = duel.TryQueueBattlePhaseChange(words[1] == "begin", out message, ActionOrigin.Console);
        }
        else if (op == "attack")
        {
            if (words.Length != 3 || !Slot(words[1], out int attacker))
                return new(false, "Expected attack SLOT direct|TARGET_SLOT.");
            if (words[2] == "direct")
                ok = duel.TryQueueDirectAttack(attacker, out message, ActionOrigin.Console);
            else if (Slot(words[2], out int defender))
                ok = duel.TryQueueTargetedAttack(attacker, defender, out message, ActionOrigin.Console);
            else return new(false, "Expected attack SLOT direct|TARGET_SLOT.");
        }
        else if (op == "activate")
        {
            if (words.Length != 3 || words[1] is not ("hand" or "set") || !Index(words[2], out int index))
                return new(false, "Expected activate hand|set INDEX.");
            ok = duel.TryQueueActivation(index, words[1] == "set", out message, ActionOrigin.Console);
        }
        else
        {
            if ((op == "flip" && words.Length != 2) || (op == "position" && words.Length != 3) ||
                !Slot(words[1], out int slot) || duel.Monsters[duel.ActivePlayer, slot] is null)
                return new(false, "Expected flip SLOT or position SLOT attack|defense.");
            bool hidden = duel.MonsterFaceDown[duel.ActivePlayer, slot];
            if (op == "flip" && !hidden) return new(false, "Flip requires a face-down monster.");
            if (op == "position" && (hidden || words[2] is not ("attack" or "defense") ||
                duel.DefensePosition[duel.ActivePlayer, slot] == (words[2] == "defense")))
                return new(false, "Choose a different face-up battle position; use flip for a set monster.");
            ok = duel.TryQueueChangePosition(slot, out message, ActionOrigin.Console);
        }
        return new(ok, message, ok);
    }

    private static bool Index(string value, out int index) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    private static bool Slot(string value, out int slot) => Index(value, out slot) && slot < 5;

    private static bool Zone(string name, out DuelZone zone)
    {
        zone = name.ToLowerInvariant() switch
        {
            "hand" => DuelZone.Hand, "deck" => DuelZone.Deck, "grave" => DuelZone.Graveyard,
            "banished" => DuelZone.Banished, "monster" => DuelZone.Monster,
            "spell" => DuelZone.SpellTrap, "field" => DuelZone.FieldSpell, _ => (DuelZone)(-1)
        };
        return Enum.IsDefined(zone);
    }

    private static bool Destination(string[] words, int offset, out DuelZone zone, out int slot,
        out bool faceDown, out bool defense)
    {
        slot = -1; faceDown = defense = false;
        if (!Zone(words[offset], out zone)) return false;
        int remaining = words.Length - offset - 1;
        if (zone == DuelZone.FieldSpell)
        { slot = 0; faceDown = true; return remaining == 0; }
        if (zone is not (DuelZone.Monster or DuelZone.SpellTrap)) return remaining == 0;
        if (remaining is < 1 or > 2 ||
            !int.TryParse(words[offset + 1], NumberStyles.None, CultureInfo.InvariantCulture, out slot) || slot > 4)
            return false;
        if (zone == DuelZone.SpellTrap)
        { faceDown = true; return remaining == 1 || words[offset + 2] == "set"; }
        string position = remaining == 2 ? words[offset + 2] : "attack";
        if (position is not ("attack" or "defense" or "set")) return false;
        faceDown = position == "set";
        defense = position != "attack";
        return true;
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
        string CardKey(int side, DuelZone zone, int index, Card? card) => card is null ? "-" :
            $"{card.Id}/{card.Index}#" + (duel.TryCaptureCardTarget(side, zone, index, out var target) ? target.Occurrence.Value.ToString(CultureInfo.InvariantCulture) : "?");
        for (int p = 0; p < 2; p++)
        {
            rows.Add($"side {p} lp={duel.Life[p]}");
            rows.Add($"side {p} hand={string.Join(",", duel.HandFor(p).Select((card,i) => CardKey(p, DuelZone.Hand, i, card)))}");
            rows.Add($"side {p} deck={string.Join(",", duel.DeckSnapshot(p).Select((card,i) => CardKey(p, DuelZone.Deck, i, card)))}");
            rows.Add($"side {p} grave={string.Join(",", duel.Graveyard(p).Select((card,i) => CardKey(p, DuelZone.Graveyard, i, card)))}");
            rows.Add($"side {p} banished={string.Join(",", duel.Banished(p).Select((card,i) => CardKey(p, DuelZone.Banished, i, card)))}");
            rows.Add($"side {p} fusion={string.Join(",", duel.FusionDeck(p).Select(card => $"{card.Id}/{card.Index}"))}");
            rows.Add($"side {p} field={CardKey(p, DuelZone.FieldSpell, 0, duel.FieldSpells[p])}:{duel.FieldSpellFaceDown[p]}");
            for (int z = 0; z < 5; z++)
            {
                rows.Add($"side {p} monster {z}={CardKey(p, DuelZone.Monster, z, duel.Monsters[p,z])}:{duel.MonsterFaceDown[p,z]}:{duel.DefensePosition[p,z]}");
                rows.Add($"side {p} spell {z}={CardKey(p, DuelZone.SpellTrap, z, duel.Spells[p,z])}:{duel.SpellFaceDown[p,z]}");
            }
        }
        return rows.ToArray();
    }
}
