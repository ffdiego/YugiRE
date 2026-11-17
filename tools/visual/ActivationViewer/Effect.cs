using System.Buffers.Binary;
using Joey.Core;
namespace ActivationViewer;

public sealed record Raster(int Width, int Height, byte[] Rgba)
{
    public static Raster Yga(byte[] data)
    {
        if (data.Length < 24 || !data.AsSpan(0, 4).SequenceEqual("yga\0"u8)) throw new InvalidDataException("Invalid YGA");
        int U(int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at, 4));
        int width=U(4), height=U(8), mode=U(12), size=U(16), stored=U(20);
        if (width is <1 or >4096 || height is <1 or >4096 || (long)width*height*4 != size || size > 16*1024*1024)
            throw new InvalidDataException("Invalid YGA geometry");
        byte[] pixels;
        if (mode == 1 && stored == data.Length-24) pixels = Archive.Decode(data.AsSpan(24), size);
        else if (mode == 0 && (stored == 0 || stored == size) && data.Length-24 == size) pixels=data[24..];
        else throw new InvalidDataException("Unsupported YGA mode/size");
        for(int i=0;i<pixels.Length;i+=4) (pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);
        return new(width,height,pixels);
    }
}
public readonly record struct Point(int X,int Y);
public sealed record EffectFrame(int Counter, int X, int Y, int CardWidth, int CardHeight, double? Seconds=null);

