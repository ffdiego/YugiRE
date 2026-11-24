using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Same-format intensity pixel kernels recovered from 00546D70
/// and 00431750/00432450/00433150/00434B40 decompilations.
/// Not a surface dispatcher, polygon rasterizer, or YGA alpha compositor.
/// The raw 256+ case belongs to 0051EBD0, not these kernels.</summary>
public static class NativeIntensityPixel
{
    // 00546D70 generates product and inverse-product tables (divisor 255).
    public static byte Product(byte level, byte channel) =>
        (byte)(level*channel/255);

    public static byte InverseProduct(byte level, byte channel) =>
        (byte)(channel*(255-level)/255);

    // 0060EF03 + 512*level + (source-dest) uses the negative
    // of the positive product for negative deltas. Truncate toward zero.
    public static byte BlendChannel(byte source,byte destination,byte level) =>
        (byte)(destination+(source-destination)*level/255);

    // 0062EE04 and 0082EE04 use channel values in their packed domain,
    // independently truncated /15. RGB555 duplicates the low 15-bit
    // table across bit 15. Do not round or expand to RGB888.
    public static ushort ScalePacked(ushort pixel,NativeSurfaceFormat format,int level4)
    {
        if(level4 is <0 or >15)
            throw new ArgumentOutOfRangeException(nameof(level4));
        int greenBits=format switch
        {
            NativeSurfaceFormat.Rgb565=>6,
            NativeSurfaceFormat.Rgb555=>5,
            _=>throw new ArgumentOutOfRangeException(nameof(format))
        };
        int redShift=5+greenBits,greenMask=(1<<greenBits)-1;
        int b=(pixel&31)*level4/15;
        int g=((pixel>>5)&greenMask)*level4/15;
        int r=((pixel>>redShift)&31)*level4/15;
        return (ushort)((r<<redShift)|(g<<5)|b);
    }

    /// <summary>Apply only the supported same-format per-pixel kernel.
    /// False means the original skips a store (key match, or equal
    /// packed 16-bit pixels); true means it executes a store, even
    /// when RGB stays identical. BGRX32 clears X on a non-key store.</summary>
    public static bool Apply(ReadOnlySpan<byte> source,Span<byte> destination,
        NativeSurfaceFormat format,uint colorKey,int intensity)
    {
        if(intensity is <0 or >255)
            throw new ArgumentOutOfRangeException(nameof(intensity),
                "The >=256 path is a different native dispatch.");
        int size=NativeSurfaceCopy.BytesPerPixel(format);
        if(source.Length<size||destination.Length<size)
            throw new ArgumentException("Incomplete pixel.");
        if(size==2)
        {
            ushort s=BinaryPrimitives.ReadUInt16LittleEndian(source);
            ushort d=BinaryPrimitives.ReadUInt16LittleEndian(destination);
            if(s==(ushort)colorKey||s==d)return false;
            int q=intensity>>4;
            ushort blended=unchecked((ushort)(ScalePacked(s,format,q)+ScalePacked(d,format,15-q)));
            BinaryPrimitives.WriteUInt16LittleEndian(destination,blended);
            return true;
        }

        uint value=size==3
            ? (uint)(source[0]|(source[1]<<8)|(source[2]<<16))
            : BinaryPrimitives.ReadUInt32LittleEndian(source);
        if(size==3?(value==(colorKey&0xffffffu)):(value==colorKey))
            return false;
        // Load RGB before writing, including the adapter's overlapping-span
        // case. Native surfaces are expected to be separately locked.
        byte b=source[0],g=source[1],r=source[2];
        destination[0]=BlendChannel(b,destination[0],(byte)intensity);
        destination[1]=BlendChannel(g,destination[1],(byte)intensity);
        destination[2]=BlendChannel(r,destination[2],(byte)intensity);
        if(size==4)destination[3]=0;
        return true;
    }
}
