namespace Joey.LookAndFeel;

/// <summary>One presentation owned by the current logical command.
/// No queue of logical actions, no clock and no reference to Core.
/// Existing visual timelines are driven by the host's frame updates.</summary>
public sealed class ActiveCommandPresentation<T> where T : class
{
    public long? Sequence { get; private set; }
    public T? Snapshot { get; private set; }
    public bool Started { get; private set; }

    public void Capture(long sequence, T? snapshot)
    {
        if (sequence <= 0 || Sequence is not null)
            throw new InvalidOperationException("Previous command presentation not released.");
        Sequence = sequence;
        Snapshot = snapshot;
        Started = false;
    }

    public bool TryRelease(long sequence, bool applied, Action<T> begin, Func<bool> busy)
    {
        if (Sequence != sequence)
            throw new InvalidOperationException("Presentation does not belong to the active command.");
        if (!Started)
        {
            Started = true;
            if (applied && Snapshot is { } captured) begin(captured);
        }
        if (busy()) return false;
        Sequence = null;
        Snapshot = null;
        Started = false;
        return true;
    }
}
