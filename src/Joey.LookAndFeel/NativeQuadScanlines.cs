namespace Joey.LookAndFeel;

public readonly record struct NativeQuadVertex(CardPoint Destination, CardPoint Source);
public readonly record struct NativeQuadSample(int X,int Y,int U,int V);

/// <summary>Decompilation-derived convex quad subset of 0052CF10/00433150.
/// Vertices in cyclic order, integer coordinates, source UV in pixels.
/// Native source flag adds one to UV denominators but not geometry.
/// Candidate sampler, not a verified native raster or general polygon library.</summary>
public static class NativeQuadScanlines
{
    private const long One=1L<<20,Half=One/2;

    private readonly record struct Edge(NativeQuadVertex Start,NativeQuadVertex End)
    {
        public (long X,long U,long V) At(int y,bool extraDivisor)
        {
            int dy=End.Destination.Y-Start.Destination.Y;
            int k=y-Start.Destination.Y;
            long x=Start.Destination.X*One+Half+
                (End.Destination.X-Start.Destination.X)*One/dy*k;
            int divisor=dy+(extraDivisor?1:0);
            long u=Start.Source.X*One+Half+(End.Source.X-Start.Source.X)*One/divisor*k;
            long v=Start.Source.Y*One+Half+(End.Source.Y-Start.Source.Y)*One/divisor*k;
            return (x,u,v);
        }
    }

    private static long Cross(CardPoint a,CardPoint b,CardPoint c) =>
        (long)(b.X-a.X)*(c.Y-b.Y)-(long)(b.Y-a.Y)*(c.X-b.X);

    /// <summary>Half-open clip; native polygon endpoints themselves are
    /// inclusive. Adapter limits inputs to signed fixed-point-safe canvas
    /// coordinates and strictly convex nondegenerate quads. Returns samples
    /// in native row order, including keyed pixels (caller decides stores).</summary>
    public static IEnumerable<NativeQuadSample> Samples(
        IReadOnlyList<NativeQuadVertex> vertices,CardBounds clip,bool extraDivisor=false)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if(vertices.Count!=4)throw new InvalidDataException("Expected four cyclic vertices.");
        if(clip.X<0||clip.Y<0||clip.Width<0||clip.Height<0||
            (long)clip.X+clip.Width>1024||(long)clip.Y+clip.Height>1024)
            throw new InvalidDataException("Clip outside supported 1024x1024 logical canvas.");
        var points=vertices.ToArray();
        foreach(var p in points)
            if(p.Destination.X is < -1023 or >1023 || p.Destination.Y is < -1023 or >1023 ||
                p.Source.X is <0 or >1023 || p.Source.Y is <0 or >1023)
                throw new InvalidDataException("Vertex outside supported fixed-point domain.");
        long winding=Cross(points[0].Destination,points[1].Destination,points[2].Destination);
        if(winding==0)throw new InvalidDataException("Degenerate quad.");
        for(int i=1;i<4;i++)
            if(Math.Sign(Cross(points[i].Destination,points[(i+1)%4].Destination,
                points[(i+2)%4].Destination))!=Math.Sign(winding))
                throw new InvalidDataException("Expected strictly convex cyclic quad.");

        // Native chain traversal uses cross-product orientation. On a
        // clockwise screen-space polygon, downward forward edges form
        // the right chain; upward forward edges form the left chain.
        List<Edge> left=[],right=[];
        for(int i=0;i<4;i++)
        {
            var a=points[i];var b=points[(i+1)%4];
            if(a.Destination.Y==b.Destination.Y)continue;
            bool forwardDown=a.Destination.Y<b.Destination.Y;
            var edge=forwardDown?new Edge(a,b):new Edge(b,a);
            if(forwardDown==(winding>0))right.Add(edge);else left.Add(edge);
        }
        int top=points.Min(p=>p.Destination.Y),bottom=points.Max(p=>p.Destination.Y);
        if(clip.Width==0||clip.Height==0)yield break;
        long clipLeft=clip.X*One+Half,clipRight=(clip.X+clip.Width)*One-Half;
        // Active segment changes at intermediate vertices, not one row later.
        // Final segment is evaluated at its inclusive last row (native duplicate).
        Edge Active(List<Edge> chain,int y) => chain.Single(e=>
            e.Start.Destination.Y<=y && (y<e.End.Destination.Y ||
            (y==bottom&&e.End.Destination.Y==bottom)));
        for(int y=Math.Max(top,clip.Y);y<=Math.Min(bottom,clip.Y+clip.Height-1);y++)
        {
            var l=Active(left,y).At(y,extraDivisor);
            var r=Active(right,y).At(y,extraDivisor);
            if(l.X>clipRight||clipLeft>r.X)continue;
            int x0=(int)(l.X>>20),x1=(int)(r.X>>20);
            int u0=(int)(l.U>>20),v0=(int)(l.V>>20);
            int w=x1-x0;
            long du=w==0?0:(((r.U>>20)-u0)*One)/(w+(extraDivisor?1:0));
            long dv=w==0?0:(((r.V>>20)-v0)*One)/(w+(extraDivisor?1:0));
            // Original left clip uses fixed-point difference before shift,
            // not the integer difference clip.X - roundedLeftX.
            long skip=0;
            if(l.X<clipLeft){skip=(clipLeft-l.X)>>20;x0=clip.X;}
            if(r.X>clipRight)x1=clip.X+clip.Width-1;
            for(int x=x0;x<=x1;x++,skip++)
                yield return new(x,y,u0+(int)((skip*du)>>20),v0+(int)((skip*dv)>>20));
        }
    }
}

/// <summary>Diagnostic CPU BGR24 raster composed from the same-format
/// keyed-copy and intensity kernels. Prevalidate all samples before any
/// store. No GPU, source alpha, vtable mapping or gameplay integration.</summary>
public static class NativeBgr24QuadRaster
{
    public static int Blit(NativeCpuSurface source,NativeCpuSurface destination,
        IReadOnlyList<NativeQuadVertex> vertices,uint rawColorKey,int intensity,
        CardBounds? clip=null,bool extraDivisor=false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);
        if(source.Format!=NativeSurfaceFormat.Bgr24||destination.Format!=NativeSurfaceFormat.Bgr24)
            throw new InvalidDataException("Only same-format BGR24 raster is supported.");
        if(ReferenceEquals(source.Pixels,destination.Pixels))
            throw new InvalidDataException("Overlapping surface storage is unsupported.");
        var c=clip??new CardBounds(0,0,destination.Width,destination.Height);
        if(c.X<0||c.Y<0||c.Width<0||c.Height<0||
            (long)c.X+c.Width>destination.Width||(long)c.Y+c.Height>destination.Height)
            throw new InvalidDataException("Clip outside destination.");
        var samples=NativeQuadScanlines.Samples(vertices,c,extraDivisor).ToArray();
        if(samples.Any(s=>s.U<0||s.U>=source.Width||s.V<0||s.V>=source.Height))
            throw new InvalidDataException("Raster source sample outside surface.");
        int stores=0;
        foreach(var s in samples)
        {
            var input=source.Pixel(s.U,s.V);
            var output=destination.Pixel(s.X,s.Y);
            if(intensity<256)
            {
                if(NativeIntensityPixel.Apply(input,output,NativeSurfaceFormat.Bgr24,rawColorKey,intensity))
                    stores++;
            }
            else
            {
                uint pixel=(uint)(input[0]|input[1]<<8|input[2]<<16);
                if(pixel==(rawColorKey&0xffffffu))continue;
                input.CopyTo(output);stores++;
            }
        }
        return stores;
    }
}
