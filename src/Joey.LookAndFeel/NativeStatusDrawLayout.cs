namespace Joey.LookAndFeel;

/// <summary>One frame of a native card.txt atlas. Manifest columns are explicit.</summary>
public readonly record struct NativeStatusAtlas(int Width, int Height, int Columns)
{
    public CardBounds Frame(int frame) =>
        NativeCardStatusGeometry.SourceFrame(Width, Height, Columns, frame);
}

/// <summary>One native cropped texture pass, at integer destination coordinates.</summary>
public readonly record struct NativeStatusSpritePass(
    int ResourceIndex, CardBounds Source, CardPoint Destination);

/// <summary>
/// The two status Draws share a main sprite and number, followed by an
/// optional transition sprite. This is a layout, not a client effect or
/// a claim about native field-flag semantics.
/// </summary>
public readonly record struct NativeStatusDrawLayout(
    NativeStatusSpritePass Main, CardPoint NumberAnchor,
    bool NumberCentered, NativeStatusSpritePass? Transition)
{
    // 0041A070 uses the mini-card bank width DAT_005F3004, not EBX
    // as suggested by the incomplete decompilation. ESI advances by
    // the frame width after the main sprite call at 0041A23A.
    public static NativeStatusDrawLayout First(
        int objectX, int statusY, int miniWidth, NativeCardStatusFrame cue,
        NativeStatusAtlas mainAtlas, NativeStatusAtlas? transitionAtlas = null)
    {
        Validate(miniWidth, cue, 6, 7, transitionAtlas);
        var source = mainAtlas.Frame(cue.MainFrame);
        int left = checked(objectX - miniWidth / 2);
        int numberX = checked(left + source.Width + 6);
        // 0041A20F..0041A223 uses (mainHeight+1)/2.
        var main = new NativeStatusSpritePass(6, source,
            new CardPoint(left, statusY - (source.Height + 1) / 2));

        NativeStatusSpritePass? transition = null;
        if (cue.TailFrame is int frame && transitionAtlas is NativeStatusAtlas atlas)
        {
            var tail = atlas.Frame(frame);
            // 0041A371..0041A398 subtract half the tail dimensions
            // from the number anchor (the accumulated ESI + 6).
            transition = new(7, tail,
                new CardPoint(numberX - tail.Width / 2, statusY - tail.Height / 2));
        }
        return new(main, new CardPoint(numberX, statusY), true, transition);
    }

    // 0041A3B0 has its own region gate BEFORE reading field status.
    // The parent 004194D0 separately checks face-up and region <= 10.
    public static bool SecondRegionEligible(ushort descriptor) => (descriptor & 0x3e) < 0x0a;

    // 0041A4FC..0041A510: right edge = objX + miniWidth/2 - 3.
    // 0041A548..0041A57D: main sprite is placed one frame plus 8
    // pixels left of that edge. Number helper is right-anchored.
    public static NativeStatusDrawLayout Second(
        int objectX, int statusY, int miniWidth, NativeCardStatusFrame cue,
        NativeStatusAtlas mainAtlas, NativeStatusAtlas? transitionAtlas = null)
    {
        Validate(miniWidth, cue, 8, 9, transitionAtlas);
        var source = mainAtlas.Frame(cue.MainFrame);
        int right = checked(objectX + miniWidth / 2 - 3);
        var main = new NativeStatusSpritePass(8, source,
            new CardPoint(right - source.Width - 8, statusY - source.Height / 2));

        NativeStatusSpritePass? transition = null;
        if (cue.TailFrame is int frame && transitionAtlas is NativeStatusAtlas atlas)
        {
            var tail = atlas.Frame(frame);
            // 0041A6B9..0041A6E6: tail is centered at right-4.
            transition = new(9, tail,
                new CardPoint(right - tail.Width / 2 - 4, statusY - tail.Height / 2));
        }
        return new(main, new CardPoint(right, statusY), false, transition);
    }

    /// <summary>
    /// 00419B70: ordinary changed type (5) and attribute (4) icons use
    /// code-1 as the atlas column. The type is on the opposite horizontal
    /// edge to the attribute, mirrored for the far-side perspective.
    /// The special equipment/card-art branch is intentionally excluded.
    /// </summary>
    public static NativeStatusSpritePass IdentityIcon(
        bool attribute, int effectiveCode, CardPoint objectCenter,
        int miniWidth, NativeCardStatusGeometry geometry, NativeStatusAtlas atlas)
    {
        if (miniWidth <= 0) throw new ArgumentOutOfRangeException(nameof(miniWidth));
        if (geometry.SideMultiplier is not (1 or -1))
            throw new ArgumentException("Invalid perspective multiplier.", nameof(geometry));
        if (effectiveCode < 1 || effectiveCode > (attribute ? 6 : 20))
            throw new ArgumentOutOfRangeException(nameof(effectiveCode));
        var source = atlas.Frame(effectiveCode - 1);
        int edge = checked(miniWidth * geometry.SideMultiplier) / 2;
        int x = checked(objectCenter.X + (attribute ? edge : -edge));
        return new(attribute ? 4 : 5, source,
            new CardPoint(x - source.Width / 2, geometry.IdentityChangeY - source.Height / 2));
    }

    /// <summary>
    /// 004194D0 passes numeric-center X and an animated Y to virtual
    /// compositor slot +0x50. The sign is the pre-smoothing target delta,
    /// not the difference between base stats and displayed stats.
    /// This returns call arguments, not an assumption about the virtual
    /// compositor's texture pivot/blend.
    /// </summary>
    public static NativeStatusFlash? AttributeFlash(
        int numericCenterX, int numberTopY, int preSmoothingDelta, int nativeGlobalCounter)
    {
        if (nativeGlobalCounter < 0)
            throw new ArgumentOutOfRangeException(nameof(nativeGlobalCounter));
        if (preSmoothingDelta == 0) return null;
        int bob = (nativeGlobalCounter >> 1) & 3;
        int y = preSmoothingDelta > 0 ? numberTopY - 7 - bob : numberTopY - 9 + bob;
        return new(NativeCardStatusOverlay.AttributeChangeFlashIndex(
            preSmoothingDelta > 0, nativeGlobalCounter),
            new CardPoint(numericCenterX, y), 0x100);
    }

    private static void Validate(int miniWidth, NativeCardStatusFrame cue,
        int mainIndex, int tailIndex, NativeStatusAtlas? transitionAtlas)
    {
        if (miniWidth <= 0) throw new ArgumentOutOfRangeException(nameof(miniWidth));
        if (cue.MainResourceIndex != mainIndex ||
            (cue.TailResourceIndex.HasValue != cue.TailFrame.HasValue) ||
            (cue.TailResourceIndex is int index && index != tailIndex))
            throw new ArgumentException("Unexpected native status resource indices.", nameof(cue));
        if (cue.TailFrame.HasValue && !transitionAtlas.HasValue)
            throw new ArgumentException("Transition requires its own DAT atlas dimensions.",
                nameof(transitionAtlas));
    }
}

/// <summary>Native virtual +0x50 call arguments for a stat-change flash.</summary>
public readonly record struct NativeStatusFlash(
    int ResourceIndex, CardPoint Anchor, int Intensity);
