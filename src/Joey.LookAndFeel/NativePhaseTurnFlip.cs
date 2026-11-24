namespace Joey.LookAndFeel;

/// <summary>Turn-change atlas selection at 004814AD–0048164A, expressed in updates.</summary>
public static class NativePhaseTurnFlip
{
    private const int CounterIncrement = 8;
    public const int Updates = 0x200 / CounterIncrement;
    public const int FrameCount = 9;
    private const int FirstFrameDelay = 0x40 / CounterIncrement;
    private const int Stagger = 0x18 / CounterIncrement;

    // No frame means the button still uses the previous side's static image.
    public static int? Frame(int button, int elapsedUpdates)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)button, (uint)DuelSurfaceLayout.PhaseCount);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedUpdates);
        int local = elapsedUpdates - button * Stagger - FirstFrameDelay;
        return local < 0 ? null : Math.Max(0, FrameCount - 1 - local);
    }
}
