namespace Joey.LookAndFeel;

/// <summary>
/// Numeric glyphs drawn beside status A/B (0041A070 and 0041A3B0).
/// Both select number.txt resource #8 through 00417F40:
/// j/duel/font/duel_fontm_y.bmp, 10 columns. The original DAT metadata
/// confirms a 70x10 bitmap (7x10 per digit).
///
/// Unlike field ATK/DEF, these Draws use the non-opacity path:
/// 00480690 centers a digit group; 004803C0 draws right-to-left from
/// a right-edge cursor. This class captures glyph positions only and
/// does not assert the missing status icon x-position/texture geometry.
/// </summary>
public static class NativeCardStatusDigits
{
    public const int OriginalFontResourceIndex = 8;
    public const string FontResource = @"j\duel\font\duel_fontm_y.bmp";
    public const int Columns = 10;
    public const int EvidenceAtlasWidth = 70;
    public const int EvidenceAtlasHeight = 10;

    /// <summary>
    /// 00480690: measure digits via 00480230, translate the right edge
    /// to centerX + measuredWidth/2, translate top to centerY-height/2,
    /// and delegate to 004803C0. The order of emitted passes is the
    /// native order: units first, then tens, hundreds, etc.
    /// </summary>
    public static NativeCardStatusDigitPass[] Centered(
        CardPoint center, int value, int atlasWidth, int atlasHeight)
    {
        int glyphWidth = ValidatedGlyphWidth(atlasWidth, atlasHeight);
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        int length = DecimalLength(value);
        int rightEdge = checked(center.X + (length * glyphWidth) / 2);
        return RightAnchored(rightEdge, center.Y, value, atlasWidth, atlasHeight);
    }

    /// <summary>
    /// 004803C0: subtract glyph advance before every draw, extract
    /// digit (%10), then divide by ten until zero. Y is the vertical
    /// center for status B: 0041A3B0 subtracts fontHeight/2 before
    /// calling 004803C0; pass through the shared center contract.
    /// </summary>
    public static NativeCardStatusDigitPass[] RightAnchored(
        int rightEdge, int centerY, int value, int atlasWidth, int atlasHeight)
    {
        int glyphWidth = ValidatedGlyphWidth(atlasWidth, atlasHeight);
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        int topY = centerY - atlasHeight / 2;
        var passes = new List<NativeCardStatusDigitPass>(DecimalLength(value));
        int x = rightEdge;
        do
        {
            x = checked(x - glyphWidth);
            int digit = value % 10;
            passes.Add(new NativeCardStatusDigitPass(
                digit,
                NativeCardStatusGeometry.SourceFrame(atlasWidth, atlasHeight, Columns, digit),
                new CardPoint(x, topY)));
            value /= 10;
        } while (value != 0);
        return [.. passes];
    }

    /// <summary>
    /// Symbolic x formulas visible in the imperfect Ghidra decompilation.
    /// EBX is deliberately an INPUT, not assumed to be a particular
    /// sprite's width: its provenance needs assembly register tracing.
    ///
    /// 0041A070 calls 00480690 at objX - EBX/2 + iStack_6c + 6;
    /// the decompiler does not prove where iStack_6c was last written.
    /// 0041A3B0 calls 004803C0 at objX + EBX/2 - 3.
    /// These formulas must NOT be used for final client placement until
    /// EBX and the post-draw offset are proved for each pass.
    /// </summary>
    public static NativeCardStatusHorizontalOperands HorizontalFromDecompilerOperands(
        int objectX, int firstStatusEbx, int firstPostDrawOffset, int secondStatusEbx) =>
        new(checked(objectX - firstStatusEbx / 2 + firstPostDrawOffset + 6),
            checked(objectX + secondStatusEbx / 2 - 3));

    private static int ValidatedGlyphWidth(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (width % Columns != 0)
            throw new ArgumentException("Yellow status font must contain ten full columns.", nameof(width));
        return width / Columns;
    }

    private static int DecimalLength(int value)
    {
        int count = 1;
        while (value >= 10) { count++; value /= 10; }
        return count;
    }
}

/// <summary>One native glyph source crop and the top-left of its destination.</summary>
public readonly record struct NativeCardStatusDigitPass(
    int Digit, CardBounds Source, CardPoint Destination);

/// <summary>
/// Symbolic X operands from 0041A070/0041A3B0. Their EBX provenance is
/// unresolved; this is an algebra check, not absolute visual geometry.
/// </summary>
public readonly record struct NativeCardStatusHorizontalOperands(
    int FirstCenteredDigitX, int SecondRightEdgeX);
