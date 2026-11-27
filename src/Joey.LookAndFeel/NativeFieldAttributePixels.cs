namespace Joey.LookAndFeel;

/// <summary>V05.3/V05.4: rectangular glyph composition observed with
/// RGB565 source and destination. This is NOT the complementary-weight
/// intensity compositor used by other Draw families.</summary>
public static class NativeFieldAttributePixels
{
    public const ushort ObservedKey = 0x8D85;

    public static ushort Pack(byte red, byte green, byte blue) =>
        (ushort)(((red >> 3) << 11) | ((green >> 2) << 5) | (blue >> 3));

    /// <summary>Prepare a point-filtered RGBA8 atlas for the client.
    /// Key comparisons precede modulation, in the packed source domain.
    /// Opaque pixels use expanded RGB565 and binary alpha. The expansion
    /// replicates high bits for display; native packed values are exact,
    /// not a claim about the display backend's RGB888 conversion.</summary>
    public static void Prepare(Span<byte> rgba, int intensity)
    {
        if (intensity is not (NativeFieldAttributeFrame.ActiveOpacity or NativeFieldAttributeFrame.InactiveOpacity))
            throw new ArgumentOutOfRangeException(nameof(intensity));
        if (rgba.Length < 4 || rgba.Length % 4 != 0)
            throw new ArgumentException("Expected a nonempty RGBA8 atlas.", nameof(rgba));
        ushort key = Pack(rgba[0], rgba[1], rgba[2]);
        if (key != ObservedKey)
            throw new InvalidDataException("Unexpected field attribute atlas corner key.");
        for (int i = 0; i < rgba.Length; i += 4)
        {
            ushort pixel = Pack(rgba[i], rgba[i + 1], rgba[i + 2]);
            if (pixel == key)
            {
                rgba.Slice(i, 4).Clear();
                continue;
            }
            if (intensity == NativeFieldAttributeFrame.InactiveOpacity)
                pixel = NativeIntensityPixel.ScalePacked(pixel, NativeSurfaceFormat.Rgb565, intensity >> 4);
            int r = pixel >> 11, g = (pixel >> 5) & 63, b = pixel & 31;
            rgba[i] = (byte)((r << 3) | (r >> 2));
            rgba[i + 1] = (byte)((g << 2) | (g >> 4));
            rgba[i + 2] = (byte)((b << 3) | (b >> 2));
            rgba[i + 3] = 255;
        }
    }
}
