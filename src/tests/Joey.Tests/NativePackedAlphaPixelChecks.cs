using Joey.LookAndFeel;
using System.Buffers.Binary;

internal static class NativePackedAlphaPixelChecks
{
    internal static void Run(Action<bool,string> check)
    {
        bool table=true;
        for(int a=0;a<16;a++)
        for(int i=0;i<256;i++)
            table &= NativePackedAlphaPixel.EffectiveIntensity((byte)a,(byte)i)==a*16*i/255;
        check(table&&NativePackedAlphaPixel.EffectiveIntensity(15,255)==240,
            "V17: all 4096 packed-alpha/global combinations, max 240");
        byte[] s=[0,0xf8,15,0x99],d=[0xff,7,5,0x77,0x55];
        check(NativePackedAlphaPixel.ApplySameFormat(s,d,10,0,255)&&
            d.SequenceEqual(new byte[]{0x61,0xe8,5,0x77,0x55}),
            "V17: 565 expanded products, preserve alpha/storage/neighbor");
        byte[] opaque=[0xff,7,0x55];
        check(NativePackedAlphaPixel.ApplyToOpaque(s,opaque,10,NativeSurfaceFormat.Rgb565,0,255)&&
            opaque.SequenceEqual(new byte[]{0,0xf8,0x55}),
            "V17: 10->3 uses q=15 and yields full source color");
        s=[0,0x7c,15,0x99];d=[0xff,3,5,0x77];
        check(NativePackedAlphaPixel.ApplySameFormat(s,d,11,0,255)&&
            d.SequenceEqual(new byte[]{0x21,0x74,5,0x77}),
            "V17: 555 expanded products");
        opaque=[0xff,3];
        check(NativePackedAlphaPixel.ApplyToOpaque(s,opaque,11,NativeSurfaceFormat.Rgb555,0,255)&&
            opaque.SequenceEqual(new byte[]{0,0x7c}),"V17: 11->4 full packed source");
        s=[0,0xf8,8,0x99];d=[0xff,7,5,0x77];
        opaque=[0xff,7];
        check(NativePackedAlphaPixel.ApplySameFormat(s,d,10,0,128)&&
            BinaryPrimitives.ReadUInt16LittleEndian(d)==0x3df7&&
            NativePackedAlphaPixel.ApplyToOpaque(s,opaque,10,NativeSurfaceFormat.Rgb565,0,128)&&
            BinaryPrimitives.ReadUInt16LittleEndian(opaque)==0x45d6,
            "V17: same-format and opaque destination have distinct truncation");
        foreach(int format in new[]{10,11})
        {
            s=[0x34,0x12,0,0x99];d=[1,2,5,0x77];
            check(!NativePackedAlphaPixel.ApplySameFormat(s,d,format,0x1234,255)&&
                !NativePackedAlphaPixel.ApplySameFormat(s,d,format,0x1234,256)&&
                d.SequenceEqual(new byte[]{1,2,5,0x77}),
                $"V17: format {format} packed key skips regardless of alpha");
            check(NativePackedAlphaPixel.ApplySameFormat(s,d,format,0,256)&&
                d.SequenceEqual(new byte[]{0x34,0x12,0,0x77}),
                $"V17: format {format} base copy writes three bytes, even zero alpha");
            s=[0x34,0x12,8,0x99];d=[0x34,0x12,5,0x77];
            check(NativePackedAlphaPixel.ApplySameFormat(s,d,format,0,128)&&
                BinaryPrimitives.ReadUInt16LittleEndian(d)!=0x1234&&d[2]==5&&d[3]==0x77,
                $"V17: format {format} identical color is not skipped");
        }
        bool rejected=false;
        try { NativePackedAlphaPixel.ApplyToOpaque(new byte[]{1,0,15,0},new byte[2],10,NativeSurfaceFormat.Rgb565,0,256); }
        catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"V17: intensity compositor rejects base dispatch");
        opaque = [0xff, 0xff, 0x55];
        NativePackedAlphaPixel.ApplyBaseToRgb565([0, 0, 8, 0x99], opaque);
        check(opaque.SequenceEqual(new byte[] {0xae, 0x73, 0x55}),
            "V17: base 10->3 blends black at nibble 8 and preserves neighbor bytes");
        NativePackedAlphaPixel.ApplyBaseToRgb565([0, 0xf8, 0, 0x99], opaque);
        check(opaque.SequenceEqual(new byte[] {0xae, 0x73, 0x55}),
            "V17: base 10->3 keeps destination color at nibble zero");
        NativePackedAlphaPixel.ApplyBaseToRgb565([0, 0xf8, 15, 0x99], opaque);
        check(opaque.SequenceEqual(new byte[] {0, 0xf8, 0x55}),
            "V17: base 10->3 takes the source at nibble 15");
        rejected=false;
        try { NativePackedAlphaPixel.EffectiveIntensity(16,255); }
        catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"V17: invalid alpha byte rejected instead of indexing outside table");
    }
}
