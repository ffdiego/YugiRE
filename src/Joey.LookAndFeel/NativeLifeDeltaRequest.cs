namespace Joey.LookAndFeel;

/// <summary>
/// Presentation input recovered from 00414110 (state 7) and 00408470.
/// Slot -1 is a native sentinel, NOT a coordinate. The actual Draw
/// 0047C660 and its anchor mapping still need assembly evidence.
/// </summary>
public readonly record struct NativeLifeDeltaRequest
{
    public int Delta { get; }
    public int AnchorSlot { get; }
    public bool HoldConsumption { get; }

    public NativeLifeDeltaRequest(int delta, int anchorSlot, bool holdConsumption)
    {
        if (anchorSlot < -1 || anchorSlot >= NativeDuelLayout.MonsterSlots)
            throw new ArgumentOutOfRangeException(nameof(anchorSlot));
        Delta = delta;
        AnchorSlot = anchorSlot;
        HoldConsumption = holdConsumption;
    }

    // 00414110 selects the slot belonging to the side whose LP is
    // affected, not always the defender. Flags 8/10h override it.
    // Accept raw native flags, not guessed gameplay interpretations.
    public static NativeLifeDeltaRequest? Battle(int affectedSide, int attackerSide,
        int attackerSlot, int defenderSlot, uint affectedSideFlags, int damageMagnitude)
    {
        if (affectedSide is not (0 or 1) || attackerSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(affectedSide));
        if (attackerSlot is < 0 or >= NativeDuelLayout.MonsterSlots ||
            defenderSlot is < -1 or >= NativeDuelLayout.MonsterSlots)
            throw new ArgumentOutOfRangeException(nameof(attackerSlot));
        if (damageMagnitude < 0) throw new ArgumentOutOfRangeException(nameof(damageMagnitude));
        if (damageMagnitude == 0) return null;
        int slot = defenderSlot == -1 || (affectedSideFlags & 0x18) != 0
            ? -1 : affectedSide == attackerSide ? attackerSlot : defenderSlot;
        return new(-damageMagnitude, slot, true);
    }

    // 00408470 uses -1 in both setup stages, with flag 1 at stage
    // zero and 0 at stage three. The stage belongs to ACTIONS.
    public static NativeLifeDeltaRequest General(int signedDelta, bool holdConsumption) =>
        new(signedDelta, -1, holdConsumption);
}
