namespace Joey.LookAndFeel;

public readonly record struct ExplosionSpritePass(int ResourceIndex, int RotationIndex, int ScaleFixed16, int Opacity)
{
    // 00422C00 applies MulDiv to the extent (pixel count - 1).
    public int ScaledPixels(int pixels)
    {
        int extent = (int)(((pixels - 1L) * ScaleFixed16 + 32768) >> 16);
        return extent > 0 ? extent + 1 : 0;
    }
}

/// <summary>Draw arguments in native composition order at 004514B0.</summary>
public readonly record struct NativeExplosionFrame(
    ExplosionSpritePass? Flare, ExplosionSpritePass? Fragments, ExplosionSpritePass? Initial)
{
    public static NativeExplosionFrame FromCounter(int counter, ReadOnlySpan<int> cosineFixed16)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counter);
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));

        ExplosionSpritePass? flare = counter is >= 0x20 and < 0x90
            ? new(1, 0, (counter + 0x60) << 8, Math.Min(255, counter + 0x60))
            : null;

        int fragmentIndex = counter is >= 0x80 and < 0xD0
            ? 2 + (counter - 0x80) * 5 / 64
            : 7;
        ExplosionSpritePass? fragments = fragmentIndex < 7
            ? new(fragmentIndex, 0, 0x10000, 255)
            : null;

        ExplosionSpritePass? initial = counter < 0x80
            ? new(0, counter, (counter + 0x80) << 8,
                Math.Clamp(cosineFixed16[(counter - 0x40) & 511] >> 8, 0, 255))
            : null;
        return new(flare, fragments, initial);
    }
}
