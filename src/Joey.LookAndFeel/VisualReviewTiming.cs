namespace Joey.LookAndFeel;

/// <summary>
/// Isolated pause/step policy for deterministic visual review. Does not change
/// simulation rules or assume a native game clock frequency.
/// </summary>
public static class VisualReviewTiming
{
    /// <summary>
    /// While paused, render without advancing. A requested step advances one
    /// nominal presentation tick for the selected client speed profile.
    /// The native Update/Draw ordering may differ and must be verified.
    /// </summary>
    public static TimeSpan Elapsed(TimeSpan realElapsed, DuelLookAndFeel style,
                                    bool paused, bool stepRequested)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!paused)
            return TimeSpan.FromSeconds(Math.Clamp(realElapsed.TotalSeconds, 0, 1));
        if (!stepRequested || !style.PlayAnimations)
            return TimeSpan.Zero;

        // Round up one unit in 100ns ticks so an idle PresentationTickClock
        // definitely advances one update instead of returning zero.
        double ticks = Math.Ceiling(TimeSpan.TicksPerSecond / style.UpdatesPerSecond);
        return TimeSpan.FromTicks(Math.Max(1, checked((long)ticks)));
    }
}
