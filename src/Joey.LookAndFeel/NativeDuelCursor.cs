namespace Joey.LookAndFeel;

/// <summary>
/// Original Power of Chaos cursor resources. The cursor/effect label
/// bubbles are separate from summon.txt/set.txt effects on the board.
/// These paths are taken from the indexed original data.dat.
/// </summary>
public enum NativeCursorAction
{
    Summon, Set, Activate, Attack, Draw, Flip, Fusion, Revive,
    SetAttack, SetDefense, Surrender, Over
}

public static class NativeDuelCursor
{
    public const string ManifestEnglish = @"cursor\cursor_e.txt";
    public const string DefaultPointer = @"cursor\cursor00.yga";
    public const string AlternatePointer = @"cursor\cursor01.yga";

    public static string ActionSprite(NativeCursorAction action) =>
        action switch
        {
            NativeCursorAction.Summon => @"cursor\cursor_e_summon.yga",
            NativeCursorAction.Set => @"cursor\cursor_e_set.yga",
            NativeCursorAction.Activate => @"cursor\cursor_e_act.yga",
            NativeCursorAction.Attack => @"cursor\cursor_e_atk.yga",
            NativeCursorAction.Draw => @"cursor\cursor_e_draw.yga",
            NativeCursorAction.Flip => @"cursor\cursor_e_flip.yga",
            NativeCursorAction.Fusion => @"cursor\cursor_e_fusion.yga",
            NativeCursorAction.Revive => @"cursor\cursor_e_rev.yga",
            NativeCursorAction.SetAttack => @"cursor\cursor_e_setatk.yga",
            NativeCursorAction.SetDefense => @"cursor\cursor_e_setdef.yga",
            NativeCursorAction.Surrender => @"cursor\cursor_e_surrender.yga",
            NativeCursorAction.Over => @"cursor\cursor_e_over.yga",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    /// <summary>
    /// Corresponds to the action popup for the hand card, NOT the sprite
    /// shown during an already-committed summon. The old sandbox discards
    /// without a native cursor action; leave that fallback unchanged.
    /// </summary>
    public static NativeCursorAction? HandAction(bool monster, bool setMode, bool discarding) =>
        discarding ? null : setMode ? NativeCursorAction.Set :
            monster ? NativeCursorAction.Summon : NativeCursorAction.Activate;

    /// <summary>
    /// Local anchoring for the original cursor action bubble.
    /// Its pointed lower tip should extend ONTO the upper portion of the
    /// lifted card rather than stopping in the gap above it. Centers the
    /// unscaled sprite horizontally; the overlap is provisional until
    /// compared with the original executable and manifest.
    /// </summary>
    public const int CloudTipOverlapPixels = 12;

    public static CardBounds ActionBounds(CardBounds card, int spriteWidth, int spriteHeight)
    {
        if (spriteWidth <= 0) throw new ArgumentOutOfRangeException(nameof(spriteWidth));
        if (spriteHeight <= 0) throw new ArgumentOutOfRangeException(nameof(spriteHeight));
        int centerX = card.X + card.Width / 2;
        return new(centerX - spriteWidth / 2,
            card.Y - spriteHeight + CloudTipOverlapPixels,
            spriteWidth, spriteHeight);
    }
}
