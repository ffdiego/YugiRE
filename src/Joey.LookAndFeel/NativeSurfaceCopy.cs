using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Subset of surface+74h format codes dispatched by 004AD9C0.
/// Code 7's fourth byte is storage, not an implied alpha channel.</summary>
public enum NativeSurfaceFormat { Rgb565=3, Rgb555=4, Bgr24=5, Bgrx32=7 }

/// <summary>CPU surface adapter, positive pitch and logical top-down rows.
/// Does not own GPU resources or infer the native allocation format.</summary>
public sealed class NativeCpuSurface
{
    public int Width { get; }
    public int Height { get; }
    public int Pitch { get; }
    public NativeSurfaceFormat Format { get; }
    public int BytesPerPixel => NativeSurfaceCopy.BytesPerPixel(Format);
    public byte[] Pixels { get; }

    public NativeCpuSurface(int width,int height,NativeSurfaceFormat format,int pitch,byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        int bpp=NativeSurfaceCopy.BytesPerPixel(format);
        if(width<=0||height<=0||pitch<(long)width*bpp||
            (long)pitch*height>pixels.Length)
            throw new ArgumentException("Invalid surface dimensions/pitch/storage.");
        Width=width;Height=height;Format=format;Pitch=pitch;Pixels=pixels;
    }

    internal Span<byte> Pixel(int x,int y)=>Pixels.AsSpan(y*Pitch+x*BytesPerPixel,BytesPerPixel);
}

/// <summary>Opaque copy/scaled copy subset of 004AD9C0 and 00547180.
/// No color key, alpha blend, mask +98, negative pitch, or overlapping
/// source/destination memory. Use only on the supported format pairs.
/// All rectangles use logical half-open edges.</summary>
public static class NativeSurfaceCopy
{
    public static int BytesPerPixel(NativeSurfaceFormat format)=>format switch
    {
        NativeSurfaceFormat.Rgb565 or NativeSurfaceFormat.Rgb555=>2,
        NativeSurfaceFormat.Bgr24=>3,
        NativeSurfaceFormat.Bgrx32=>4,
        _=>throw new ArgumentOutOfRangeException(nameof(format))
    };

    /// <summary>004C7600/004C7740/004C7640/004C7780 and
    /// 004B50C0/004B5500/004B5FE0/004B89F0/004BFD00/004C0130.
    /// Expanding low-bit colors uses zero fill, not bit replication.
    /// Same-format copies preserve all storage bits; BGR to 32-bit
    /// clears the fourth byte. These are not alpha conversions.</summary>
    public static void CopyPixel(ReadOnlySpan<byte> source,NativeSurfaceFormat sourceFormat,
        Span<byte> destination,NativeSurfaceFormat destinationFormat)
    {
        int sb=BytesPerPixel(sourceFormat),db=BytesPerPixel(destinationFormat);
        if(source.Length<sb||destination.Length<db)
            throw new ArgumentException("Incomplete pixel.");
        if(sourceFormat==destinationFormat)
        {
            source[..sb].CopyTo(destination);
            return;
        }
        int r,g,b;
        if(sb==2)
        {
            int v=BinaryPrimitives.ReadUInt16LittleEndian(source);
            b=(v&31)<<3;
            if(sourceFormat==NativeSurfaceFormat.Rgb565)
            {
                r=((v>>11)&31)<<3;g=((v>>5)&63)<<2;
            }
            else
            {
                r=((v>>10)&31)<<3;g=((v>>5)&31)<<3;
            }
        }
        else {b=source[0];g=source[1];r=source[2];}
        if(db==2)
        {
            ushort v=destinationFormat==NativeSurfaceFormat.Rgb565
                ? (ushort)(((r>>3)<<11)|((g>>2)<<5)|(b>>3))
                : (ushort)(((r>>3)<<10)|((g>>3)<<5)|(b>>3));
            BinaryPrimitives.WriteUInt16LittleEndian(destination,v);
        }
        else
        {
            destination[0]=(byte)b;destination[1]=(byte)g;destination[2]=(byte)r;
            if(db==4)destination[3]=0;
        }
    }

