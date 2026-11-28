namespace Joey.LookAndFeel;

/// <summary>
/// The part of the disabled-chain Draw contract recovered from 004531C0.
/// This is a frame INDEX, not a DAT image name or a screen-space draw pass.
/// Assembly 0045324E tests 0x40 in the high byte of the descriptor,
/// i.e. 0x4000 in the complete word. The rotated branch passes 90
/// degrees and 0x10000 scale; the normal branch draws the whole image.
/// </summary>
public readonly record struct NativeDisabledChainFrame(
    int Counter, int FrameIndex, bool DescriptorBit4000)
{
    // 004531E7 resolves the object center via 0045C140.
    // 00453254..00453279 swaps half-extents for the 90-degree draw;
    // 0045328B..004532B0 uses the unrotated image half-extents.
    public int Degrees => DescriptorBit4000 ? 90 : 0;

    public CardBounds BoundsAt(CardPoint center, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth));
        if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight));
        int width = DescriptorBit4000 ? imageHeight : imageWidth;
        int height = DescriptorBit4000 ? imageWidth : imageHeight;
        return new(center.X - width / 2, center.Y - height / 2, width, height);
    }

    public static NativeDisabledChainFrame FromCounter(int counter, uint descriptorFlags)
    {
        if (counter is < 0 or >= NativeDisabledChainTimeline.Updates)
            throw new ArgumentOutOfRangeException(nameof(counter));
        return new(counter, (counter / 2) % NativeDisabledChainTimeline.FrameCount,
            (descriptorFlags & 0x4000u) != 0);
    }
}

/// <summary>
/// Counter-only model of 00452F20 / 004531A0 / 004531C0 (vtable 005DB374).
/// The native Update increments +1 and deactivates at t=24; the Draw samples
/// six indices at two updates each, then repeats. Update cadence is not FPS.
/// The Draw's center-relative bounds are known; resource identity,
/// absolute center resolution, trigger and input blocking remain pending.
/// </summary>
public sealed class NativeDisabledChainTimeline
{
    public const int Updates = 0x18;
    public const int FrameCount = 6;

    public int Counter { get; private set; }
    public bool Active { get; private set; } = true;
    // 005DB378 points to 00403080 (return obj+4 active).
    public bool BlocksRelease => Active;

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

