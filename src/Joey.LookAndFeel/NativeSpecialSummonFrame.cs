namespace Joey.LookAndFeel;

/// <summary>
/// Special-summon render parameters from native Update 004500E0 and
/// Draw 00450100. Not pixel-perfect: the source object at +0x108,
/// 0054E890 transform and sprite blending remain to be reconstructed.
/// </summary>
public readonly record struct NativeSpecialSummonFrame(
    int Counter,
    int? SpiralFrame,
    int SpiralOpacity,
    bool DrawCardOverlay,
    int OverlayScaleFixed8)
{
    public const int UpdateIncrement = 8;
    public const int Updates = 41; // t=0,8,...320; next Update t=328 deactivates.
    public const int SpiralFrameCount = 4;

    public static NativeSpecialSummonFrame FromUpdate(int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= Updates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));

        int counter = update * UpdateIncrement;
        // 00450100: a four-frame repeating spiral while t<0xC0.
        int? spiral = counter < 0xC0 ? (counter >> 4) & 3 : null;
        int opacity = spiral.HasValue
            ? Math.Clamp(cosineFixed16[(counter - 0x60) & 511] >> 8, 0, 255)
            : 0;

        // Second native pass: from t>64 until t<320, a 0054E890
        // transformed card image at object+0x108; not a generic halo.
        bool overlay = counter > 0x40 && counter < 0x140;
        int scale = overlay ? 0x100 - (cosineFixed16[(counter - 0xC0) & 511] >> 13) : 0;
        return new(counter, spiral, opacity, overlay, scale);
    }
}
