namespace Joey.LookAndFeel;

public enum DuelCardZone { Monster, Spell, Field }

public readonly record struct CardRenderGeometry(
    CardBounds Bounds, int Degrees, bool FaceDown, CardPoint? AttributesCenter);

public static class DuelCardLayout
{
    public const int MiniWidth = 50, MiniHeight = 72;

    // 00418A80 stores (oppositeSide * 2 + defenseBit) << 7 at object+40h.
    // 00418EF0 converts each 128-unit quarter turn to 90 degrees.
    public static int Rotation(bool nearSide, bool defense) =>
        (nearSide ? 0 : 180) + (defense ? 90 : 0);

    public static CardPoint Center(DuelCardZone zone, bool nearSide, int slot = 0) => zone switch
    {
        DuelCardZone.Field => nearSide ? new(286, 353) : new(766, 236),
        DuelCardZone.Spell => NativeDuelLayout.MonsterCenter(slot, nearSide) with { Y = nearSide ? 460 : 140 },
        _ => NativeDuelLayout.MonsterCenter(slot, nearSide)
    };

    public static CardPoint FrameCenter(CardBounds frame) =>
        new(frame.X + frame.Width / 2, frame.Y + frame.Height / 2);

    public static CardBounds BoardPortrait(CardPoint center) =>
        new(center.X - MiniWidth / 2, center.Y - MiniHeight / 2, MiniWidth, MiniHeight);

    public static CardRenderGeometry OnBoard(DuelCardZone zone, bool nearSide, int slot, bool defense, bool faceDown)
    {
        var center = Center(zone, nearSide, slot);
        var frame = BoardPortrait(center);
        // The human can inspect the attributes of their own Set monsters
        // even while the card artwork stays face-down. An opponent's
        // unrevealed monster must not disclose information. Native 004194D0
        // renders ATK/DEF with side/visibility flags; exact gray/active
        // color variants remain under assembly investigation.
        CardPoint? attributes = zone == DuelCardZone.Monster &&
                                (nearSide || !faceDown)
            ? center with { Y = center.Y + (nearSide ? 46 : -46) } // 00419612–00419630
            : null;
        return InTransit(frame, nearSide, defense, faceDown) with { AttributesCenter = attributes };
    }

    public static CardRenderGeometry InTransit(CardBounds portraitFrame, bool nearSide, bool defense, bool faceDown)
    {
        var bounds = defense
            ? new CardBounds(portraitFrame.X + (portraitFrame.Width - portraitFrame.Height) / 2,
                portraitFrame.Y + (portraitFrame.Height - portraitFrame.Width) / 2,
                portraitFrame.Height, portraitFrame.Width)
            : portraitFrame;
        return new(bounds, Rotation(nearSide, defense), faceDown, null);
    }
}
