namespace Joey.LookAndFeel;

/// <summary>
/// Separate object 00450730 / 00450A90, not a stretched normal glow.
/// Counter and sound request are recovered from existing decompilation.
/// Draw 00450AD0 is still unknown: this class exposes no invented passes.
/// </summary>
public sealed class NativeAlternativeActivationTimeline
{
    public const int InitialCounter = -128;
    public const int Increment = 16;
    public const int EndThreshold = 385;
    public const int Updates = 33;
    public const int Layer = 0x110;
    public const uint ActivationSound = 0x1000000D;
    // 005DB570 = 525 confirmed by the selection/fusion bundle.
    public static readonly CardPoint Center = new(525, 300);

    public int Counter { get; private set; } = InitialCounter;
    public bool Active => Counter < EndThreshold;

    /// <summary>
    /// Return the native sound request only on the Update whose input
    /// counter is zero. The caller decides whether audio is enabled.
    /// Independent of Draw, time units, and command completion.
    /// </summary>
    public uint? AdvanceNativeUpdate()
    {
        if (!Active) return null;
        uint? sound = Counter == 0 ? ActivationSound : null;
        Counter += Increment;
        return sound;
    }

    /// <summary>00406B20 stage 2 fallback; all facts supplied by caller.
    /// The index is the internal catalog index, not the resource ID.</summary>
    public static bool UsesAlternative(int resolvedSlot, int requestedCardIndex,
        int? actualVisualCardIndex) =>
        resolvedSlot == -1 || actualVisualCardIndex != requestedCardIndex;

    public static bool AlwaysAlternative(ushort opcode) => opcode == 0x49;

    // 0040AA40 uses ordinary objects for every eligible visible occurrence;
    // when none were created, it creates one alternative object.
    public static bool UsesAlternativeForGroup(int eligibleVisualOccurrences)
    {
        if (eligibleVisualOccurrences < 0)
            throw new ArgumentOutOfRangeException(nameof(eligibleVisualOccurrences));
        return eligibleVisualOccurrences == 0;
    }
}

/// <summary>
/// Constructor 0043D810 (exclusion.txt). Its Update slot is a no-op;
/// do not attach a made-up duration or use an explosion as fallback.
/// The private native card argument is intentionally not exposed for
/// hidden descriptors, including through debug/capture callers.
/// </summary>
public readonly record struct NativeExclusionStart(ushort Descriptor, uint VisibleCardArgument)
{
    public const int InitialCounter = 64;
    public const int Layer = 0x120;
    public const string Manifest = @"j\duel\effect\exclusion.txt";
    public bool FaceUp => (Descriptor & 0x8000) != 0;

    public static NativeExclusionStart FromConstructor(ushort descriptor, uint nativeCardArgument) =>
        new(descriptor, (descriptor & 0x8000) != 0 ? nativeCardArgument : 0);
}
