namespace Joey.LookAndFeel;

/// <summary>00456740: transient full-card image from the source S/T
/// slot to the equipped monster. Counter starts at 0; Update +8.
/// Draw at 192 still paints, requests sound, then clears active.
/// The host owns the terminal Draw acknowledgment and command gate.</summary>
public readonly record struct NativeEquipmentTravelFrame(int Counter,CardPoint Center,
    int ScaleFixed16,int RotationIndex,int Intensity,uint? SoundRequest,bool Terminal);

public static class NativeEquipmentTravel
{
    public const int Increment=8, EndCounter=192, Updates=24, Layer=0x110;
    public const uint FinishSound=0x1000000a;

    public static NativeEquipmentTravelFrame Sample(int counter,CardPoint source,CardPoint target,
        int sourceSide,int perspectiveBit,ReadOnlySpan<int> cosine)
    {
        if(counter<0 || counter>EndCounter || counter%Increment!=0)
            throw new ArgumentOutOfRangeException(nameof(counter));
        if(sourceSide is not (0 or 1) || perspectiveBit is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(sourceSide));
        NativeFusionMath.Validate(cosine);
        int t=counter-128;
        int angle=counter<128?t:2*t;
        // IDIV-like truncation towards zero (004567BC..C5).
        int scale=(256+NativeFusionMath.C(cosine,angle)/256)<<6;
        int weight=counter<128?0:4*t;
        var center=new CardPoint(source.X+(target.X-source.X)*weight/256,
            source.Y+(target.Y-source.Y)*weight/256);
        bool terminal=counter==EndCounter;
        return new(counter,center,scale,256*(sourceSide^perspectiveBit),128,
            terminal?FinishSound:null,terminal);
    }
}
