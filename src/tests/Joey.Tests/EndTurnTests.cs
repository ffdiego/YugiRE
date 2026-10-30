using Joey.Core;

static class EndTurnTests
{
    public static void Run(Action<bool, string> check)
    {
        var monster = new Card(1, 1, "Monster", "", 0);
        var sparks = new Card(2, 342, "Sparks", "", 21u << 20);
        // Deliberately oversized opening fixture to exercise multiple required choices.
        var duel = new DemoDuel([monster, monster, monster, sparks, sparks, sparks, sparks, sparks]);
        check(duel.Hand.Count == 8 && duel.EndTurn(out _) && duel.Turn == 1 && duel.ActivePlayer == 0 &&
            duel.Phase == DemoPhase.End && duel.PendingEndDiscards == 2, "End turn waits for two excess hand cards");
        check(!duel.Draw(out _) && !duel.Play(0, out _) && !duel.Summon(0, 0, 0, true, out _) &&
            !duel.BeginBattle(out _), "End-phase discard blocks main and battle actions");
        duel.NextTurn();
        check(duel.Turn == 1 && duel.PendingEndDiscards == 2, "Repeated end turn cannot bypass mandatory discards");
        check(!duel.DiscardForEndTurn(-1, out _) && !duel.DiscardForEndTurn(8, out _) &&
            duel.Hand.Count == 8 && duel.Graveyard(0).Count == 0, "Invalid discard selection preserves hand and graveyard");
        var chosen = duel.Hand[3];
        check(duel.DiscardForEndTurn(3, out _) && duel.Hand.Count == 7 && duel.PendingEndDiscards == 1 &&
            duel.Graveyard(0).SequenceEqual([chosen]) && duel.Turn == 1, "First chosen discard moves only to the acting player's graveyard");
        check(duel.DiscardForEndTurn(0, out _) && duel.Turn == 2 && duel.ActivePlayer == 1 &&
            duel.Phase == DemoPhase.Main && duel.Graveyard(0).Count == 2 && duel.Graveyard(1).Count == 0,
            "Last required discard completes the turn exactly once");
        check(!duel.DiscardForEndTurn(0, out _) && duel.Graveyard(1).Count == 0, "No discard outside pending end phase");
        var normal = DemoDuel.FromDecks(Enumerable.Repeat(monster, 40).ToArray(), Enumerable.Repeat(monster, 40).ToArray());
        normal.Draw(out _);
        check(normal.EndTurn(out _) && normal.Turn == 2 && normal.PendingEndDiscards == 0 && normal.Graveyard(0).Count == 0,
            "Exactly six cards require no discard");
        normal.Draw(out _); normal.EndTurn(out _); normal.Draw(out _); normal.EndTurn(out _);
        check(normal.Turn == 3 && normal.Hand.Count == 7 && normal.PendingEndDiscards == 1,
            "Imported deck reaches the same six-card limit after natural draws");
    }
}
