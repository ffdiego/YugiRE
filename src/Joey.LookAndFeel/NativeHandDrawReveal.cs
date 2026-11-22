namespace Joey.LookAndFeel;

/// <summary>
/// Deck-to-hand face transition. 00418B60 counts eight 0x20 updates and
/// interpolates the face bit; 00418EF0 uses a fixed cosine table.
/// Math.Cos is only a presentation preview, not bit-for-bit native parity.
/// </summary>
public readonly record struct NativeHandDrawFrame(bool FaceDown, double HorizontalScale);

public static class NativeHandDrawReveal
{
    public const int FaceMidpointUpdate = CardTravel.Updates / 2;

    public static NativeHandDrawFrame FromUpdate(int update, bool nearSide)
    {
        if (update < 0 || update > CardTravel.Updates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (!nearSide)
            return new NativeHandDrawFrame(FaceDown: true, HorizontalScale: 1);

        int counter = update * 0x20;
        int reflected = Math.Min(counter, 0x100 - counter);
        double scale = Math.Max(0, Math.Cos(reflected * Math.PI / 256));
        return new NativeHandDrawFrame(
            FaceDown: update < FaceMidpointUpdate,
            HorizontalScale: scale);
    }
}
