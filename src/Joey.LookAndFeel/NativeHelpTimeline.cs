namespace Joey.LookAndFeel;

/// <summary>Raw contextual selection word consumed by help Update/Draw.
/// The high two bits are deliberately excluded from identity comparisons.</summary>
public readonly record struct NativeHelpTarget(ushort Word)
{
    public int Side => Word & 1;
    public int Zone => (Word >> 1) & 31;
    public int Index => (Word >> 6) & 255;
    public int Identity => Word & 0x3fff;
}

/// <summary>0045E3D0/0045E4A0: persistent, update-driven contextual help.
/// Input is the one-update request from 0045E240, not an action queue.
/// Caller supplies native gate facts, 0045C140 anchor and decoded resource size.</summary>
public sealed class NativeHelpTimeline
{
    public const int Layer = 0xf00;
    public int MessageKey { get; private set; } = -1;
    public int Counter { get; private set; }
    public NativeHelpTarget Target { get; private set; }
    public bool Visible => MessageKey >= 0;

    // Each call consumes the request, including a rejected one.
    // A changed request/target closes an existing object for this update;
    // it cannot reopen until a subsequent update.
    public void AdvanceUpdate(int requestKey, NativeHelpTarget currentTarget,
        int localSide, bool native005F2FDC, bool native005F2FE0,
        bool nativeVirtual2C, bool native005664A0Group6)
    {
        if (Counter < 256) Counter += 4;
        if (!native005F2FDC || native005F2FE0)
        {
            MessageKey = -1;
            return;
        }

        if (Visible)
        {
            if (requestKey != MessageKey || currentTarget.Identity != Target.Identity)
                MessageKey = -1;
            return;
        }

        if (requestKey < 0 || (requestKey != 9 && currentTarget.Side != (localSide & 1)))
            return;
        if (!nativeVirtual2C && !native005664A0Group6) return;

        Target = currentTarget;
        MessageKey = requestKey;
        Counter = 0;
    }

    // Geometry only, not a renderer: native virtual +4C and two manifest
    // lookups still need DAT verification. Scale is in 8.8 fixed point.
    public CardBounds? Sample(int anchorX, int anchorY, int handHeightSource,
        int fieldHeightSource, int spriteWidth, int spriteHeight,
        ReadOnlySpan<int> cosine)
    {
        if (!Visible) return null;
        NativeFusionMath.Validate(cosine);
        int sourceHeight = Target.Zone == 11 ? handHeightSource / 2 : fieldHeightSource;
        int y = Math.Max(anchorY - sourceHeight / 2, spriteHeight);
        int x = Math.Min(anchorX, 800 - spriteWidth / 2);
        int scale = Counter < 16 ? NativeFusionMath.C(cosine, Counter * 8 - 128) >> 8 : 256;
        int h = unchecked(spriteHeight * scale) / 256;
        return new CardBounds(x - spriteWidth / 2, y - spriteHeight / 2 - h / 2, spriteWidth, h);
    }
}
