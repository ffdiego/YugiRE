namespace Joey.LookAndFeel;

/// <summary>
/// 00408730 packs the field side, region, slot and position bit for 004531C0.
/// This is not a chain-negation event. The native command's gameplay semantics
/// and scheduler gate must be resolved independently of this visual address.
/// </summary>
public readonly record struct NativeDisabledChainTarget
{
    public ushort Descriptor { get; }
    public int Side => Descriptor & 1;
    public int Region => (Descriptor >> 1) & 31;
    public int Slot => (Descriptor >> 6) & 255;
    public bool Rotated => (Descriptor & 0x4000) != 0;

    public NativeDisabledChainTarget(ushort descriptor)
    {
        Descriptor = descriptor;
        if (Region is not (0 or 5 or 10) ||
            Slot >= (Region == 10 ? 1 : NativeDuelLayout.MonsterSlots))
            throw new ArgumentException("Not a field descriptor emitted by 00408730.", nameof(descriptor));
    }

    public static NativeDisabledChainTarget FromField(int side, int zone, bool positionBit)
    {
        if (side is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(side));
        if (zone is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(zone));
        int region = zone / 5 * 5;
        int slot = zone % 5;
        return new((ushort)(side | (region << 1) | (slot << 6) | (positionBit ? 0x4000 : 0)));
    }

    // 0045C140 delegates these regions to 0045C280. Reuse the client's
    // existing board geometry; absolute centers still need native comparison.
    public CardPoint Center(int perspective)
    {
        if (perspective is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(perspective));
        return DuelCardLayout.Center(Region switch {
            0 => DuelCardZone.Monster, 5 => DuelCardZone.Spell, 10 => DuelCardZone.Field,
            _ => throw new InvalidOperationException("Invalid region")
        }, Side == perspective, Slot);
    }

    // Decompilation of 00570710: generic set/clear, not a toggle. The
    // command writes bit 5 even when the visual is suppressed by argHigh.
    public static uint FieldFlagsAfterCommand(uint flags, ushort argLow) =>
        argLow == 0 ? flags & ~0x20u : flags | 0x20u;

    public static bool CreatesVisual(ushort argLow, ushort argHigh) => argLow != 0 && argHigh == 0;
}
