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
    /// The original cursor uses one image at a time. On a legal hover it
    /// switches to an action-specific image which includes the action cloud;
    /// outside an actionable card it uses cursor00. Never draw both on the
    /// same frame.
    /// </summary>
    public static string CursorSprite(NativeCursorAction? action) =>
        action is { } selected ? ActionSprite(selected) : DefaultPointer;

    /// <summary>
    /// Determine the only currently legal hand cursor action. This helper
    /// receives the actual Core legality gates rather than classifying all
    /// spells as Activate and all monsters as Summon.
    /// </summary>
    public static NativeCursorAction? HandAction(
        bool monster, bool canSummon, bool canSet, bool canActivate,
        bool requestSet, bool discarding)
    {
        if (discarding) return null; // discard is a sandbox-only label
        if (monster)
        {
            if (requestSet && canSet) return NativeCursorAction.Set;
            if (canSummon) return NativeCursorAction.Summon;
            return canSet ? NativeCursorAction.Set : null;
        }

        if (requestSet && canSet) return NativeCursorAction.Set;
        if (canActivate) return NativeCursorAction.Activate;
        return canSet ? NativeCursorAction.Set : null;
    }

    /// <summary>
    /// Right-click cycles only if two real choices are legal. Traps on hand
    /// cannot Activate, so their only available action is Set.
    /// </summary>
    public static bool CanToggleHandAction(
        bool monster, bool canSummon, bool canSet, bool canActivate) =>
        canSet && (monster ? canSummon : canActivate);

    /// <summary>
    /// Cursor coordinates come from the full window, not the duel canvas.
    /// The asset's precise hotspot is not yet recovered from cursor_e.txt.
    /// Keeping the origin at the mouse point is an explicit provisional
    /// placement until paired screenshots establish the real offset.
    /// </summary>
    public static CardPoint PointerOrigin(CardPoint windowMouse) => windowMouse;
}
