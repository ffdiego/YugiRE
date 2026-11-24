namespace Joey.LookAndFeel;

/// <summary>
/// Native j/duel/card/card.txt index bank, loaded by 004182E0.
/// 0..3: existing card selection frames; 4..13: status/change overlays.
///
/// These are resource *indices*, not guessed bitmap filenames or draw
/// coordinates. Their associated positions, blends and actual manifest
/// entries are pending DAT/assembly correlation. No draw is claimed here.
/// </summary>
public static class NativeCardStatusOverlay
{
    // 00419B70 compares effective type/attribute against CARD_Prop and
    // draws the corresponding icons only when the effective value changed.
    public const int ChangedAttributeIndex = 4;
    public const int ChangedTypeIndex = 5;

    // 0041A070: source bits [2..5], state 3, frames 6 and 7.
    public const int FirstStatusIndex = 6;
    public const int FirstStatusTailIndex = 7;
    public const int FirstTransitionState = 3;

    // 0041A3B0: source bits [6..8], state 4, frames 8 and 9.
    public const int SecondStatusIndex = 8;
    public const int SecondStatusTailIndex = 9;
    public const int SecondTransitionState = 4;

    // 004194D0: animated +/- changes to ATK/DEF in the same card bank.
    // Even/odd variant selection comes from the native global frame count.
    public const int PositiveChangeFirstIndex = 10;
    public const int PositiveChangeSecondIndex = 11;
    public const int NegativeChangeFirstIndex = 12;
    public const int NegativeChangeSecondIndex = 13;

    public static NativeCardIdentityOverlays IdentityChanges(
        int originalClassCode, int effectiveClassCode,
        int originalAttributeCode, int effectiveAttributeCode)
    {
        if (originalClassCode < 0 || effectiveClassCode < 0 ||
            originalAttributeCode < 0 || effectiveAttributeCode < 0)
            throw new ArgumentOutOfRangeException(nameof(originalClassCode));

        // 00419B70: 0 < effective type < 0x15, 0 < attribute < 7.
        // Special card ID 0x48A has another branch requiring DAT identity;
        // that branch intentionally remains outside this model.
        return new(
            ShowType: effectiveClassCode is > 0 and < 0x15 &&
                      effectiveClassCode != originalClassCode,
            ShowAttribute: effectiveAttributeCode is > 0 and < 7 &&
                           effectiveAttributeCode != originalAttributeCode);
    }

    /// <summary>
    /// 0041A070. The first counter comes from byte flags [2..5] in
    /// 00A55D7A. The native object may also enter special state 3 even
    /// if the counter is zero. Only frame routing is currently modeled.
    /// </summary>
    public static NativeCardStatusFrame? First(
        ushort fieldFlags, int animationState, int animationCounter,
        int nativeGlobalCounter)
    {
        ValidateCounters(animationCounter, nativeGlobalCounter);
        int counter = (fieldFlags >> 2) & 0xf;
        if (counter == 0 && animationState != FirstTransitionState) return null;
        int mainFrame = animationState == FirstTransitionState &&
                        animationCounter < 0x100
            ? animationCounter / 0x40 + 2
            : (nativeGlobalCounter >> 4) % 3;
        bool tail = animationState == FirstTransitionState &&
                    animationCounter > 0xff;
        return new(FirstStatusIndex, mainFrame, counter,
            tail ? FirstStatusTailIndex : null,
            tail ? (-animationCounter >> 4) & 7 : null);
    }

    /// <summary>
    /// 0041A3B0. The second counter comes from flag bits [6..8].
    /// Frame 8 is fixed at 0 except during state-4 transition, where
    /// (t / 64) selects frames 0..3; frame 9 is the later 8-frame tail.
    /// </summary>
    public static NativeCardStatusFrame? Second(
        ushort fieldFlags, int animationState, int animationCounter)
    {
        if (animationCounter < 0)
            throw new ArgumentOutOfRangeException(nameof(animationCounter));
        int counter = (fieldFlags & 0x1c0) >> 6;
        if (counter == 0 && animationState != SecondTransitionState) return null;
        int mainFrame = animationState == SecondTransitionState &&
                        animationCounter < 0x100
            ? animationCounter / 0x40 : 0;
        bool tail = animationState == SecondTransitionState &&
                    animationCounter > 0xff;
        return new(SecondStatusIndex, mainFrame, counter,
            tail ? SecondStatusTailIndex : null,
            tail ? (-animationCounter >> 4) & 7 : null);
    }

    public static int AttributeChangeFlashIndex(bool increased, int nativeGlobalCounter)
    {
        if (nativeGlobalCounter < 0)
            throw new ArgumentOutOfRangeException(nameof(nativeGlobalCounter));
        // 004194D0 uses (global >> 2) & 1 for both sign variants.
        int variant = (nativeGlobalCounter >> 2) & 1;
        return (increased ? PositiveChangeFirstIndex : NegativeChangeFirstIndex) + variant;
    }

    private static void ValidateCounters(int phase, int global)
    {
        if (phase < 0) throw new ArgumentOutOfRangeException(nameof(phase));
        if (global < 0) throw new ArgumentOutOfRangeException(nameof(global));
    }
}

/// <summary>00419B70's independently gated type and attribute change icons.</summary>
public readonly record struct NativeCardIdentityOverlays(bool ShowType, bool ShowAttribute);

/// <summary>
/// A storyboard *cue*, not a client draw command: the actual bitmap path,
/// source frame width, x/y offset and native blend are still unknown.
/// </summary>
public readonly record struct NativeCardStatusFrame(
    int MainResourceIndex, int MainFrame, int FieldValue,
    int? TailResourceIndex, int? TailFrame);
