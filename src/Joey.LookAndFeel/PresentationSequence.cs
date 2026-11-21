namespace Joey.LookAndFeel;

public readonly record struct PresentationStep<T>(T Kind, int Updates);

/// <summary>A local FIFO of visual steps. Advancing it only changes which sample the renderer should draw.</summary>
public sealed class PresentationSequence<T>
{
    private readonly Queue<PresentationStep<T>> pending = new();

    public PresentationStep<T>? Current => pending.TryPeek(out var step) ? step : null;
    public int ElapsedUpdates { get; private set; }

    public void Enqueue(T kind, int updates)
    {
        if (updates <= 0) throw new ArgumentOutOfRangeException(nameof(updates));
        pending.Enqueue(new(kind, updates));
    }

    public void Advance(int updates)
    {
        if (updates < 0) throw new ArgumentOutOfRangeException(nameof(updates));
        while (updates > 0 && pending.TryPeek(out var step))
        {
            int consumed = Math.Min(updates, step.Updates - ElapsedUpdates);
            ElapsedUpdates += consumed;
            updates -= consumed;
            if (ElapsedUpdates == step.Updates)
            {
                pending.Dequeue();
                ElapsedUpdates = 0;
            }
        }
    }

    public void Clear()
    {
        pending.Clear();
        ElapsedUpdates = 0;
    }
}
