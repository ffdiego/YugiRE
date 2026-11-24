namespace Joey.LookAndFeel;

/// <summary>
/// Static HUD subcontracts supported by 0047C660 (LP Draw) and
/// 004803C0/00480200 (numeric atlas Draw). All coordinates and blend choices
/// are still client presentation details until compared with native frames.
/// </summary>
public static class NativeLifeHud
{
    public const int BarFullAtLife = 8000;

    // The DAT's verified LP fontl BMP is 80x12 with ten 8x12 glyphs.
    // Native screenshot shows a roughly 3x pixel scale for these digits.
    // This scale and anchors are PAIR-CAPTURE estimates, not verified
    // arguments from Draw 0047C660. Keep the multiplier in LookAndFeel.
    public const int DisplayGlyphScale = 3;
    public const int DisplayGlyphHeight = 12 * DisplayGlyphScale;
    public const int DisplayRightX = 195;
    // Preserve the previous visible bottom edges as size increases 32→36.
    public const int DisplayTopY = -1;
    public const int DisplayBottomY = 532;

    public static int DisplayGlyphWidth(int atlasWidth, int atlasHeight, int columns)
    {
        if (atlasHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(atlasHeight));
        // An integer scale prevents uneven fractional rasterization of
        // the native 8px bitmap glyphs (previously 21.3333px at 32px).
        return GlyphWidth(atlasWidth, columns) * DisplayGlyphScale;
    }

    /// <summary>
    /// An unverified, stable overlay anchor on the duel board. It is derived
    /// from existing card-row centres rather than the LP sidebar: near
    /// (525, 406), far (525, 194), between monster and spell/trap zones.
    /// </summary>
    public static CardPoint FloatingNumberAnchor(bool nearSide)
    {
        var monster = DuelCardLayout.Center(DuelCardZone.Monster, nearSide, 2);
        var spell = DuelCardLayout.Center(DuelCardZone.Spell, nearSide, 2);
        return new((monster.X + spell.X) / 2, (monster.Y + spell.Y) / 2);
    }

    public static int DisplayHeightForAtlas(int atlasHeight)
    {
        if (atlasHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(atlasHeight));
        return atlasHeight * DisplayGlyphScale;
    }

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
