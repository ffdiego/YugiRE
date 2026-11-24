namespace Joey.LookAndFeel;

/// <summary>
/// Vertical reference lines of card status overlays from 00419B70,
/// 0041A070 and 0041A3B0. DAT_005F3008 is the height of the
/// miniature-card image bank (loaded by 00418560), NOT number glyphs.
/// Horizontal sprite placement is now resolved by NativeStatusDrawLayout
/// using the original mini-card width and manifest frame dimensions.
/// </summary>
public readonly record struct NativeCardStatusGeometry(
    int SideMultiplier, int IdentityChangeY, int StatusY)
{
    /// <summary>
    /// The decompiled native expression for side:
    /// ((~perspective &amp; 1) XOR (~descriptor &amp; 1)) * -2 + 1.
    ///
    /// 00419B70: identity-change Y = objY - (miniHeight * side) / 2.
    /// 0041A070 / 0041A3B0: status Y = objY + side * (miniHeight / 2 - 5).
    /// These are *base anchors*, before sprite-height and local pass
    /// offsets. This class intentionally does not guess their final X/Y.
    /// </summary>
    public static NativeCardStatusGeometry FromNative(
        CardPoint objectCenter, int miniHeight, int perspectiveSide, int descriptorSide)
    {
        if (miniHeight <= 0) throw new ArgumentOutOfRangeException(nameof(miniHeight));
        if (perspectiveSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(perspectiveSide));
        if (descriptorSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(descriptorSide));

        int sideMultiplier = perspectiveSide == descriptorSide ? 1 : -1;
        return new(sideMultiplier,
            objectCenter.Y - (miniHeight * sideMultiplier) / 2,
            objectCenter.Y + sideMultiplier * (miniHeight / 2 - 5));
    }

    /// <summary>
    /// Horizontal source strip of an atlas with a manifest-provided
    /// column count. The native status Draws use frame * (width/columns),
    /// cropping each sample to one column, at Y=0. A non-divisible atlas
    /// is rejected rather than silently using the wrong frame.
    /// </summary>
    public static CardBounds SourceFrame(
        int atlasWidth, int atlasHeight, int columns, int frame)
    {
        if (atlasWidth <= 0) throw new ArgumentOutOfRangeException(nameof(atlasWidth));
        if (atlasHeight <= 0) throw new ArgumentOutOfRangeException(nameof(atlasHeight));
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (frame < 0 || frame >= columns)
            throw new ArgumentOutOfRangeException(nameof(frame));
        if (atlasWidth % columns != 0)
            throw new ArgumentException("Status atlas width must be divisible by its manifest columns.",
                nameof(atlasWidth));
        int width = atlasWidth / columns;
        return new CardBounds(checked(frame * width), 0, width, atlasHeight);
    }
}

/// <summary>
/// Native battle-command mutations that trigger the two status animations.
/// 005B9470 dispatches 0x60 to 0040B270 and 0x69 to 0040B370.
/// This is a numeric model, NOT a substitute for the original field state
/// or an assertion that the YugiRE Core already implements these commands.
/// </summary>
public static class NativeCardStatusCounters
{
    public const int AddCommand = 0x60;
    public const int MinimumCommand = 0x69;
    public const int DirectSetFirstCommand = 0x5f;
    public const int TransitionCounter = 0x200;

    /// <summary>
    /// 0040B270: (current &amp; 0xF) + delta, saturating at 15.
    /// Animation state 3 begins at 0x200 if the visual object exists.
    /// </summary>
    public static int AddFirst(int current, ushort delta)
    {
        if (current < 0) throw new ArgumentOutOfRangeException(nameof(current));
        return Math.Min(15, (current & 0xf) + (int)delta);
    }

    /// <summary>
    /// 0040B370: if the existing three-bit value is zero or the
    /// requested amount is lower, take the requested amount; otherwise
    /// retain existing. Finally mask to three bits (NOT saturation).
    /// Animation state 4 begins at 0x200 if the object exists.
    /// </summary>
    public static int MinimumSecond(int current, ushort requested)
    {
        if (current < 0) throw new ArgumentOutOfRangeException(nameof(current));
        int prior = current & 7;
        int candidate = prior == 0 || requested < prior ? requested : prior;
        return candidate & 7;
    }

    /// <summary>005B9470 opcode 0x5F stores the low four bits directly.</summary>
    public static int SetFirst(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return value & 15;
    }
}
