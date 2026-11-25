namespace Joey.LookAndFeel;

/// <summary>Effect of the numeric Draw on its private accumulated values.</summary>
public enum NativeFieldAttributeDisposition { Preserve, ResetToBase, Advance }

/// <summary>
/// Native field-attribute visibility contract extracted from card Draw
/// 00418EF0 -> 004194D0 (Joey PC).
///
/// This gate controls ONLY the numeric ATK/DEF passes. Extra status markers
/// (00419B70, 0041A3B0, 0041A070) have independent branches and should
/// never be hidden by assuming their visibility equals the number gate.
/// </summary>
public static class NativeFieldAttributeVisibility
{
    // 00418B60 sets obj+0x14 to 0x100 when the card's rotation, position
    // and face transition is finished. 00418EF0 does not reach 004194D0
    // while that counter is incomplete, or in animation state 5.
    public const int SettledCounter = 0x100;
    public const int ExitAnimationState = 5;
    public const ushort FaceUpDescriptorFlag = 0x8000;

    /// <summary>
    /// Model the native branch with the raw 16-bit position descriptor at
    /// obj+0x24. The value at obj+0x10 must match the current logical
    /// occupant returned by 0056DD30, otherwise the numeric pass is skipped.
    ///
    /// Descriptor layout: bit 0 = side, bits 1..5 = zone region,
    /// bits 6..13 = slot offset, bit 15 = face-up/revealed.
    /// The original checks region+offset &lt;=10, then region == 0,
    /// and allows attributes for the perspective side or a revealed card.
    /// </summary>
    public static bool ShouldDrawNative(
        ushort positionDescriptor,
        int perspectiveSide,
        ushort cardResourceId,
        ushort fieldOccupantResourceId,
        bool objectVisible,
        int movementCounter,
        int animationState) =>
        ClassifyNative(positionDescriptor,perspectiveSide,cardResourceId,
            fieldOccupantResourceId,objectVisible,movementCounter,animationState)
            == NativeFieldAttributeDisposition.Advance;

    /// <summary>004194D0 resets to the base values for region+slot&gt;10,
    /// non-monsters or hidden enemy cards. Identity mismatch skips that reset.
    /// Outer Draw gating in 00418EF0 never reaches either reset or tween.</summary>
    public static NativeFieldAttributeDisposition ClassifyNative(
        ushort positionDescriptor, int perspectiveSide,
        ushort cardResourceId, ushort fieldOccupantResourceId,
        bool objectVisible, int movementCounter, int animationState)
    {
        if (perspectiveSide is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(perspectiveSide));
        if (!objectVisible || movementCounter != SettledCounter ||
            animationState == ExitAnimationState)
            return NativeFieldAttributeDisposition.Preserve;

        int region = (positionDescriptor >> 1) & 0x1f;
        int slot = (positionDescriptor >> 6) & 0xff;
        if (region + slot > 10 || region != 0)
            return NativeFieldAttributeDisposition.ResetToBase;

        int cardSide = positionDescriptor & 1;
        if (cardSide != perspectiveSide &&
            (positionDescriptor & FaceUpDescriptorFlag) == 0)
            return NativeFieldAttributeDisposition.ResetToBase;

        return cardResourceId == fieldOccupantResourceId
            ? NativeFieldAttributeDisposition.Advance
            : NativeFieldAttributeDisposition.Preserve;
    }

    /// <summary>
    /// Client-side adaptation when cards are already resolved into Core
    /// board slots. All currently rendered numbers must have a settled
    /// on-board card, be visible to the human player, and not leak the
    /// pending flip of an enemy defender before sword contact.
    ///
    /// The human always sees the stats of own Set monsters. This predicate
    /// deliberately does not gate unrelated effect/status overlays.
    /// </summary>
    public static bool ShouldDrawClient(
        bool ownMonster, bool faceDown, bool settled,
        bool hiddenDefenderBeforeImpact) =>
        ClassifyClient(true,ownMonster,faceDown,settled,hiddenDefenderBeforeImpact)
            == NativeFieldAttributeDisposition.Advance;

    /// <summary>Client slot adaptation: a vacated slot discards its cache.
    /// A populated slot with an in-flight card preserves it; the next Sample
    /// validates identity. A hidden settled enemy resets lazily to base.</summary>
    public static NativeFieldAttributeDisposition ClassifyClient(
        bool occupied, bool ownMonster, bool faceDown, bool settled,
        bool hiddenDefenderBeforeImpact)
    {
        if (!occupied) return NativeFieldAttributeDisposition.ResetToBase;
        if (!settled) return NativeFieldAttributeDisposition.Preserve;
        return ownMonster || (!faceDown && !hiddenDefenderBeforeImpact)
            ? NativeFieldAttributeDisposition.Advance
            : NativeFieldAttributeDisposition.ResetToBase;
    }
}
