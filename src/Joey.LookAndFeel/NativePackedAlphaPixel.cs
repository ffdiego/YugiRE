using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Pixel subset of 004365C0/00437360, 0052F570 and
/// 00439B60/00424FC0. Alpha nibble occupies byte 2 of a four-byte
/// source. Same-format and opaque-destination paths are deliberately
/// different; not a general surface or polygon compositor.</summary>
public static class NativePackedAlphaPixel
{
    public static byte EffectiveIntensity(byte alphaNibble,byte intensity)
    {
        if(alphaNibble>15)throw new ArgumentOutOfRangeException(nameof(alphaNibble));
        return NativeIntensityPixel.Product(intensity,(byte)(alphaNibble*16));
    }

    private static NativeSurfaceFormat PackedFormat(int format) => format switch
    {
        10=>NativeSurfaceFormat.Rgb565,
        11=>NativeSurfaceFormat.Rgb555,
        _=>throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static int BlendExpanded(int source,int destination,int shift,byte effective)
    {
        int s=source<<shift,d=destination<<shift;
        return (NativeIntensityPixel.Product(effective,(byte)s)+
                NativeIntensityPixel.InverseProduct(effective,(byte)d))>>shift;
    }

    /// <summary>Same-format kernels: expand channels with zero fill,
    /// sum separately truncated products, then repack. Unlike the
    /// opaque intensity path, there is no equal-color skip.
    /// Blended store writes only bytes 0/1. Base copy writes 0..2.</summary>
    public static bool ApplySameFormat(ReadOnlySpan<byte> source,Span<byte> destination,
        int format,ushort colorKey,int intensity)
    {
        var packed=PackedFormat(format);
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);
        if(source.Length<4||destination.Length<4)
            throw new ArgumentException("Incomplete packed alpha pixel.");
        ushort s=BinaryPrimitives.ReadUInt16LittleEndian(source);
        if(s==colorKey)return false;
        byte alpha=source[2];
        if(alpha>15)throw new ArgumentOutOfRangeException(nameof(source),"Expected alpha nibble.");
        if(intensity>=256)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination,s);
            destination[2]=alpha;
            return true;
        }
        ushort d=BinaryPrimitives.ReadUInt16LittleEndian(destination);
        byte e=EffectiveIntensity(alpha,(byte)intensity);
        int greenBits=packed==NativeSurfaceFormat.Rgb565?6:5;
        int redShift=greenBits+5,greenMask=(1<<greenBits)-1;
        int b=BlendExpanded(s&31,d&31,3,e);
        int g=BlendExpanded((s>>5)&greenMask,(d>>5)&greenMask,8-greenBits,e);
        int r=BlendExpanded((s>>redShift)&31,(d>>redShift)&31,3,e);
        BinaryPrimitives.WriteUInt16LittleEndian(destination,(ushort)((r<<redShift)|(g<<5)|b));
        return true;
    }

    /// <summary>10->3 or 11->4, intensity 0..255 only.
    /// Cross-format helper quantizes the effective byte again, then
    /// adds the two packed lookup results. Base cross-format dispatch
    /// is outside this adapter; do not substitute same-format copy.</summary>
    public static bool ApplyToOpaque(ReadOnlySpan<byte> source,Span<byte> destination,
        int sourceFormat,NativeSurfaceFormat destinationFormat,ushort colorKey,int intensity)
    {
        if(PackedFormat(sourceFormat)!=destinationFormat)
            throw new ArgumentException("Unsupported packed alpha/opaque format pair.");
        if(intensity is <0 or >255)
            throw new ArgumentOutOfRangeException(nameof(intensity));
        if(source.Length<4||destination.Length<2)
            throw new ArgumentException("Incomplete packed pixel.");
        ushort s=BinaryPrimitives.ReadUInt16LittleEndian(source);
        if(s==colorKey)return false;
        ushort d=BinaryPrimitives.ReadUInt16LittleEndian(destination);
        int q=EffectiveIntensity(source[2],(byte)intensity)>>4;
        ushort result=(ushort)(NativeIntensityPixel.ScalePacked(s,destinationFormat,q)+
                               NativeIntensityPixel.ScalePacked(d,destinationFormat,15-q));
        BinaryPrimitives.WriteUInt16LittleEndian(destination,result);
        return true;
    }
}
