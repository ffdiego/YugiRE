using Joey.Core;

// Temporary source-compatibility for opt-in historical fixtures only.
// No direct mutation, new queue, trace reset or gate reconfiguration.
// Historical fixtures which assumed those things must be retired or
// migrated; the active suite must use explicit typed admission instead.
internal static class HistoricalTurnDrawFixture
{
    internal static void NextTurn(this DemoDuel duel) => EndTurn(duel, out _);

    internal static bool EndTurn(this DemoDuel duel, out string message)
    {
        if (Environment.GetEnvironmentVariable("YUGIRE_HISTORICAL_CARD_TESTS") != "1")
            throw new InvalidOperationException("Use explicit queued end-turn admission in active fixtures.");
        return TurnFixture.End(duel, out message);
    }

    internal static bool BeginBattle(this DemoDuel duel, out string message) =>
        HistoricalBattle(duel, true, out message);

    internal static bool EndBattle(this DemoDuel duel, out string message) =>
        HistoricalBattle(duel, false, out message);

    private static bool HistoricalBattle(DemoDuel duel, bool enterBattle, out string message)
    {
        if (Environment.GetEnvironmentVariable("YUGIRE_HISTORICAL_CARD_TESTS") != "1")
            throw new InvalidOperationException("Use explicit queued phase admission or an observed checkpoint.");
        return TurnFixture.Battle(duel, enterBattle, out message);
    }

    // Historical source compatibility only; no parallel combat or response execution.
    internal static bool Attack(this DemoDuel duel, int source, int target, out string message)
    {
        RequireHistoricalCombat();
        return DrainCombat(duel, duel.TryQueueAttack(source, target, out message, ActionOrigin.Test));
    }

    internal static bool PassAttackResponse(this DemoDuel duel, out string message)
    {
        RequireHistoricalCombat();
        message = "No queued attack response.";
        return duel.PendingAttackResponse is { } request &&
            DrainCombat(duel, duel.TryQueueAttackResponse(request, null, out message, ActionOrigin.Test));
    }

    internal static bool ActivateAttackTrap(this DemoDuel duel, int owner, int zone, out string message)
    {
        RequireHistoricalCombat();
        message = "No matching queued attack Trap.";
        return duel.PendingAttackResponse is { } request &&
            request.EligibleTraps.FirstOrDefault(option => option.Owner == owner && option.Zone == zone) is { } selected &&
            DrainCombat(duel, duel.TryQueueAttackResponse(request, selected, out message, ActionOrigin.Test));
    }

    private static void RequireHistoricalCombat()
    {
        if (Environment.GetEnvironmentVariable("YUGIRE_HISTORICAL_CARD_TESTS") != "1")
            throw new InvalidOperationException("Use explicit queued attack admission in active fixtures.");
    }

    private static bool DrainCombat(DemoDuel duel, bool admitted)
    {
        if (!admitted) return false;
        var queue = duel.Actions;
        for (int tick = 0; tick < 1000 && queue.IsBusy; tick++) queue.Tick();
        if (queue.IsBusy || queue.Fault is not null)
            throw new InvalidOperationException("Historical combat fixture did not drain.");
        return true;
    }

    internal static bool Draw(this DemoDuel duel, out string message)
    {
        if (Environment.GetEnvironmentVariable("YUGIRE_HISTORICAL_CARD_TESTS") != "1")
            throw new InvalidOperationException("Use explicit queued turn-draw admission in active fixtures.");
        if (!duel.TryQueueTurnDraw(out message, ActionOrigin.Test))
            return false;

        var queue = duel.Actions;
        for (int tick = 0; tick < 1000 && queue.IsBusy; tick++)
            queue.Tick();
        if (queue.IsBusy || queue.Fault is not null)
            throw new InvalidOperationException("Historical fixture did not drain the turn draw.");
        return true;
    }
}
