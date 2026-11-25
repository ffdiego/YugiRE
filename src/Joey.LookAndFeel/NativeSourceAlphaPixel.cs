using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Same-format four-byte kernels for native format codes 12 and 13.
/// These two codes use the same per-pixel arithmetic in 00438100/00438E30
/// and the same copy kernel 00530220. The distinction between their
/// allocation/load paths is outside this contract. No cross-format claims.
/// Unlike a general alpha-compositing API, the fourth destination byte is
/// forced to 255 for a blended store and copied in the base path.</summary>
public static class NativeSourceAlphaPixel
{
    public static byte EffectiveIntensity(byte sourceAlpha,byte globalIntensity) =>
        NativeIntensityPixel.Product(globalIntensity,sourceAlpha);

    /// <summary>Pixel operation only. Accept nonnegative native intensity;
    /// >=256 selects 0051EBD0 -> 00530220 (keyed copy of all four bytes).
    /// Return false only for a key match, which ignores the alpha byte.
    /// A non-key blend at zero intensity still writes destination alpha 255.
    /// Do not assume this dispatch applies to every YGA or mask resource.</summary>
    public static bool Apply(ReadOnlySpan<byte> source,Span<byte> destination,
        int nativeFormat,uint colorKey,int intensity)
    {
        if(nativeFormat is not (12 or 13))
            throw new ArgumentOutOfRangeException(nameof(nativeFormat));
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);
        if(source.Length<4||destination.Length<4)
            throw new ArgumentException("Incomplete four-byte pixel.");
        uint pixel=BinaryPrimitives.ReadUInt32LittleEndian(source);
        if(((pixel^colorKey)&0xffffffu)==0)
            return false;
        if(intensity>=256)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination,pixel);
            return true;
        }
        byte effective=EffectiveIntensity((byte)(pixel>>24),(byte)intensity);
        byte b=(byte)pixel,g=(byte)(pixel>>8),r=(byte)(pixel>>16);
        destination[0]=NativeIntensityPixel.BlendChannel(b,destination[0],effective);
        destination[1]=NativeIntensityPixel.BlendChannel(g,destination[1],effective);
        destination[2]=NativeIntensityPixel.BlendChannel(r,destination[2],effective);
        destination[3]=255;
        return true;
    }
}
