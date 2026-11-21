namespace Joey.LookAndFeel;

/// <summary>
/// Static HUD subcontracts supported by 0047C660 (LP Draw) and
/// 004803C0/00480200 (numeric atlas Draw). All coordinates and blend choices
/// are still client presentation details until compared with native frames.
/// </summary>
public static class NativeLifeHud
{
    public const int BarFullAtLife = 8000;

    /// <summary>
    /// 0047C660 limits obj+24h to 8000 before multiplying by bar width and
    /// dividing by 8000. Use 64-bit intermediates to avoid overflow.
    /// </summary>
    public static int FilledBarPixels(int displayedLife, int barWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(barWidth);
        int clamped = Math.Clamp(displayedLife, 0, BarFullAtLife);
        return (int)((long)clamped * barWidth / BarFullAtLife);
    }

    /// <summary>
    /// The native numeric Draw reads the atlas glyph count at object+8 and
    /// selects glyphs by source X = (value % 10) * (textureWidth / count).
    /// The glyph count is loaded from duel/font/number.txt in the original
    /// resource loader. Do not guess it when the manifest is unavailable.
    /// </summary>
    public static int? TryFontColumns(string manifest, string bitmapName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(bitmapName);
        string expected = System.IO.Path.GetFileName(bitmapName.Replace('\\', '/'));
        foreach (string raw in manifest.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) continue;
            string file = System.IO.Path.GetFileName(words[0].Replace('\\', '/'));
            if (!file.Equals(expected, StringComparison.OrdinalIgnoreCase)) continue;

            // Native atlases have decimal glyphs 0..9 and optionally +/−.
            // Unrecognized descriptors are deliberately not treated as proof.
            if (int.TryParse(words[1], out int count) && count is >= 10 and <= 12)
                return count;
        }
        return null;
    }

    public static int GlyphWidth(int textureWidth, int glyphColumns)
    {
        if (textureWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(textureWidth));
        if (glyphColumns is < 10 or > 12 || textureWidth % glyphColumns != 0)
            throw new ArgumentException("The DAT bitmap must be a horizontal 10..12-glyph atlas.",
                                        nameof(glyphColumns));
        return textureWidth / glyphColumns;
    }

    public static int[] DecimalGlyphs(int displayedLife)
    {
        int value = Math.Clamp(displayedLife, 0, NativeLifePointPresentation.MaximumDisplayed);
        if (value == 0) return [0];
        List<int> result = [];
        while (value != 0)
        {
            result.Add(value % 10);
            value /= 10;
        }
        result.Reverse();
        return [.. result];
    }
}
