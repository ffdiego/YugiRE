namespace Joey.LookAndFeel;

/// <summary>Diagnostic transport at the observed native 30/60 Update/Draw
/// cadence. A slow host never skips a native sample; at most one per frame.</summary>
public sealed class NativeReviewClock
{
    public int FramesPerSecond { get; private set; }
    private double accumulated;
    public void Pause() { FramesPerSecond=0; accumulated=0; }
    public void Toggle(int fps)
    {
        if(fps is not (30 or 60)) throw new ArgumentOutOfRangeException(nameof(fps));
        if(FramesPerSecond==fps) { Pause(); return; }
        FramesPerSecond=fps;
        accumulated=0;
    }
    public bool Advance(double elapsedSeconds)
    {
        if(FramesPerSecond==0) return false;
        double interval=1.0/FramesPerSecond;
        accumulated+=Math.Clamp(elapsedSeconds,0,interval);
        if(accumulated+1e-9<interval) return false;
        accumulated=Math.Max(0,accumulated-interval);
        return true;
    }
}
