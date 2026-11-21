namespace Joey.LookAndFeel;

/// <summary>
/// Native normal-summon Update/Draw samples from joey_pc.exe
/// 0044F9F0 / 0044FA10. The decompiler exposes two draw windows but does
/// not establish pixel-exact clipping, texture orientation or blend mode.
/// </summary>
public enum NativeNormalSummonStage { ExpandMiniature, WrappedMiniature }

public readonly record struct NativeNormalSummonFrame(
    int Counter, NativeNormalSummonStage Stage, int ScaleFixed16,
    int Opacity, int ScrollPixels)
{
    public const int UpdateIncrement = 8;
    public const int Updates = 33; // t=0,8,...256; next Update t=264 deactivates.

    public static NativeNormalSummonFrame FromUpdate(int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= Updates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine entries.", nameof(cosineFixed16));

        int counter = update * UpdateIncrement;
        // 0044FA10: first branch when (counter+0x40)*0x100 <= 0x8000.
        if (counter <= 0x40)
        {
            int scale = (counter + 0x40) * 0x100;
            int opacity = Math.Clamp(cosineFixed16[(counter + 0x38) & 511] >> 8, 0, 255);
            return new(counter, NativeNormalSummonStage.ExpandMiniature, scale, opacity, 0);
        }

        // 0044FA10 second branch: native opacity and source scroll counter.
        // Source rectangle/wrapping depends on sprite dimensions and renderer
        // internals not fully recovered from the decompiler.
        int fade = Math.Clamp((int)((long)cosineFixed16[(counter - 0xA0) & 511] * 3 >> 10), 0, 255);
        int scroll = counter * 5 / 8;
        return new(counter, NativeNormalSummonStage.WrappedMiniature, 0x10000, fade, scroll);
    }

    public int WrappedSourceOffset(int height)
    {
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        return ScrollPixels % height;
    }
}
