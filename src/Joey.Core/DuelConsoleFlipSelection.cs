namespace Joey.Core;

// Console-only location parsing. All eligibility, identity revalidation and
// resolution stay in the existing queued producers.
public sealed partial class DuelConsoleSession
{
    private bool HasPendingFlipSelection =>
        duel.PendingFlipTarget is not null || duel.PendingFlipSpellTrapTargets is not null;

    public string PendingChoicePrompt => Queue.PendingDecision is { } decision
        ? $"Decision {decision.Id} requires explicit answer."
        : PendingSummonTrapResponse is not null ? "Pending Normal Summon response requires respond-summon."
        : PendingAttackTrapResponse is not null ? "Pending attack response requires respond-attack."
        : HasPendingEndDiscard ? "End Phase requires discard-end INDEX."
        : PendingHandSelection is not null ? "Pending hand selection requires select-hand."
        : duel.PendingFlipTarget is { Trigger: DemoMonsterEffectTrigger.PositionChange }
            ? "Pending monster position effect requires select-flip."
        : duel.PendingFlipTarget is not null ? "Pending monster FLIP requires select-flip."
        : duel.PendingFlipSpellTrapTargets is not null ? "Pending Spell/Trap FLIP requires select-st-pair."
        : PendingFieldSelection is { Source: CardSelectionSource.Deck } ? "Pending recruitment requires select-recruit."
        : PendingFieldSelection is { Source: CardSelectionSource.Graveyard } ? "Pending revival requires select-revive."
        : "No pending choice.";

    private DuelConsoleResult ReadFlipChoices()
    {
        if (Queue.IsBusy) return new(false, "Wait for the current command.");
        if (duel.PendingFlipTarget is { } monster)
        {
            var candidates = duel.QueuedMonsterFlipCandidates();
            string prompt = monster.Trigger == DemoMonsterEffectTrigger.PositionChange
                ? $"Side {monster.Owner} position effect; select-flip SIDE SLOT:"
                : $"Side {monster.Owner} monster FLIP; select-flip SIDE SLOT [SIDE SLOT]|decline:";
            return new(true, string.Join("\n", new[] { prompt }.Concat(candidates.Select(c => $"{c.Target.Side} {c.Zone}: {(c.FaceDown ? "hidden" : c.Card.Id.ToString())}"))));
        }
        if (duel.PendingFlipSpellTrapTargets is { } pair)
        {
            var candidates = duel.QueuedFlipSpellTrapCandidates(pair);
            return new(true, string.Join("\n", new[] {
                $"Side {pair.Owner} Spell/Trap FLIP; select-st-pair SIDE SLOT|field SIDE SLOT|field:"
            }.Concat(candidates.Select(c => $"{c.Target.Side} {(c.Target.Zone == DuelZone.FieldSpell ? "field" : c.Zone.ToString())}: {(c.FaceDown ? "hidden" : c.Card.Id.ToString())}"))));
        }
        return new(false, "No pending FLIP.");
    }

    private DuelConsoleResult SelectMonsterFlip(string[] words)
    {
        if (duel.PendingFlipTarget is not { } request ||
            !duel.TryCaptureCardTarget(request.Owner, DuelZone.Monster, request.SourceZone, out var source))
            return new(false, "No tracked pending monster FLIP.");
        var targets = new List<CardTarget>();
        if (!(words.Length == 2 && words[1] == "decline"))
        {
            if (words.Length is not (3 or 5))
                return new(false, "Expected select-flip SIDE SLOT [SIDE SLOT]|decline.");
            for (int i = 1; i < words.Length; i += 2)
            {
                if (!Side(words[i], out int side) || !Slot(words[i + 1], out int slot) ||
                    !duel.TryCaptureCardTarget(side, DuelZone.Monster, slot, out var target))
                    return new(false, "Choose a tracked monster occurrence.");
                targets.Add(target);
            }
        }
        bool accepted = duel.TryQueueMonsterFlipAnswer(source, targets, out string message, ActionOrigin.Console);
        return new(accepted, message, accepted);
    }

    private DuelConsoleResult SelectSpellTrapPair(string[] words)
    {
        if (duel.PendingFlipSpellTrapTargets is not { } request)
            return new(false, "No pending Spell/Trap FLIP.");
        var targets = new List<CardTarget>();
        if (!(words.Length == 2 && words[1] == "decline"))
        {
            if (words.Length != 5)
                return new(false, "Expected select-st-pair SIDE SLOT|field SIDE SLOT|field.");
            for (int i = 1; i < words.Length; i += 2)
            {
                if (!Side(words[i], out int side))
                    return new(false, "Side must be 0 or 1.");
                bool field = words[i + 1] == "field";
                int slot = 0;
                if (!field && !Slot(words[i + 1], out slot))
                    return new(false, "Choose a Spell/Trap slot or field.");
                if (!duel.TryCaptureCardTarget(side, field ? DuelZone.FieldSpell : DuelZone.SpellTrap, slot, out var target))
                    return new(false, "Choose a tracked Spell/Trap occurrence.");
                targets.Add(target);
            }
        }
        bool accepted = duel.TryQueueFlipSpellTrapPair(request, targets, out string message, ActionOrigin.Console);
        return new(accepted, message, accepted);
    }
}
