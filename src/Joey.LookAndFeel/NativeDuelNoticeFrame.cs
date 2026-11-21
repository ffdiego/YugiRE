namespace Joey.LookAndFeel;

/// <summary>
/// Native opening notice, Update 00451950 and Draw 00451980.
/// The Draw itself releases its effect after counter >= 0x140.
/// </summary>
public readonly record struct NativeDuelStartFrame(int Counter, int ScaleFixed8, int Opacity)
{
    public const int UpdateIncrement = 8;
    public const int Updates = 40; // live draw samples t=0,8,...312.

    public static NativeDuelStartFrame FromUpdate(int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= Updates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected a 512-entry cosine x16.16 table.",
                                        nameof(cosineFixed16));
        int counter = update * UpdateIncrement;
        if (counter < 0x40)
        {
            // 00451980: first 64 counter units expand/settle the logo.
            int scale = 0x100 + (cosineFixed16[counter * 2] >> 9);
            int fadeIn = Math.Clamp(cosineFixed16[(counter * 2 - 0x80) & 511] >> 8, 0, 255);
            return new(counter, scale, fadeIn);
        }
        if (counter <= 0x100)
            return new(counter, 0x100, 255);

        int fadeOut = Math.Clamp(cosineFixed16[(counter * 2) & 511] >> 8, 0, 255);
        return new(counter, 0x100, fadeOut);
    }
}

/// <summary>
/// Duel-result banner native 00451E40 / 00451E60. Draw stays centered at
/// (400,300) and fades in over 128 counter units. The native Update stops
/// at 0x200, so the later fade-out branch in Draw is not assumed reachable
/// here; the client holds the result while the game-over screen is open.
/// </summary>
public readonly record struct NativeDuelEndFrame(int Counter, int Opacity)
{
    public const int UpdateIncrement = 4;
    public const int MaxCounter = 0x200;
    public const int FadeInUpdates = 0x80 / UpdateIncrement;

    public static NativeDuelEndFrame FromUpdate(int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update < 0)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected a 512-entry cosine x16.16 table.",
                                        nameof(cosineFixed16));
        int counter = (int)Math.Min((long)update * UpdateIncrement, MaxCounter);
        int opacity = counter < 0x80
            ? Math.Clamp(cosineFixed16[(counter - 0x80) & 511] >> 8, 0, 255)
            : 255;
        return new(counter, opacity);
    }
}
