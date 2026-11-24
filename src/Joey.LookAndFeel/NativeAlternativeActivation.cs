namespace Joey.LookAndFeel;

/// <summary>
/// Separate object 00450730 / 00450A90, not a stretched normal glow.
/// Counter/sound and Draw 00450AD0 are recovered from bundle 18.
/// Card-bank dimensions and category are supplied by the caller.
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

    // 00450ADE..00450C19: the large card is drawn FIRST. The
    // inherited 004505D0 glow is drawn only for 0 <= t <= 256.
    // The scratch dimensions are initialized from HALF the large
    // card-bank dimensions by 00450260 (descriptor 8016).
    public NativeAlternativeActivationDraw? Sample(int cardWidth,int cardHeight,
        int category,ReadOnlySpan<int> cosine)
    {
        if (!Active) return null;
        if (cardWidth<=0 || cardHeight<=0)
            throw new ArgumentOutOfRangeException(nameof(cardWidth));
        if (category is <0 or >1) throw new ArgumentOutOfRangeException(nameof(category));
        NativeFusionMath.Validate(cosine);
        int scale = Counter<0 ? NativeFusionMath.C(cosine,Counter)>>9 : 128;
        int intensity = Counter<256 ? 2*scale : NativeFusionMath.C(cosine,Counter-256)>>8;
        int w=unchecked(cardWidth*scale)>>8, h=unchecked(cardHeight*scale)>>8;
        var bounds=new CardBounds(Center.X-w/2,Center.Y-h/2,w,h);
        var scratch=new CardBounds(Center.X-(cardWidth/2)/2,Center.Y-(cardHeight/2)/2,
            cardWidth/2,cardHeight/2);
        NativeActivationFrame? glow=Counter is >=0 and <=256 ?
            NativeActivationFrame.FromCounter(Counter,category,cosine) : null;
        return new(Counter,scale,intensity,bounds,scratch,glow);
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

public readonly record struct NativeAlternativeActivationDraw(int Counter,int ScaleFixed8,
    int CardIntensity,CardBounds CardBounds,CardBounds ScratchBounds,NativeActivationFrame? Glow);

/// <summary>
/// Constructor 0043D810 (exclusion.txt). Its Update slot is a no-op;
/// 0043DBE0 increments in Draw, not Update. Never substitute explosion.
/// The private native card argument is intentionally not exposed for
/// hidden descriptors, including through debug/capture callers.
/// </summary>
public readonly record struct NativeExclusionStart
{
    public ushort Descriptor { get; }
    public uint VisibleCardArgument { get; }
    private NativeExclusionStart(ushort descriptor,uint visibleCardArgument)
    {
        Descriptor=descriptor;
        VisibleCardArgument=visibleCardArgument;
    }
    public const int InitialCounter = 64;
    public const int Layer = 0x120;
    public const string Manifest = @"j\duel\effect\exclusion.txt";
    public bool FaceUp => (Descriptor & 0x8000) != 0;

    public static NativeExclusionStart FromConstructor(ushort descriptor, uint nativeCardArgument) =>
        new(descriptor, (descriptor & 0x8000) != 0 ? nativeCardArgument : 0);
}
