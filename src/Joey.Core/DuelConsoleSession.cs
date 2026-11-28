using System.Globalization;

namespace Joey.Core;

public sealed record DuelConsoleResult(bool Success, string Message, bool Enqueued = false);

/// <summary>Explicit laboratory producer, not an ordinary player input API.
/// The host must opt in and drive Queue.Tick; parsing never mutates duel state.</summary>
public sealed partial class DuelConsoleSession
{
    public const string Help = "coin SIDE REGION COUNT BITS HIGHLIGHT | dice SIDE REGION INDEX | draw N [side] | damage N [side] | heal N [side] | destroy side slot | spawn ID side zone [slot [attack|defense|set]] | move side zone index dest [slot [attack|defense|set]] | move-id ID side zone index dest [slot [attack|defense|set]] | summon INDEX SLOT [tribute...] | summon-banish HAND SLOT attack|defense GRAVE [GRAVE [GRAVE]] | set INDEX [SLOT [tribute...]] | respond-summon pass|SIDE SLOT | respond-attack pass|SIDE SLOT | flip SLOT | position SLOT attack|defense | activate hand|set INDEX [SIDE field|SIDE spell SLOT|SIDE monster SLOT] | activate field | equip hand|set INDEX SIDE SLOT | ritual hand|set SPELL MONSTER SLOT attack|defense (hand|field INDEX)... | battle begin|end | turn draw|end | discard-end INDEX | attack SLOT direct|TARGET_SLOT | discard SIDE INDEX | send SIDE INDEX | state | diff | trace | decision ID accept|decline | choices | select-hand INDEX|decline | select-flip SIDE SLOT [SIDE SLOT]|decline | select-st-pair SIDE SLOT|field SIDE SLOT|field|decline | select-recruit INDEX SLOT|batch INDEX [INDEX]|decline | select-revive INDEX SLOT attack|set | help";
    private readonly DemoDuel duel;
    private readonly Dictionary<ushort, Card> catalog;
    private string[]? baseline;
    public ActionQueue Queue { get; }
    public bool HasPendingEndDiscard => duel.HasQueuedEndDiscard;
    public bool HasPendingChoice => Queue.PendingDecision is not null || PendingSummonTrapResponse is not null ||
        PendingAttackTrapResponse is not null || PendingHandSelection is not null || HasPendingEndDiscard || HasPendingFlipSelection || PendingFieldSelection is not null;

    private DemoSummonResponse? PendingSummonTrapResponse =>
        duel.PendingSummonResponse is { } response && duel.UsesQueuedSummonResponse(response) ? response : null;

    private DemoAttackResponse? PendingAttackTrapResponse =>
        duel.PendingAttackResponse is { } response && duel.UsesQueuedAttackResponse(response) ? response : null;

    public bool AllowsPendingEndDiscardInput(string line)
    {
        var words = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        return words.Length != 0 && words[0].ToLowerInvariant() is ("discard-end" or "state" or "diff" or "trace" or "help");
    }

    private PendingCardSelection? PendingHandSelection =>
        duel.PendingCardSelection is { MaxSelections: 1 } request && duel.UsesQueuedHandSelection(request)
            ? request
            : null;

    public static DemoDuel EmptyLabDuel() => DemoDuel.FromPartialObservation(0, [], new Card?[5],
        new Card?[5], 8000, equipmentContextResolved: true);

    public DuelConsoleSession(DemoDuel duel, IPresentationGate? gate = null, IEnumerable<Card>? cards = null)
        : this(duel, gate, cards, attach: false) { }

    public static DuelConsoleSession Attach(DemoDuel duel, IPresentationGate? gate = null, IEnumerable<Card>? cards = null) =>
        new(duel, gate, cards, attach: true);