public static class Effect
{
    public const int Width=800, Height=600;
    public static int[] Cosines() => Enumerable.Range(0,512).Select(i=>(int)(Math.Cos(i*Math.PI/256.0)*65536.0)).ToArray();
    // 00422C00 calls Win32 MulDiv: round to nearest; positive inputs in this effect.
    public static int MulDiv(int value,int scale)
    {
        long product=(long)value*scale;
        return checked((int)(product>=0 ? (product+32768)/65536 : -((-product+32768)/65536)));
    }
    public static Point[] Quad(int width,int height,int x,int y,int angle,int scale,int[] table)
    {
        int w=MulDiv(width-1,scale), h=MulDiv(height-1,scale);
        if(w==0 || h==0) return [];
        int c=table[angle&511], s=table[(angle-128)&511];
        Point[] local=[new(-w/2,-h/2),new((w+1)/2,-h/2),new((w+1)/2,(h+1)/2),new(-w/2,(h+1)/2)];
        return local.Select(p=>new Point(x+unchecked(p.X*c-p.Y*s>>16),y+unchecked(p.Y*c+p.X*s>>16))).ToArray();
    }
    // 00546D70 initializes both LUTs. C# signed division truncates toward zero.
    public static byte Blend(byte destination,byte source,int alpha,int opacity)
    {
        if(alpha is <0 or >255 || opacity is <0 or >255) throw new ArgumentOutOfRangeException();
        int effective=alpha*opacity/255;
        return checked((byte)(destination+(source-destination)*effective/255));
    }
    private const long One=1<<20, Half=One/2;
    private readonly record struct Edge(long X,long U,long V);
    private static Edge OnEdge(Point a,Point b,Point ua,Point ub,int y)
    {
        if(a.Y>b.Y) { (a,b)=(b,a); (ua,ub)=(ub,ua); }
        int dy=b.Y-a.Y, n=y-a.Y;
        // Fixed 20 fractional bits, per-edge truncation as in 004276F0.
        return new(a.X*One+Half+(b.X-a.X)*One/dy*n,
                   ua.X*One+Half+(ub.X-ua.X)*One/dy*n,
                   ua.Y*One+Half+(ub.Y-ua.Y)*One/dy*n);
    }
    public static void Polygon(byte[] destination,int destWidth,int destHeight,Raster source,Point[] quad,int opacity)
    {
        if(destination.Length!=(long)destWidth*destHeight*4 || source.Rgba.Length!=(long)source.Width*source.Height*4 || quad.Length!=4)
            throw new InvalidDataException("Invalid raster/quad");
        if(opacity is <0 or >255) throw new ArgumentOutOfRangeException(nameof(opacity));
        Point[] uv=[new(0,0),new(source.Width-1,0),new(source.Width-1,source.Height-1),new(0,source.Height-1)];
        int top=quad.Min(p=>p.Y), bottom=quad.Max(p=>p.Y);
        if(bottom-top>8192 || quad.Max(p=>p.X)-quad.Min(p=>p.X)>8192) throw new InvalidDataException("Unbounded quad");
        for(int y=Math.Max(0,top);y<=Math.Min(destHeight-1,bottom);y++)
        {
            var edges=new List<Edge>(4);
            for(int i=0;i<4;i++)
            {
                int j=(i+1)%4;
                if(quad[i].Y==quad[j].Y) continue;
                int lo=Math.Min(quad[i].Y,quad[j].Y), hi=Math.Max(quad[i].Y,quad[j].Y);
                if(y>=lo && (y<hi || y==bottom)) edges.Add(OnEdge(quad[i],quad[j],uv[i],uv[j],y));
            }
            if(edges.Count<2) continue;
            var left=edges.MinBy(e=>e.X); var right=edges.MaxBy(e=>e.X);
            int x0=(int)(left.X>>20), x1=(int)(right.X>>20), span=x1-x0;
            long du=span==0?0:((right.U&~(One-1))-(left.U>>20)*One)/span;
            long dv=span==0?0:((right.V&~(One-1))-(left.V>>20)*One)/span;
            int u0=(int)(left.U>>20), v0=(int)(left.V>>20);
            for(int x=Math.Max(0,x0);x<=Math.Min(destWidth-1,x1);x++)
            {
                int n=x-x0, u=u0+(int)(du*n>>20), v=v0+(int)(dv*n>>20);
                // Native routines rely on validated source quads; guard every access here.
                if(u<0 || v<0 || u>=source.Width || v>=source.Height) continue;
                int src=(v*source.Width+u)*4, dst=(y*destWidth+x)*4;
                // YGA wrappers default to RGB color key zero (004A9CC0 / 004276F0).
                if(source.Rgba[src]==0 && source.Rgba[src+1]==0 && source.Rgba[src+2]==0) continue;
                int alpha=source.Rgba[src+3];
                for(int channel=0;channel<3;channel++) destination[dst+channel]=Blend(destination[dst+channel],source.Rgba[src+channel],alpha,opacity);
                destination[dst+3]=255;
            }
        }
    }
    public static byte[] Render(EffectFrame frame,Raster lower,Raster upper,byte[] background,int[] table)
    {
        if(frame.Counter is <0 or >256 || table.Length!=512 || background.Length!=Width*Height*4 ||
           frame.CardWidth is <1 or >512 || frame.CardHeight is <1 or >512 || frame.X is <0 or >=Width || frame.Y is <0 or >=Height)
            throw new InvalidDataException("Frame outside supported activation contract");
        var output=(byte[])background.Clone();
        int scale=table[(frame.Counter-128)&511];
        if(scale<0 || scale>65536) throw new InvalidDataException("Invalid cosine amplitude");
        int opacity=Math.Min(scale>>8,255);
        var q=Quad(lower.Width,lower.Height,frame.X,frame.Y,frame.Counter/2,scale,table);
        if(q.Length!=0) Polygon(output,Width,Height,lower,q,opacity);
        // The scratch surface is black with WHITE color key, not an extra YGA.
        int x0=frame.X-frame.CardWidth/2, y0=frame.Y-frame.CardHeight/2, shade=scale>>9;
        for(int y=Math.Max(0,y0);y<Math.Min(Height,y0+frame.CardHeight);y++)
            for(int x=Math.Max(0,x0);x<Math.Min(Width,x0+frame.CardWidth);x++)
                for(int channel=0;channel<3;channel++)
                {
                    int at=(y*Width+x)*4+channel;
                    output[at]=Blend(output[at],0,255,shade);
                }
        q=Quad(upper.Width,upper.Height,frame.X,frame.Y,frame.Counter,scale,table);
        if(q.Length!=0) Polygon(output,Width,Height,upper,q,opacity);
        return output;
    }
    public static byte[] Checkerboard()
    {
        var pixels=new byte[Width*Height*4];
        for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
        {
            byte value=(byte)(((x/16+y/16)&1)==0?38:58);
            int i=(y*Width+x)*4; pixels[i]=pixels[i+1]=pixels[i+2]=value; pixels[i+3]=255;
        }
        return pixels;
    }
}
