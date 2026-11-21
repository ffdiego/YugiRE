namespace Joey.LookAndFeel;

/// <summary>
/// Hand layout extracted from Joey's card-view helpers. Coordinates are in
/// the original 800x600 duel canvas, not the scaled desktop window.
/// Native VAs: 0045C590, 0045C600, 0045C8B0.
/// </summary>
public static class HandPresentation
{
    // 0045C600 uses a resource width, not the width projected on the canvas.
    // On the reference frame the 200-pixel card is shown at half size.
    public const int LayoutCardWidth = 200;
    public const int CardWidth = LayoutCardWidth / 2, CardHeight = 290 / 2, HoverOffset = 32;

    public static int CenterX(int index, int count, bool nearSide = true)
    {
        if (count < 1 || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        int span = Math.Min((LayoutCardWidth / 2 + 8) * count, 512) - LayoutCardWidth / 2;
        int offset = span / 2 - (count > 1 ? span * index / (count - 1) : 0);
        return 525 + (nearSide ? -offset : offset);
    }

    public static CardBounds Bounds(int index, int count, bool nearSide, int lift = 0) =>
        new(CenterX(index, count, nearSide) - CardWidth / 2,
            (nearSide ? NativeDuelLayout.CanvasHeight - lift : 0) - CardHeight / 2,
            CardWidth, CardHeight);

    /// <summary>
    /// The native per-update motion: target + trunc((current-target)*3/4).
    /// The native *frequency* of these updates is not yet established.
    /// </summary>
    public static int Approach(int current, int target) =>
        target + (current - target) * 3 / 4;
}

/// <summary>Point in the original 800x600 logical canvas.</summary>
public readonly record struct CardPoint(int X, int Y);

/// <summary>Rectangle with integer coordinates (no raylib dependency).</summary>
public readonly record struct CardBounds(int X, int Y, int Width, int Height);

/// <summary>
/// Native monster zone centers from DAT_005DB560, shared across the duel screen.
/// Rect dimensions are a *client policy* (66x88), not a measured native constant.
/// </summary>
public static class NativeDuelLayout
{
    public const int CanvasWidth = 800, CanvasHeight = 600;
    public const int MonsterSlots = 5;
    public const int MonsterZoneSpacing = 78;
    public const int MonsterCardWidth = 66, MonsterCardHeight = 88;

    /// <param name="nearSide">
    /// True for the player displayed near the hand. Opponent slot order is mirrored.
    /// </param>
    public static CardPoint MonsterCenter(int zone, bool nearSide)
    {
        if (zone is < 0 or >= MonsterSlots)
            throw new ArgumentOutOfRangeException(nameof(zone));
        return new(nearSide ? 369 + zone * MonsterZoneSpacing : 681 - zone * MonsterZoneSpacing,
                   nearSide ? 352 : 248);
    }

    public static CardBounds MonsterBounds(int zone, bool nearSide)
    {
        var center = MonsterCenter(zone, nearSide);
        return new(center.X - MonsterCardWidth / 2, center.Y - MonsterCardHeight / 2,
                   MonsterCardWidth, MonsterCardHeight);
    }

    // 0045C280 regions 0D/0E, coordinate tables 005DB5B0/005DB5C0.
    public static CardBounds GraveBounds(bool nearSide) =>
        DuelCardLayout.BoardPortrait(nearSide ? new(760, 366) : new(290, 234));

    public static CardBounds DeckBounds(bool nearSide) =>
        DuelCardLayout.BoardPortrait(nearSide ? new(760, 460) : new(290, 140));
}