    /// <summary>00547180 initializes source cursor, quotient, remainder
    /// and signed error for each axis. 004AD9C0 adds remainder BEFORE
    /// copying the pixel. The equal-size fast path is distinct: when
    /// either dimension is resized, both axes use the recurrence.</summary>
    public static int[] AxisSamples(int sourceLength,int destinationLength,
        int skippedDestination,int visibleLength,bool unscaledFastPath)
    {
        if(sourceLength<=0||destinationLength<=0||skippedDestination<0||
            visibleLength<0||(long)skippedDestination+visibleLength>destinationLength)
            throw new ArgumentOutOfRangeException(nameof(destinationLength));
        if(unscaledFastPath&&sourceLength!=destinationLength)
            throw new ArgumentException("Unscaled axis requires equal extents.");
        int[] result=new int[visibleLength];
        if(unscaledFastPath)
        {
            for(int i=0;i<result.Length;i++)result[i]=skippedDestination+i;
            return result;
        }

        long error=-(long)destinationLength+(long)skippedDestination*sourceLength;
        long cursor=0;
        if(error>0)
        {
            cursor=error/destinationLength+1;
            error-=cursor*destinationLength;
        }
        error--;
        int quotient=sourceLength/destinationLength,remainder=sourceLength%destinationLength;
        for(int i=0;i<result.Length;i++)
        {
            error+=remainder;
            if(error>=0){cursor++;error-=destinationLength;}
            if(cursor<0||cursor>=sourceLength)
                throw new InvalidDataException("Native axis sample outside source.");
            result[i]=(int)cursor;
            cursor+=quotient;
        }
        return result;
    }

    /// <summary>Copy valid source subrect to destination, clipped against
    /// surface bounds and optional caller clip. Zero destination extent
    /// is a no-op. Returns number of written pixels, not a native status.</summary>
    public static int Blit(NativeCpuSurface source,CardBounds sourceRect,
        NativeCpuSurface destination,CardBounds destinationRect,CardBounds? clip=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if(ReferenceEquals(source.Pixels,destination.Pixels))
            throw new ArgumentException("Overlapping surface storage is unsupported.");
        if(sourceRect.X<0||sourceRect.Y<0||sourceRect.Width<=0||sourceRect.Height<=0||
            (long)sourceRect.X+sourceRect.Width>source.Width||
            (long)sourceRect.Y+sourceRect.Height>source.Height)
            throw new ArgumentException("Source rectangle outside surface.");
        if(destinationRect.Width<0||destinationRect.Height<0)
            throw new ArgumentException("Negative destination extent unsupported.");
        if(destinationRect.Width==0||destinationRect.Height==0)return 0;
        var c=clip??new CardBounds(0,0,destination.Width,destination.Height);
        if(c.Width<0||c.Height<0)throw new ArgumentException("Invalid clip rectangle.");
        long left=Math.Max(0L,Math.Max(c.X,destinationRect.X));
        long top=Math.Max(0L,Math.Max(c.Y,destinationRect.Y));
        long right=Math.Min(destination.Width,Math.Min((long)c.X+c.Width,(long)destinationRect.X+destinationRect.Width));
        long bottom=Math.Min(destination.Height,Math.Min((long)c.Y+c.Height,(long)destinationRect.Y+destinationRect.Height));
        if(right<=left||bottom<=top)return 0;
        int width=(int)(right-left),height=(int)(bottom-top);
        bool same=sourceRect.Width==destinationRect.Width&&sourceRect.Height==destinationRect.Height;
        var xs=AxisSamples(sourceRect.Width,destinationRect.Width,(int)(left-destinationRect.X),width,same);
        var ys=AxisSamples(sourceRect.Height,destinationRect.Height,(int)(top-destinationRect.Y),height,same);
        for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
                CopyPixel(source.Pixel(sourceRect.X+xs[x],sourceRect.Y+ys[y]),source.Format,
                    destination.Pixel((int)left+x,(int)top+y),destination.Format);
        return checked(width*height);
    }
}
