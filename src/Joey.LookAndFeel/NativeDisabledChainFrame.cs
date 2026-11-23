namespace Joey.LookAndFeel;

/// <summary>
/// The part of the disabled-chain Draw contract recovered from 004531C0.
/// This is a frame INDEX, not a DAT image name or a screen-space draw pass.
/// The descriptor bit 0x40 selects a distinct native composition branch;
/// neither branch's geometry or blending has been established yet.
/// </summary>
public readonly record struct NativeDisabledChainFrame(
    int Counter, int FrameIndex, bool DescriptorBit40)
{
    public static NativeDisabledChainFrame FromCounter(int counter, uint descriptorFlags)
    {
        if (counter is < 0 or >= NativeDisabledChainTimeline.Updates)
            throw new ArgumentOutOfRangeException(nameof(counter));
        return new(counter, (counter / 2) % NativeDisabledChainTimeline.FrameCount,
            (descriptorFlags & 0x40u) != 0);
    }
}

/// <summary>
/// Counter-only model of 00452F20 / 004531A0 / 004531C0 (vtable 005DB374).
/// The native Update increments +1 and deactivates at t=24; the Draw samples
/// six indices at two updates each, then repeats. Update cadence is not FPS.
/// No image, coordinates, trigger, draw order or input blocking is inferred.
/// </summary>
public sealed class NativeDisabledChainTimeline
{
    public const int Updates = 0x18;
    public const int FrameCount = 6;

    public int Counter { get; private set; }
    public bool Active { get; private set; } = true;

    public NativeDisabledChainFrame? Sample(uint descriptorFlags) =>
        Active ? NativeDisabledChainFrame.FromCounter(Counter, descriptorFlags) : null;

    public void AdvanceNativeUpdate()
    {
        if (!Active) return;
        Counter++;
        Active = Counter < Updates;
    }

    public void Reset()
    {
        Counter = 0;
        Active = true;
    }
}

