namespace Joey.LookAndFeel;

/// <summary>Raw virtual slot, resource key before remap and integer geometry.
/// Parameter256 is the native argument, NOT a proven linear alpha.
/// Reflection scratch and sparkle geometry are modeled separately.</summary>
public readonly record struct NativeCoinPass(int Key,int VirtualSlot,
    CardBounds Source,CardBounds Destination,int Parameter256);

public readonly record struct NativeCoinDraw(NativeCoinSample Coin,
    IReadOnlyList<NativeCoinPass> Passes,bool ReflectionScratchRequired,
    bool SparkleBranchReached);

/// <summary>00457520 assembly, keys 0/1/2 before bank remap.
/// Accept actual texture dimensions/manifest divisors from host; no guessed
/// DAT names or composition, no state or random generation.</summary>
public static class NativeCoinPassLayout
{
    private static readonly int[] ShadowFrames=[1,2,3,3,2,1,0,1,2,3,3,2,1,0];

    public static NativeCoinDraw Sample(NativeCoinTimeline timeline,int index,
        int highlightBit,ReadOnlySpan<int> cosine,
        Func<int,NativeDiceTexture> resource)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(resource);
        if(highlightBit is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(highlightBit));
        var coin=timeline.Sample(index,cosine);
        int t=coin.LocalCounter,remaining=timeline.EndCounter-timeline.Counter;
        if(t<=-128)return new(coin,[],false,false);
        var face=resource(0);
        var shadow=resource(1);
        if(face.Patterns!=14 || shadow.Patterns<4)
            throw new InvalidDataException("V20 supports the 14-entry selector and at least four shadow frames; verify native manifest.");
        int x=coin.Center.X,y=coin.Center.Y;
        var passes=new List<NativeCoinPass>(3);
        if(t>0 && t<256)
        {
            // 00457727..00457774: selected result bit changes the
            // number of strip cycles. Frame order is not face identity.
            int cycles=coin.ResultBit?5*face.Patterns:4*face.Patterns+face.Patterns/2;
            int frame=(6+cycles*t/256)%face.Patterns;
            var ss=shadow.Pattern(ShadowFrames[frame]);
            int offset=coin.Bounce*25/256;
            passes.Add(new(1,0x4c,ss,new(x+offset-ss.Width/2,y+offset-ss.Height/2,ss.Width,ss.Height),256));
            var fs=face.Pattern(frame);
            int w=fs.Width*coin.ScaleFixed8/256,h=fs.Height*coin.ScaleFixed8/256;
            // 004579AF..004579E1: both axes subtract HALF HEIGHT.
            passes.Add(new(0,0x4c,fs,new(x-h/2,y-h/2-coin.Bounce*50/256,w,h),256));
            return new(coin,passes,false,false);
        }
        int intensity=t< -64?512+4*t:remaining<64?4*remaining:256;
        var flatShadow=shadow.Pattern(0);
        // 00457AD6..00457B05: center using shadow FRAME WIDTH,
        // +1 on both axes. Face uses its own frame width, no +1.
        passes.Add(new(1,0x48,flatShadow,
            new(x-flatShadow.Width/2+1,y-flatShadow.Width/2+1,flatShadow.Width,flatShadow.Height),intensity));
        var flatFace=face.Pattern(t>0&&!coin.ResultBit?13:6);
        passes.Add(new(0,0x48,flatFace,
            new(x-flatFace.Width/2,y-flatFace.Width/2,flatFace.Width,flatFace.Height),intensity));
        if(t>=256 && t<320)
        {
            // 00457BD2..00457CDB: key 2, size grows while argument fades.
            var halo=resource(2).Whole;
            int scale=2*t-256;
            int w=halo.Width*scale/256,h=halo.Height*scale/256;
            passes.Add(new(2,0x48,halo,new(x-w/2,y-h/2,w,h),4*(320-t)));
        }
        bool highlight=(coin.ResultBit?1:0)==highlightBit;
        // 00457CDE..00457D01: branch uses GLOBAL remaining, not local t.
        // 00457EAE falls through to 00457EB2 after reflection.
        // The key-5 loop is reached in BOTH cases, with independent gates.
        return new(coin,passes,highlight&&remaining is >192 and <=320,
            highlight);
    }
}
