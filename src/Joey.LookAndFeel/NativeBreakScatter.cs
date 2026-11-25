namespace Joey.LookAndFeel;

public enum NativeBreakScatterKind { None, Fragmented, Direct }

/// <summary>Raw 00558FB0 tile before native radial normalization.
/// </summary>
public readonly record struct NativeBreakBlock(CardBounds Source,
    int RadialX,int RadialY,int RadiusSquared,int NoiseIndexX,int NoiseIndexY,
    byte NoiseX,byte NoiseY);

/// <summary>Static contract recovered from decompilation of 00558FB0,
/// 00558BC0, 0055C020, 00558BF0, 005C3BF5 and 005C3C02.
/// Recipe 29 confirms FSQRT, truncating CRT __ftol and signed IDIV.
/// No renderer or rules RNG.</summary>
public static class NativeBreakScatter
{
    private static readonly byte[] noise=GenerateNoise();

    public static byte[] GenerateNoise()
    {
        // 00558BF0 calls CRT srand(0), then stores the low byte of
        // 256 consecutive rand() outputs. Local state: do not mutate
        // either the duel RNG or a global UI random stream.
        uint state=0;
        byte[] result=new byte[256];
        for(int i=0;i<result.Length;i++)
        {
            state=unchecked(state*0x343fdu+0x269ec3u);
            result[i]=(byte)((state>>16)&0xffu);
        }
        return result;
    }

    public static NativeBreakScatterKind Classify(int parameter) =>
        parameter<1 ? NativeBreakScatterKind.None :
        parameter>=256 ? NativeBreakScatterKind.Direct :
        NativeBreakScatterKind.Fragmented;

    /// <summary>Raw row-major source rectangles; preserve partial edge
    /// fragments and two different row-dependent table offsets.</summary>
    public static IEnumerable<NativeBreakBlock> Blocks(int width,int height)
    {
        if(width is <1 or >4096 || height is <1 or >4096)
            throw new ArgumentOutOfRangeException(nameof(width));
        for(int y=0;y<height;y+=2)
        {
            int row=y/2;
            for(int x=0;x<width;x+=4)
            {
                int nx=(26*row+x)&255,ny=(1+38*row+x)&255;
                int rx=x-width/2,ry=y-height/2;
                yield return new(new(x,y,Math.Min(4,width-x),Math.Min(2,height-y)),
                    rx,ry,rx*rx+ry*ry,nx,ny,noise[nx],noise[ny]);
            }
        }
    }

    /// <summary>00559110..0055911B: FILD, FSQRT, then 005C3E40
    /// forces x87 RC=11 (truncate toward zero). The argument is a
    /// nonnegative integer, so the result is floor(sqrt(n)).
    /// Integer corrections make the result independent of libm rounding.</summary>
    public static int RadialDivisor(int radiusSquared)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusSquared);
        int root=(int)Math.Sqrt(radiusSquared);
        while ((long)(root+1)*(root+1)<=radiusSquared) root++;
        while ((long)root*root>radiusSquared) root--;
        return root;
    }

    /// <summary>Native block placement relative to image top-left.
    /// Composition and visibility are the caller's responsibility.</summary>
    public static CardBounds Project(NativeBreakBlock block,CardPoint origin,
        int width,int height,int parameter) =>
        Project(block,origin,width,height,parameter,RadialDivisor(block.RadiusSquared));

    /// <summary>Explicit divisor overload retained for diagnostic comparisons.</summary>
    public static CardBounds Project(NativeBreakBlock block,CardPoint origin,
        int width,int height,int parameter,int radialDivisor)
    {
        if(width is <1 or >4096 || height is <1 or >4096)
            throw new ArgumentOutOfRangeException(nameof(width));
        if(Classify(parameter)!=NativeBreakScatterKind.Fragmented)
            throw new ArgumentOutOfRangeException(nameof(parameter));
        ArgumentOutOfRangeException.ThrowIfNegative(radialDivisor);
        int rx=block.RadialX,ry=block.RadialY;
        if(radialDivisor!=0)
        {
            rx=rx*256/radialDivisor;
            ry=ry*256/radialDivisor;
        }
        int sum=width+height;
        int extent=sum-((sum*parameter)>>8);
        int dx=unchecked(block.NoiseX*rx*extent)>>16;
        int dy=unchecked(block.NoiseY*ry*extent)>>16;
        return new(origin.X+block.Source.X+dx,origin.Y+block.Source.Y+dy,
            block.Source.Width,block.Source.Height);
    }
}
