namespace Joey.Core;

// Only adapts text to existing recruitment/revival producers. The request
// owner, not the active turn, identifies the source and destination side.
public sealed partial class DuelConsoleSession
{
    private PendingCardSelection? PendingFieldSelection =>
        duel.PendingCardSelection is { Destination: CardSelectionDestination.Field } request
            ? request : null;

    private DuelConsoleResult ReadFieldChoices()
    {
        if (Queue.IsBusy || PendingFieldSelection is not { } request)
            return new(false, "No pending field selection available.");
        IEnumerable<string> candidates = request.Source == CardSelectionSource.Deck
            ? duel.QueuedDeckRecruitmentCandidates(request).Select(c => $"{c.SourceIndex}: {c.Card.Id}")
            : duel.QueuedGraveyardRevivalCandidates(request).Select(c => $"{c.SourceIndex}: {c.Card.Id}");
        string slots = string.Join(",", Enumerable.Range(0, 5).Where(z => duel.Monsters[request.Owner,z] is null));
        string pose = request.MaySelectFaceDownDefense ? "attack|set" : request.SummonPosition.ToString();
        string syntax = request.Source == CardSelectionSource.Graveyard ? "select-revive INDEX SLOT attack|set"
            : request.MaxSelections > 1 ? "select-recruit batch INDEX [INDEX]" : "select-recruit INDEX SLOT";
        if (request.MinSelections == 0) syntax += " or select-recruit decline";
        return new(true, string.Join("\n", new[] {
            $"Side {request.Owner} {request.Source} → Field; min={request.MinSelections} max={request.MaxSelections}; pose={pose}; free slots={slots}.",
            syntax
        }.Concat(candidates)));
    }

    private DuelConsoleResult SelectRecruitment(string[] words)
    {
        if (PendingFieldSelection is not { Source: CardSelectionSource.Deck } request)
            return new(false, "No pending deck recruitment.");
        bool sameName = request.Filter == CardSelectionFilter.SameNameMonster;
        var targets = new List<CardTarget>();
        int? destination = null;
        if (words.Length == 2 && words[1] == "decline")
        {
            // The producer validates whether decline is legal.
        }
        else if (words.Length is 3 or 4 && words[1] == "batch")
        {
            if (!sameName || request.MaxSelections <= 1)
                return new(false, "Batch selection requires a multi-copy recruitment.");
            foreach (string token in words.Skip(2))
            {
                if (!Index(token, out int index) ||
                    !duel.TryCaptureCardTarget(request.Owner, DuelZone.Deck, index, out var target))
                    return new(false, "Choose tracked deck occurrences.");
                targets.Add(target);
            }
        }
        else if (words.Length == 3 && Index(words[1], out int index) && Slot(words[2], out int slot))
        {
            if (!duel.TryCaptureCardTarget(request.Owner, DuelZone.Deck, index, out var target))
                return new(false, "Choose a tracked deck occurrence.");
            targets.Add(target);
            destination = slot;
        }
        else return new(false, "Expected select-recruit INDEX SLOT|batch INDEX [INDEX]|decline.");

        string message;
        bool accepted = sameName
            ? duel.TryQueueSameNameRecruitment(request, targets, out message, destination, ActionOrigin.Console)
            : duel.TryQueueDeckRecruitment(request, targets.Count == 0 ? null : targets[0],
                destination, out message, ActionOrigin.Console);
        return new(accepted, message, accepted);
    }

    private DuelConsoleResult SelectRevival(string[] words)
    {
        if (PendingFieldSelection is not { Source: CardSelectionSource.Graveyard } request)
            return new(false, "No pending graveyard revival.");
        if (words.Length != 4 || !Index(words[1], out int index) ||
            !Slot(words[2], out int slot) || words[3] is not ("attack" or "set") ||
            !duel.TryCaptureCardTarget(request.Owner, DuelZone.Graveyard, index, out var target))
            return new(false, "Expected select-revive INDEX SLOT attack|set with a tracked graveyard occurrence.");
        bool accepted = duel.TryQueueGraveyardRevival(request, target, slot, words[3] == "set",
            out string message, ActionOrigin.Console);
        return new(accepted, message, accepted);
    }
}
