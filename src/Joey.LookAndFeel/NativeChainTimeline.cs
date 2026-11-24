namespace Joey.LookAndFeel;

/// <summary>
/// Contract of 0041F5C0 / 0041F800 / 0041F810 / 0041F830 and
/// the unambiguous selector calculations of 0041F870.
/// Selector keys are arguments to a native resource lookup, not proven
/// physical atlas indices. No rendering or command mutation occurs here.
/// </summary>
public sealed class NativeChainTimeline(int number)
{
    public const int WaitCounter = 128;
    public const int EndCounter = 192;
    public const int Step = 4;
    public int Number { get; } = number >= 0 ? number :
        throw new ArgumentOutOfRangeException(nameof(number));
    public int Layer => 0x120 + Number;
    public int Counter { get; private set; }
    public bool Released { get; private set; }
    public bool Active => Counter < EndCounter;

    // Slot +4 query, NOT the object-active byte. At the held frame the
    // native query reports false even though the object remains visible.
    public bool BlocksRelease => Active && (Released || Counter != WaitCounter);

    public void Release() => Released = true;

    public void AdvanceNativeUpdate()
    {
        if (Active && (Released || Counter < WaitCounter))
            Counter += Step;
    }

    public int? SpriteSelector => !Active || Counter == 0 ? null :
        Counter <= WaitCounter ? Math.Min(Counter / 4, 25) :
        Math.Max(1, 15 - (Counter - WaitCounter) / 4);

    public bool HasNumberPass => SpriteSelector.HasValue && Number != 0;

    // 0041FD60 packs the side and combined native zone. Do not silently
    // convert combined zone 11+ into a monster/spell field-only descriptor.
    public static ushort Descriptor(int side, int combinedZone)
    {
        if (side is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(side));
        if (combinedZone is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(combinedZone));
        return (ushort)(0x8000 | (combinedZone << 1) | side);
    }

    // 0041FEA0 searches by the explicit nonzero number; number zero
    // permits either the zero-number match or the matching side/zone.
    public bool MatchesRelease(ushort descriptor, int side, int combinedZone, int number) =>
        Number == number || (number == 0 && (descriptor & 1) == side &&
            (((descriptor >> 1) & 31) + ((descriptor >> 6) & 255)) == combinedZone);
}
