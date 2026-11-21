namespace Joey.LookAndFeel;

/// <summary>
/// Hand layout extracted from Joey's card-view helpers. Coordinates are in
/// the original 800x600 duel canvas, not the scaled desktop window.
/// Native VAs: 0045C590, 0045C600, 0045C8B0.
/// </summary>
public static class HandPresentation
{
    // These sprite dimensions are the current client's asset choice; they
    // have not been shown to be global native renderer dimensions.
    public const int CardWidth = 108, CardHeight = 153, HoverOffset = 32;

    public static int CenterX(int index, int count)
    {
        if (count < 1 || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        int span = Math.Min((CardWidth / 2 + 8) * count, 512) - CardWidth / 2;
        return 525 - span / 2 + (count > 1 ? span * index / (count - 1) : 0);
    }

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

    // Mirrored side columns observed in the duel video. Dimensions are from the client.
    public static CardBounds GraveBounds(bool nearSide) => SideZoneBounds(nearSide, monsterRow: true);
    public static CardBounds DeckBounds(bool nearSide) => SideZoneBounds(nearSide, monsterRow: false);
    private static CardBounds SideZoneBounds(bool nearSide, bool monsterRow) =>
        new(nearSide ? 730 : 250, monsterRow ? MonsterBounds(0, nearSide).Y : nearSide ? 416 : 96,
            MonsterCardWidth, MonsterCardHeight);
}
