using System.Text.Json;
using Joey.Core;

internal static class DuelConsoleHandSelectionChecks
{
    private static Card Monster(ushort id, uint attack = 100) =>
        new(id, id, "Selection fixture", "", (3u << 20) | (4u << 25) | (attack << 9));
    private static Card Spell(ushort id) => new(id, id, "Selection fixture", "", 22u << 20);
    private static Card?[] Slots(Card? card = null) => [card, null, null, null, null];

    private static DuelScriptStep Run(DuelConsoleSession console, params string[] lines)
    {
        var script = new DuelConsoleScript(console, lines);
        for (int i = 0; i < 64; i++)
        {
            var step = script.Advance();
            if (step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision)
                return step;
            if (console.Queue.IsBusy) console.Queue.Tick();
        }
        throw new InvalidOperationException("Selection script did not terminate or reach a prompt.");
    }

    public static void Run(Action<bool, string> check)
    {
        var valid = Monster(24);
        var search = DemoDuel.FromPartialObservation(0, [Spell(336)], Slots(), Slots(Monster(47)), 8000,
            side1Deck: [valid, Monster(25, 190), valid]);
        var deckConsole = new DuelConsoleSession(search);
        check(search.TryCaptureCardTarget(1, DuelZone.Deck, 2, out var chosen), "A06 capture the second deck occurrence");
        var searched = Run(deckConsole, "activate hand 0", "choices", "select-hand\t2", "state");
        check(searched.Status == DuelScriptStatus.Completed && search.HandFor(1).Count == 1 &&
            search.TryCaptureCardTarget(1, DuelZone.Hand, 0, out var moved) && moved.Occurrence == chosen.Occurrence &&
            deckConsole.Queue.Trace.Count(row => row.Stage == ActionTraceStage.Executed &&
                row.Entry.Command is ShuffleDeckCommand) == 1,
            "A06 one script answers the opponent-owned deck request and preserves the selected occurrence and shuffle");

        var recovery = DemoDuel.FromPartialObservation(0, [], Slots(Monster(427)), Slots(), 8000,
            side0Graveyard: [Monster(24), Spell(342)]);
        recovery.MonsterFaceDown[0, 0] = recovery.DefensePosition[0, 0] = true;
        var graveConsole = new DuelConsoleSession(recovery);
        var waiting = Run(graveConsole, "flip 0", "damage 200 1");
        check(waiting.Status == DuelScriptStatus.WaitingForDecision && recovery.Life[1] == 8000 &&
            !graveConsole.AllowsPendingChoiceInput("damage 200 1") && graveConsole.AllowsPendingChoiceInput("select-hand 1"),
            "A06 a pending recovery blocks subsequent script operations");
        using (var prefix = JsonDocument.Parse(graveConsole.Execute("trace").Message))
            check(!prefix.RootElement.GetProperty("complete").GetBoolean(),
                "A06 ending at a recovery prompt exports an incomplete trace");
        int beforeQuery = graveConsole.Queue.Trace.Count;
        var choices = graveConsole.Execute("choices");
        check(choices.Success && !choices.Enqueued && choices.Message.EndsWith("\n1: 342", StringComparison.Ordinal) &&
            !graveConsole.Execute("select-hand 0").Success && !graveConsole.Execute("select-hand decline").Success &&
            graveConsole.Queue.Trace.Count == beforeQuery && recovery.Graveyard(0).Count == 2,
            "A06 candidate query is read-only and the mandatory request rejects invalid responses");
        var recovered = Run(graveConsole, "select-hand 1");
        check(recovered.Status == DuelScriptStatus.Completed && recovery.Hand.Single().Id == 342 &&
            recovery.Graveyard(0).Single().Id == 24 && !graveConsole.HasPendingChoice,
            "A06 console queues and completes the graveyard-to-hand movement");

        var optional = DemoDuel.FromPartialObservation(0, [Monster(RitualSearchRules.SonicBird)], Slots(), Slots(), 8000,
            side0Deck: [Spell(669)]);
        var optionalConsole = new DuelConsoleSession(optional);
        var declined = Run(optionalConsole, "summon 0 0", "select-hand decline");
        check(declined.Status == DuelScriptStatus.Completed && optional.Hand.Count == 0 &&
            optional.DeckCount(0) == 1 && !optionalConsole.HasPendingChoice &&
            optionalConsole.Queue.Trace.All(row => row.Entry.Command is not (ShuffleDeckCommand or MoveToHandCommand)),
            "A06 declining an optional hand selection consumes the request without moving or shuffling cards");
    }
}
