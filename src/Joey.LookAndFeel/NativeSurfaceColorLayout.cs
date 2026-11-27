using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Native surface format codes from 004A6120, 004AA8D0 and
/// 004AB460. The device classifier covers only opaque DirectDraw
/// surfaces; software alpha allocation is a separate path.
/// No inference from the .yga filename or active GPU pixel format.</summary>
public static class NativeSurfaceColorLayout
{
    /// <summary>The software alpha branch of 004ABC10 indexes 004ABDAC
    /// by the current device format minus two. A null result means
    /// that the branch skips software alpha allocation.</summary>
    public static int? SelectSourceAlphaFormat(int deviceFormat) => deviceFormat switch
    {
        3 => 10,
        2 or 4 => 11,
        5 or 7 => 12,
        6 or 8 => 13,
        _ => null
    };

    public static int ClassifyDirectDraw(int bits,uint redMask,uint greenMask,uint blueMask)
    {
        if(bits==8)return 2;
        if(bits==16)
        {
            if((redMask,greenMask,blueMask)==(0xf800u,0x7e0u,0x1fu))return 3;
            if((redMask,greenMask,blueMask)==(0x7c00u,0x3e0u,0x1fu))return 4;
        }
        if(bits is 24 or 32)
        {
            if((redMask,greenMask,blueMask)==(0xff0000u,0xff00u,0xffu))
                return bits==24?5:7;
            if((redMask,greenMask,blueMask)==(0xffu,0xff00u,0xff0000u))
                return bits==24?6:8;
        }
        return 1; // 004A6120 unknown-layout result, not an alpha flag.
    }

    /// <summary>004AA8D0 takes a Windows COLORREF (00BBGGRR),
    /// returning the raw color key in the surface's storage order.
    /// Unknown format and input FFFFFFFF both return native sentinel.</summary>
    public static uint ColorKey(uint colorRef,int nativeFormat)
    {
        if(colorRef==uint.MaxValue)return uint.MaxValue;
        uint r=colorRef&255,g=(colorRef>>8)&255,b=(colorRef>>16)&255;
        return nativeFormat switch
        {
            2=>colorRef,
            3 or 10=>(r>>3)<<11|(g>>2)<<5|(b>>3),
            4 or 11=>(r>>3)<<10|(g>>3)<<5|(b>>3),
            5 or 7 or 12=>(r<<16)|(g<<8)|b,
            6 or 8 or 13=>colorRef&0xffffffu,
            _=>uint.MaxValue
        };
    }

    /// <summary>004AB460 dispatches already-decoded BGRA bytes to
    /// 004AB950 (10), 004AD0E0 (11), 004BA020 (12), 004AD580 (13).
    /// All four use a four-byte stride. Formats 10/11 write only
    /// the first three bytes; preserve byte 3 rather than guessing
    /// its initial value. This is an upload conversion, not a blit.</summary>
    public static void UploadAlphaPixel(ReadOnlySpan<byte> decodedBgra,
        Span<byte> destination,int nativeFormat)
    {
        if(nativeFormat is not (10 or 11 or 12 or 13))
            throw new ArgumentOutOfRangeException(nameof(nativeFormat));
        if(decodedBgra.Length<4||destination.Length<4)
            throw new ArgumentException("Incomplete four-byte pixel.");
        byte b=decodedBgra[0],g=decodedBgra[1],r=decodedBgra[2],alpha=decodedBgra[3];
        switch(nativeFormat)
        {
            case 10:
            case 11:
                ushort packed=nativeFormat==10
                    ? (ushort)(((r>>3)<<11)|((g>>2)<<5)|(b>>3))
                    : (ushort)(((r>>3)<<10)|((g>>3)<<5)|(b>>3));
                BinaryPrimitives.WriteUInt16LittleEndian(destination,packed);
                destination[2]=(byte)(alpha>>4);
                break;
            case 12:
                destination[0]=b;destination[1]=g;destination[2]=r;destination[3]=alpha;
                break;
            case 13:
                destination[0]=r;destination[1]=g;destination[2]=b;destination[3]=alpha;
                break;
        }
    }
}
