using Joey.Core;

// Test preparation uses the real scheduler. Keep every setup command in the
// trace; callers configure any presentation gate before the first admission.
internal static class TurnFixture
{
    internal static bool End(DemoDuel duel, out string message)
    {
        if (!duel.TryQueueEndTurn(out message, ActionOrigin.Test)) return false;
        var queue = duel.Actions;
        for (int tick = 0; tick < 1000 && queue.IsBusy; tick++)
            queue.Tick();
        if (queue.IsBusy || queue.Fault is not null)
            throw new InvalidOperationException("Fixture end-turn queue did not drain.");
        return true;
    }

    internal static bool Battle(DemoDuel duel, bool enterBattle, out string message)
    {
        if (!duel.TryQueueBattlePhaseChange(enterBattle, out message, ActionOrigin.Test)) return false;
        var queue = duel.Actions;
        for (int tick = 0; tick < 1000 && queue.IsBusy; tick++)
            queue.Tick();
        if (queue.IsBusy || queue.Fault is not null)
            throw new InvalidOperationException("Fixture battle-phase queue did not drain.");
        return duel.Phase == (enterBattle ? DemoPhase.Battle : DemoPhase.Main);
    }

    internal static void Advance(DemoDuel duel)
    {
        int turn = duel.Turn;
        if (!End(duel, out var message) || duel.Turn != turn + 1)
            throw new InvalidOperationException("Fixture turn did not advance: " + message);
    }
}
