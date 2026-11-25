namespace Joey.LookAndFeel;

/// <summary>Recovered scratch operations, not a blended image.
/// Slots +94 and +A0 are unresolved; preserve the raw arguments.
/// Clear 004B1160 uses the scratch's current key, not an inferred RGBA.
/// </summary>
public readonly record struct NativeCoinReflection(
    CardBounds Scratch, int ReflectionKey, CardPoint CopyOrigin,
    int MaskKey, int MaskSlot, IReadOnlyList<int> MaskArguments,
    CardPoint Destination, int CompositeSlot, IReadOnlyList<int> CompositeArguments);

public readonly record struct NativeCoinExtras(
    NativeCoinReflection? Reflection, IReadOnlyList<NativeCoinPass> Sparkles);

/// <summary>00457D07..004580A2, assembly-confirmed geometry.
/// This model emits the ordered scratch plan and actual gated key-5
/// rectangles; it does not assign semantics to the unknown virtual slots.
/// </summary>
public static class NativeCoinExtraLayout
{
    private static readonly int[] Frames=[3,2,1,0,0,1,2,3];

    public static NativeCoinExtras Sample(NativeCoinTimeline timeline,int index,
        NativeCoinDraw primary,Func<int,NativeDiceTexture> resource)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(resource);
        if(!timeline.Active||index<0||index>=timeline.Count||
            primary.Coin.LocalCounter!=timeline.Counter-index*128)
            throw new InvalidDataException("Mismatched coin sample.");
        if(!primary.SparkleBranchReached)return new(null,[]);
        int remaining=timeline.EndCounter-timeline.Counter;
        int cx=primary.Coin.Center.X,cy=primary.Coin.Center.Y;
        var face=resource(0);
        int w=face.PatternWidth,h=face.Height;
        NativeCoinReflection? reflection=null;
        if(primary.ReflectionScratchRequired)
        {
            var mask=resource(3).Whole;
            var image=resource(4).Whole;
            int x=(mask.Width+image.Width)*(640-2*remaining)/256-image.Width;
            // 00457E82..00457EA8 uses half mask WIDTH for both axes.
            reflection=new(new(0,0,w,h),4,new(x,0),3,0x94,
                new[]{0,255,0,255,0},new(cx-mask.Width/2,cy-mask.Width/2),
                0xa0,new[]{0,0,0});
        }

        List<NativeCoinPass> sparkles=[];
        for(int j=0;j<5;j++)
        {
            // Global counter-end, not the per-coin local counter.
            int t=256-remaining-16*j;
            if(t<0||t>=64)continue;
            var source=resource(5).Pattern(Frames[t/8]);
            int x=cx+j*w/4-source.Width/2-w/2;
            int y=cy+((3*j+2*index)%8)*h/8-source.Height/2-h/2;
            // +4C has null destination-size and zero final argument.
            // Parameter256 is a diagnostic full-intensity representation,
            // not a native alpha parameter for this virtual slot.
            sparkles.Add(new(5,0x4c,source,new(x,y,source.Width,source.Height),256));
        }
        return new(reflection,sparkles);
    }
}