    private DuelConsoleSession(DemoDuel duel, IPresentationGate? gate, IEnumerable<Card>? cards, bool attach)
    {
        this.duel = duel ?? throw new ArgumentNullException(nameof(duel));
        catalog = (cards ?? []).GroupBy(c => c.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        Queue = attach ? duel.ConsoleQueue(gate) : duel.CreatePrimitiveActionQueue(gate, allowLabEdits: true);
    }

    public DuelConsoleResult Execute(string line)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words[0].StartsWith('#')) return new(true, "");
        string op = words[0].ToLowerInvariant();
        if (op == "help" && words.Length == 1) return new(true, Help);
        if (op == "trace" && words.Length == 1)
        {
            var trace = ActionTraceJson.Capture(Queue, "laboratory-console");
            return new(true, ActionTraceJson.Serialize(trace with { Complete = trace.Complete && !HasPendingChoice }));
        }
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
        if (op == "choices" && words.Length == 1)
        {
            if (PendingSummonTrapResponse is { } response)
                return new(true, string.Join("\n", response.EligibleTraps.Select(
                    option => $"{option.Owner} {option.Zone}: {option.Trap.Id}").Append("pass")));
            if (PendingAttackTrapResponse is { } attack)
                return new(true, string.Join("\n", attack.EligibleTraps.Select(
                    option => $"{option.Owner} {option.Zone}: {option.Trap.Id}").Append("pass")));
            return HasPendingFlipSelection ? ReadFlipChoices() : PendingFieldSelection is not null ? ReadFieldChoices() : ReadHandChoices();
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
        if (HasPendingChoice && !AllowsPendingChoiceInput(line)) return new(false, PendingChoicePrompt);
        if (op == "select-hand") return SelectHandCard(words);
        if (op == "select-flip") return SelectMonsterFlip(words);
        if (op == "select-st-pair") return SelectSpellTrapPair(words);
        if (op == "select-recruit") return SelectRecruitment(words);
        if (op == "select-revive") return SelectRevival(words);
        if (op == "respond-summon") return RespondToSummon(words);
        if (op == "respond-attack") return RespondToAttack(words);
        // Legal inputs use the same producer methods as normal play, with
        // the source labelled Console only for tracing, not for rule bypass.
        if (op == "summon-banish") return ExecuteBanishSummon(words);
        if (op == "ritual") return ExecuteRitualSummon(words);
        if (op is "summon" or "set" or "flip" or "position" or "activate" or "equip" or "battle" or "turn" or "discard-end" or "attack")
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
            case "coin":
                if (words.Length != 6 || !Side(words[1], out int coinSide) ||
                    !Index(words[2], out int coinRegion) || coinRegion > 10 ||
                    !Positive(words[3], out int count) || count is not (1 or 3) ||
                    !Index(words[4], out int bits) || bits >= (1 << count) ||
                    !Index(words[5], out int highlight) || highlight > 1)
                    return new(false, "Expected coin SIDE REGION(0–10) COUNT(1|3) BITS HIGHLIGHT(0|1).");
                batch = [new LabCoinResultCommand(new(coinSide,coinRegion,count,(uint)bits,highlight))];
                break;
            case "dice":
                if (words.Length != 4 || !Side(words[1], out int diceSide) ||
                    !Index(words[2], out int diceRegion) || diceRegion > 10 ||
                    !Positive(words[3], out int face) || face > 6)
                    return new(false, "Expected dice SIDE REGION(0–10) INDEX(1–6).");
                batch = [new LabDiceResultCommand(new(diceSide,diceRegion,face))];
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

    public bool AllowsPendingChoiceInput(string line)
    {
        var words = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;
        string operation = words[0].ToLowerInvariant();
        if (operation is "state" or "diff" or "trace" or "help") return true;
        return Queue.PendingDecision is not null
            ? operation == "decision"
            : PendingSummonTrapResponse is not null ? operation is ("choices" or "respond-summon")
            : PendingAttackTrapResponse is not null ? operation is ("choices" or "respond-attack")
            : PendingHandSelection is not null ? operation is ("choices" or "select-hand")
            : HasPendingEndDiscard ? operation == "discard-end"
            : duel.PendingFlipTarget is not null ? operation is ("choices" or "select-flip")
            : duel.PendingFlipSpellTrapTargets is not null ? operation is ("choices" or "select-st-pair")
            : PendingFieldSelection is { Source: CardSelectionSource.Deck } ? operation is ("choices" or "select-recruit")
            : PendingFieldSelection is { Source: CardSelectionSource.Graveyard } && operation is ("choices" or "select-revive");
    }

    private DuelConsoleResult ExecuteBanishSummon(string[] words)
    {
        const string usage = "Expected summon-banish HAND SLOT attack|defense GRAVE [GRAVE [GRAVE]].";
        if (words.Length is < 5 or > 7 || !Index(words[1], out int handIndex) ||
            !Slot(words[2], out int slot) || words[3] is not ("attack" or "defense"))
            return new(false, usage);

        int side = duel.ActivePlayer;
        if (!duel.TryCaptureCardTarget(side, DuelZone.Hand, handIndex, out var source))
            return new(false, "Choose a tracked card in the active player's hand.");

        var cost = new List<CardTarget>();
        foreach (string token in words.Skip(4))
        {
            if (!Index(token, out int index) ||
                !duel.TryCaptureCardTarget(side, DuelZone.Graveyard, index, out var target))
                return new(false, "Choose a tracked card in the active player's Graveyard.");
            cost.Add(target);
        }

        bool admitted = duel.TryQueueGraveyardBanishSummon(source, slot, cost,
            words[3] == "defense", out string message, ActionOrigin.Console);
        return new(admitted, message, admitted);
    }

    private DuelConsoleResult RespondToSummon(string[] words)
    {
        if (PendingSummonTrapResponse is not { } response)
            return new(false, "No queued summon response is pending.");
        DemoSummonTrapOption? choice = null;
        if (words is not [_, "pass"])
        {
            if (words.Length != 3 || !Side(words[1], out int owner) || !Slot(words[2], out int zone))
                return new(false, "Expected respond-summon pass | respond-summon SIDE SLOT.");
            choice = response.EligibleTraps.FirstOrDefault(option => option.Owner == owner && option.Zone == zone);
            if (choice is null) return new(false, "Choose a Set Trap from the current response.");
        }
        bool admitted = duel.TryQueueSummonResponse(response, choice, out var message, ActionOrigin.Console);
        return new(admitted, message, admitted);
    }

    private DuelConsoleResult RespondToAttack(string[] words)
    {
        if (PendingAttackTrapResponse is not { } response)
            return new(false, "No queued attack response is pending.");
        DemoAttackTrapOption? choice = null;
        if (words is not [_, "pass"])
        {
            if (words.Length != 3 || !Side(words[1], out int owner) || !Slot(words[2], out int zone))
                return new(false, "Expected respond-attack pass | respond-attack SIDE SLOT.");
            choice = response.EligibleTraps.FirstOrDefault(option => option.Owner == owner && option.Zone == zone);
            if (choice is null) return new(false, "Choose a Set Trap from the current response.");
        }
        bool admitted = duel.TryQueueAttackResponse(response, choice, out var message, ActionOrigin.Console);
        return new(admitted, message, admitted);
    }

    private DuelConsoleResult ExecuteLegal(string op, string[] words)
    {
        bool ok;
        string message;
        if (op == "ritual") return ExecuteRitualSummon(words);
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
        else if (op == "turn")
        {
            if (words.Length != 2 || words[1] is not ("draw" or "end"))
                return new(false, "Expected turn draw|end.");
            ok = words[1] == "draw"
                ? duel.TryQueueTurnDraw(out message, ActionOrigin.Console)
                : duel.TryQueueEndTurn(out message, ActionOrigin.Console);
        }
        else if (op == "discard-end")
        {
            if (words.Length != 2 || !Index(words[1], out int index))
                return new(false, "Expected discard-end INDEX.");
            ok = duel.TryQueueEndTurnDiscard(index, out message, ActionOrigin.Console);
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
            int defender = -1;
            if (words[2] != "direct" && !Slot(words[2], out defender))
                return new(false, "Expected attack SLOT direct|TARGET_SLOT.");
            ok = duel.TryQueueAttack(attacker, defender, out message, ActionOrigin.Console);
        }
        else if (op == "activate") return ExecuteActivation(words);
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

    private DuelConsoleResult ExecuteActivation(string[] words)
    {
        const string usage = "Expected activate hand|set INDEX [SIDE field|SIDE spell SLOT|SIDE monster SLOT] or activate field.";
        if(words.Length==2 && words[1]=="field")
        {
            bool admitted=duel.TryQueueFieldSpellActivation(DuelZone.FieldSpell,0,out string fieldMessage,ActionOrigin.Console);
            return new(admitted,fieldMessage,admitted);
        }
        if (words.Length < 3 || words[1] is not ("hand" or "set") || !Index(words[2], out int index))
            return new(false, usage);

        bool fromSet = words[1] == "set";
        if (words.Length == 3)
        {
            bool accepted = duel.TryQueueActivation(index, fromSet, out string detail, ActionOrigin.Console);
            return new(accepted, detail, accepted);
        }

        if (words.Length is not (5 or 6) || !Side(words[3], out int side))
            return new(false, usage);
        DuelZone targetZone;
        int slot = 0;
        switch (words[4])
        {
            case "field" when words.Length == 5: targetZone = DuelZone.FieldSpell; break;
            case "spell" when words.Length == 6 && Slot(words[5], out slot): targetZone = DuelZone.SpellTrap; break;
            case "monster" when words.Length == 6 && Slot(words[5], out slot): targetZone = DuelZone.Monster; break;
            default: return new(false, usage);
        }

        DuelZone sourceZone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
        if (!duel.TryCaptureCardTarget(duel.ActivePlayer, sourceZone, index, out var source) ||
            !duel.TryCaptureCardTarget(side, targetZone, slot, out var target))
            return new(false, "Choose a tracked source and target.");

        bool enqueued;
        string message;
        if (targetZone == DuelZone.Monster)
        {
            var card = fromSet ? duel.Spells[duel.ActivePlayer, index] : duel.Hand[index];
            enqueued = card is not null && TargetedMonsterSpellRules.Supports(card)
                ? duel.TryQueueTargetedMonsterSpell(source, target, out message, ActionOrigin.Console)
                : duel.TryQueueTemporaryStatModifier(source, target, out message, ActionOrigin.Console);
        }
        else enqueued = duel.TryQueueTargetedSpellTrap(source, target, out message, ActionOrigin.Console);
        return new(enqueued, message, enqueued);
    }

    private DuelConsoleResult ReadHandChoices()
    {
        if (Queue.IsBusy || PendingHandSelection is not { } request)
            return new(false, "No pending hand selection available.");
        if (request.Source == CardSelectionSource.Hand || request.Destination == CardSelectionDestination.Banished)
        {
            var manipulationLines = request.EligibleCards.Select(c =>
                $"{c.AnswerIndex}: side {c.SourceSide ?? request.SourceSide} {request.Source}[{c.SourceIndex}] {c.Card.Id}");
            return new(true,string.Join("\n",new[] {
                $"Chooser {request.Owner}: {request.Source} → {request.Destination}; select-hand INDEX{(request.MinSelections == 0 ? "|decline" : "")}"
            }.Concat(manipulationLines)));
        }
        var choices = request.Source == CardSelectionSource.Deck
            ? duel.QueuedDeckSearchCandidates(request).Select(item => (item.SourceIndex, item.Card))
            : duel.QueuedGraveyardRecoveryCandidates(request).Select(item => (item.SourceIndex, item.Card));
        var lines = choices.Select(item => $"{item.SourceIndex}: {item.Card.Id}").ToArray();
        string prompt = $"Chooser {request.Owner}: side {request.SourceSide} {request.Source} → {request.Destination} (source indices):";
        if (request.MinSelections == 0) prompt += " select-hand decline is available.";
        return new(true, string.Join("\n", new[] { prompt }.Concat(lines)));
    }

    private DuelConsoleResult SelectHandCard(string[] words)
    {
        if (PendingHandSelection is not { } request)
            return new(false, "No pending hand selection available.");
        if (words.Length != 2)
            return new(false, "Expected select-hand INDEX|decline.");

        if (request.Source == CardSelectionSource.Hand || request.Destination == CardSelectionDestination.Banished)
        {
            int? pick = null;
            if (words[1] != "decline")
            {
                if (!Index(words[1],out int handIndex)) return new(false,"Choose a selection index.");
                pick = handIndex;
            }
            bool ok = duel.TrySubmitQueuedHandSelection(request,pick,out string handMessage,ActionOrigin.Console);
            return new(ok,handMessage,ok);
        }
        bool fromDeck = request.Source == CardSelectionSource.Deck;
        CardTarget? target = null;
        if (words[1] != "decline")
        {
            if (!Index(words[1], out int index) ||
                !duel.TryCaptureCardTarget(request.Owner, fromDeck ? DuelZone.Deck : DuelZone.Graveyard,
                    index, out var selected))
                return new(false, "Choose a tracked source index for the pending request.");
            target = selected;
        }

        string message;
        bool accepted = fromDeck
            ? duel.TryQueueDeckSearch(request, target, out message, ActionOrigin.Console)
            : duel.TryQueueGraveyardRecovery(request, target, out message, ActionOrigin.Console);
        return new(accepted, message, accepted);
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
