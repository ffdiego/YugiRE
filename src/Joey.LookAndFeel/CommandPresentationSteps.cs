namespace Joey.LookAndFeel;

/// <summary>Visual substeps owned by ONE active command, never a backlog of duel actions.
/// Opening a second command or releasing an unfinished scope is a contract error.
/// Legacy opening/CPU actions must use their own separate presentation sequence.</summary>
public sealed class CommandPresentationSteps<T>
{
    private readonly PresentationSequence<T> steps = new();
    public long? Sequence { get; private set; }

    public void Begin(long sequence)
    {
        if (sequence <= 0 || Sequence is not null || steps.Current is not null)
            throw new InvalidOperationException("Previous command visual steps are still active.");
        Sequence = sequence;
    }

    public PresentationSequence<T> For(long sequence) =>
        Sequence == sequence ? steps : throw new InvalidOperationException("Visual steps do not belong to this command.");

    public void Release(long sequence)
    {
        if (For(sequence).Current is not null)
            throw new InvalidOperationException("Cannot release unfinished command presentation.");
        Sequence = null;
    }

    public void Reset()
    {
        steps.Clear();
        Sequence = null;
    }
}
