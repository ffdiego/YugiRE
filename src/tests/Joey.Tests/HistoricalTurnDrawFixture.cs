using Joey.Core;

// Temporary source-compatibility for opt-in historical fixtures only.
// No direct mutation, new queue, trace reset or gate reconfiguration.
// Historical fixtures which assumed those things must be retired or
// migrated; the active suite must use explicit typed admission instead.
internal static class HistoricalTurnDrawFixture
{
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
