namespace Joey.LookAndFeel;

/// <summary>
/// Animation counter for status A/B on a settled native card object.
/// Command 0x60/0x69 starts state 3/4 at 0x200 (0040B270/0040B370).
/// 00418B60 decreases obj+0x1C by 0x10 once per native Update while
/// obj+0x14 == 0x100; on zero it clears obj+0x18 (state) and counter.
///
/// Crucially the counter runs BACKWARD: the tail strip at t >= 0x100
/// comes first, followed by the main strip at 0 &lt; t &lt; 0x100.
/// The renderer's clock/sample cadence still awaits native validation.
/// </summary>
public readonly record struct NativeCardStatusTransition(int State, int Counter)
{
    public const int StartCounter = 0x200;
    public const int Step = 0x10;
    public const int TotalUpdates = StartCounter / Step;

    public static NativeCardStatusTransition First() =>
        new(NativeCardStatusOverlay.FirstTransitionState, StartCounter);

    public static NativeCardStatusTransition Second() =>
        new(NativeCardStatusOverlay.SecondTransitionState, StartCounter);

    public bool Active => State is NativeCardStatusOverlay.FirstTransitionState or
        NativeCardStatusOverlay.SecondTransitionState && Counter > 0;

    public NativeCardStatusTransition Advance(bool cardSettled)
    {
        if (!cardSettled || !Active) return this;
        // Native initialization always uses 0x200, multiple of 16.
        // Guard malformed external state instead of underflowing forever.
        if (Counter < Step || Counter % Step != 0)
            throw new InvalidOperationException("Invalid native card status animation counter.");
        int next = Counter - Step;
        return next == 0 ? default : new(State, next);
    }
}
