namespace Joey.LookAndFeel;

/// <summary>
/// Floating LP digit motion from FUN_004806F0, reached by Draw 0047C660.
/// Positions are relative to the right-aligned number anchor. All timings
/// are native counter values, not wall-clock milliseconds.
/// </summary>
public readonly record struct NativeFloatingLifeGlyph(
    int GlyphIndex, int XOffsetPixels, int YOffsetPixels, int Phase);

public static class NativeFloatingLifeDigits
{
    public const int StaticFromCounter = 0x100;

    /// <summary>
    /// 0047C660 supplies the LP ramp * 2; 004806F0 delays later (leftward)
    /// characters by 64/(numberOfGlyphs-1) counter units, and uses two
    /// cosine-driven Y excursions before the static 256+ branch.
    /// Glyph indexes 10/11 are optional plus/minus from 12-column atlases.
    /// </summary>
    public static IReadOnlyList<NativeFloatingLifeGlyph> Sample(
        int pendingDelta, int floatCounter, int glyphWidth, int glyphHeight,
        int columns, ReadOnlySpan<int> cosineFixed16, bool showPositiveSign = true)
    {
        if (glyphWidth <= 0 || glyphHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(glyphWidth));
        if (columns is < 10 or > 12)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));
        if (floatCounter < 0)
            throw new ArgumentOutOfRangeException(nameof(floatCounter));
        if (pendingDelta == 0 || floatCounter == 0)
            return [];

        long magnitude = Math.Abs((long)pendingDelta);
        List<int> digits = [];
        do
        {
            digits.Add((int)(magnitude % 10));
            magnitude /= 10;
        } while (magnitude > 0);
        if (columns == 12 && (pendingDelta < 0 || showPositiveSign))
            digits.Add(pendingDelta < 0 ? 11 : 10);

        int delay = digits.Count < 2 ? 0 : 0x40 / (digits.Count - 1);
        var result = new List<NativeFloatingLifeGlyph>(digits.Count);
        for (int i = 0; i < digits.Count; i++)
        {
            int phase = floatCounter - delay * i;
            if (phase <= 0) continue;
            int yOffset = 0;
            if (phase < 0x80)
            {
                int cosine = cosineFixed16[(phase * 2 - 0x80) & 511];
                yOffset = -(int)((long)cosine * glyphHeight >> 16);
            }
            else if (phase < 0xC0)
            {
                int cosine = cosineFixed16[(phase * 4 - 0x80) & 511];
                yOffset = -(int)((long)cosine * (glyphHeight / 4) >> 16);
            }
            result.Add(new(digits[i], -(i + 1) * glyphWidth, yOffset, phase));
        }
        return result;
    }
}
