namespace Joey.LookAndFeel;

/// <summary>00403310 routes raw native summon event types. The enum here
/// describes presentation requests, not rules or a queue. A separate
/// presentation RNG sample is injected only for the strong-monster path.
/// </summary>
public enum NativeJoeySummonKind { None, SoundOnly, Reaction, Signature }

public readonly record struct NativeJoeySummonRequest(
    NativeJoeySummonKind Kind, int ReactionNumber, string? SignatureManifest,
    uint PrimarySound, uint SecondarySound, uint ImmediateSound,
    bool UsesPresentationRandom);

public static class NativeJoeyCinematicRouting
{
    public const string Prefix = @"j\bust_up\";
    public const string StartManifest = Prefix+"start.txt";

    /// <summary>00403310 calls 00402460/004027B0 for printed ATK,
    /// not the current equipped/buffed field value. Keep eventType raw
    /// until the caller contracts are mapped to actions.
    /// </summary>
    public static NativeJoeySummonRequest Summon(bool matchesNativeSide, int resourceId,
        int printedAttack, int eventType, uint? presentationRandom = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(resourceId);
        if(printedAttack is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(printedAttack));

        if(!matchesNativeSide)
        {
            return eventType switch
            {
                1 or 2 => Reaction(printedAttack >= 2000 ? 1 : 0,0x10000054),
                4 => new(NativeJoeySummonKind.SoundOnly,0,null,0,0,0x10000058,false),
                5 => Reaction(1,0x10000055),
                _ => default
            };
        }
        if(resourceId is 0x51 or 0x2e8)
            return new(NativeJoeySummonKind.Signature,0,
                Prefix+(resourceId==0x51?"summon.txt":"summon2.txt"),
                0,0,0x2000000b,false);

        bool strongType = eventType is 1 or 2;
        bool alternate = resourceId is 0x2ef or 0x2f9 or 0x52e;
        int reaction = resourceId==0x44?2:0;
        uint secondary = resourceId switch
        {
            0x2ef => strongType ? 0x10000065u : 0x1000002fu,
            0xf => 0x10000061,
            0x44 => 0x10000032,
            0x4cf => 0x10000062,
            0x2f8 => 0x10000063,
            0x2f9 => 0x1000002e,
            _ when eventType==3 => 0x1000004d,
            _ => 0
        };

        if(!strongType || printedAttack<=0x739)
            return Reaction(reaction,secondary);

        // Even the fixed alternate/Jinzo branch consumes the native UI
        // RNG value before overriding its result. Never use the rules RNG.
        if(presentationRandom is not uint sample)
            throw new ArgumentException("Strong summon requires a presentation RNG sample.",nameof(presentationRandom));
        if(secondary==0) secondary=eventType==1?0x1000002cu:0x1000002bu;
        uint primary=0x10000028u+2*(sample&1);
        reaction=13;
        if(alternate){reaction=8;primary=0x10000029;}
        if(secondary==0x10000065){primary=secondary;secondary=0;}
        return new(NativeJoeySummonKind.Reaction,reaction,null,primary,
            secondary,0x2000000c,true);
    }

    private static NativeJoeySummonRequest Reaction(int number,uint sound) =>
        new(sound==0?NativeJoeySummonKind.None:
            number==0?NativeJoeySummonKind.SoundOnly:NativeJoeySummonKind.Reaction,
            number,null,sound,0,0,false);

    /// <summary>00415C90 branches on native result code, not a local-player
    /// win/loss enum. The returned names describe Joey's bust resources.
    /// </summary>
    public static (string Manifest,uint Sound) Result(int nativeResult) =>
        nativeResult switch
        {
            2 => (Prefix+"lose.txt",0x10000059),
            3 => (Prefix+"draw.txt",0x1000005c),
            _ => (Prefix+"win.txt",0x1000005d)
        };

    // 004152D0 always consumes one UI RNG sample for start.txt.
    public static int StartVariant(uint presentationRandom) => (int)(presentationRandom%3);

    // 00415C90 uses the unsigned remainder of the first bank parameter.
    // A zero divisor suppresses the random call. Do not infer the meaning
    // of this manifest field from the modulo operation.
    public static uint ResultVariantCount(uint firstBankParameter) => firstBankParameter%1000;
    public static uint ResultVariant(uint firstBankParameter,uint? presentationRandom)
    {
        uint count=ResultVariantCount(firstBankParameter);
        if(count==0)return 0;
        if(presentationRandom is not uint sample)
            throw new ArgumentException("Result requires a presentation RNG sample.",nameof(presentationRandom));
        return sample%count;
    }
}
