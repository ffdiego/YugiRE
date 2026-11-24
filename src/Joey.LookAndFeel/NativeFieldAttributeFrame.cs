namespace Joey.LookAndFeel;

/// <summary>
/// The three medium numeric atlases in j/duel/font/number.txt.
/// 004194D0 selects resource indices 1 (normal), 2 (increased) and
/// 3 (decreased) through 00417F40. Their manifest columns are 11/10/10.
/// The default's eleventh glyph (index 10) is used as the separator.
/// </summary>
public static class NativeFieldAttributeFonts
{
    public const string Normal = @"j\duel\font\duel_fontm.bmp";
    public const string Increased = @"j\duel\font\duel_fontm_b.bmp";
    public const string Decreased = @"j\duel\font\duel_fontm_r.bmp";
    public const int NormalColumns = 11;
    public const int ModifiedColumns = 10;
    public const int SeparatorIndex = 10;

    public static string ForValue(int displayed, int original) =>
        displayed > original ? Increased :
        displayed < original ? Decreased : Normal;

    public static int Columns(string resource) =>
        resource.Equals(Normal, StringComparison.OrdinalIgnoreCase) ? NormalColumns :
        resource.Equals(Increased, StringComparison.OrdinalIgnoreCase) ||
        resource.Equals(Decreased, StringComparison.OrdinalIgnoreCase) ? ModifiedColumns :
        throw new ArgumentException("Unknown field attribute font resource.", nameof(resource));
}

public readonly record struct NativeFieldNumberPass(
    string Sprite, int Columns, int Value, int CenterX, int TopY, int Opacity)
{
    /// <summary>
    /// 00480500 centers the right-to-left glyph sequence using
    /// integer width/2. This is not always identical to center-width/2
    /// when the total glyph width is odd.
    /// </summary>
    public int LeftX(int glyphWidth)
    {
        if (glyphWidth <= 0) throw new ArgumentOutOfRangeException(nameof(glyphWidth));
        int length = NativeLifeHud.DecimalGlyphs(Value).Length;
        int width = checked(length * glyphWidth);
        return CenterX + width / 2 - width;
    }
}

/// <summary>
/// Draw contract of 004194D0: DEF first, separator from glyph #10 of
/// the normal 11-column atlas, then ATK. Opacity 0xA0/0x100 follows
/// the position bit 0x4000, without altering identity/visibility rules.
/// Coordinates are relative to the field-attribute anchor determined by
/// 00418A80 / 0045C140; final native pixels still need paired validation.
/// </summary>
public readonly record struct NativeFieldAttributeFrame(
    NativeFieldNumberPass Defense, int SeparatorX, int SeparatorY,
    int SeparatorOpacity, NativeFieldNumberPass Attack)
{
    public const int ActiveOpacity = 0x100;
    public const int InactiveOpacity = 0xA0;

    public static NativeFieldAttributeFrame Compose(
        CardPoint center, int normalGlyphWidth, int normalGlyphHeight,
        int attack, int defense, int originalAttack, int originalDefense,
        bool defensePosition)
    {
        if (normalGlyphWidth <= 0) throw new ArgumentOutOfRangeException(nameof(normalGlyphWidth));
        if (normalGlyphHeight <= 0) throw new ArgumentOutOfRangeException(nameof(normalGlyphHeight));
        if (attack < 0 || defense < 0 || originalAttack < 0 || originalDefense < 0)
            throw new ArgumentOutOfRangeException(nameof(attack), "Attributes must be nonnegative.");

        int top = center.Y - normalGlyphHeight / 2;
        // 004196D6..004196F6 and 004199D0..004199DE use two
        // separate signed integer halves. For odd glyph widths this
        // differs by one pixel from 3 * normalGlyphWidth.
        int distance = normalGlyphWidth / 2 + checked(5 * normalGlyphWidth) / 2;
        string defenseSprite = NativeFieldAttributeFonts.ForValue(defense, originalDefense);
        string attackSprite = NativeFieldAttributeFonts.ForValue(attack, originalAttack);
        var defensePass = new NativeFieldNumberPass(
            defenseSprite, NativeFieldAttributeFonts.Columns(defenseSprite),
            defense, center.X + distance, top,
            defensePosition ? ActiveOpacity : InactiveOpacity);
        var attackPass = new NativeFieldNumberPass(
            attackSprite, NativeFieldAttributeFonts.Columns(attackSprite),
            attack, center.X - distance, top,
            defensePosition ? InactiveOpacity : ActiveOpacity);
        return new(defensePass, center.X + normalGlyphWidth / 2 - normalGlyphWidth, top,
            InactiveOpacity, attackPass);
    }

    /// <summary>
    /// 004194D0 updates the displayed attribute inside its Draw path:
    /// current = target + trunc((current-target)*7/8). C# division of
    /// signed integers rounds toward zero, matching x86's sign correction
    /// before SAR. This helper does NOT advance during a native Update.
    /// </summary>
    public static int SmoothDisplayed(int displayed, int target) =>
        target + (int)(((long)displayed - target) * 7 / 8);
}

