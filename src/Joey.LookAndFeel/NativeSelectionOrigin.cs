namespace Joey.LookAndFeel;

/// <summary>Read-only static origin table from the V30/V11 bundle
/// 005DB5B0:80 and 0045C280/0045C140. Caller supplies the live
/// side-specific X adjustment for descriptor D; never guess it.
/// </summary>
public static class NativeSelectionOrigin
{
    public static CardPoint Resolve(int mode, int side, int perspective, int? graveOrDeckXAdjustment = null)
    {
        if (side is not (0 or 1) || perspective is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(side));
        bool own = side == perspective;
        return NativeSelectionWindow.TransitionOriginDescriptor(mode) switch
        {
            0xC => own ? new(284,460) : new(766,140),
            0xD => graveOrDeckXAdjustment is int adjustment ?
                own ? new(checked(760+adjustment),460) : new(checked(290-adjustment),140)
                : throw new ArgumentException("Native descriptor D requires live X adjustment.", nameof(graveOrDeckXAdjustment)),
            0xE => own ? new(760,366) : new(290,234),
            0xF => own ? new(762,308) : new(292,292),
            _ => throw new ArgumentOutOfRangeException(nameof(mode),"Unknown native origin descriptor.")
        };
    }
}
