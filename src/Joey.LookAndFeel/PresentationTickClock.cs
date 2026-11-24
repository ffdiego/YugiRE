namespace Joey.LookAndFeel;

/// <summary>Converts render elapsed time to whole presentation updates, retaining sub-update time.</summary>
public sealed class PresentationTickClock
{
    private double remainder;

    public int Advance(TimeSpan elapsed, DuelLookAndFeel style)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!style.PlayAnimations)
        {
            Reset();
            return 0;
        }

        double rate = style.UpdatesPerSecond;
        double units = remainder + elapsed.Ticks * rate;
        if (!double.IsFinite(units))
        {
            Reset();
            return int.MaxValue;
        }

        remainder = units % TimeSpan.TicksPerSecond;
        return (int)Math.Min(int.MaxValue, Math.Floor(units / TimeSpan.TicksPerSecond));
    }

    public void Reset() => remainder = 0;
}
