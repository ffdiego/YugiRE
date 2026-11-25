using Joey.LookAndFeel;

internal static class NativeSourceAlphaPixelChecks
{
    internal static void Run(Action<bool,string> check)
    {
        bool table=true;
        for(int a=0;a<256;a++)
        {
            int product=0;
            for(int i=0;i<256;i++)
            {
                table &= NativeSourceAlphaPixel.EffectiveIntensity((byte)a,(byte)i)==product/255;
                product+=a;
            }
        }
        check(table,"V17: all 65536 source-alpha/global intensity combinations");
        foreach(int format in new[]{12,13})
        {
            byte[] src=[255,0,0,128],dst=[0,255,1,77,99];
            check(NativeSourceAlphaPixel.Apply(src,dst,format,0x0000ff00,128)&&
                dst.SequenceEqual(new byte[]{64,191,1,255,99}),
                $"V17: format {format} two-stage alpha and signed delta; no adjacent write");
            dst=[0,255,1,77];
            check(!NativeSourceAlphaPixel.Apply(src,dst,format,0xab0000ff,128)&&
                dst.SequenceEqual(new byte[]{0,255,1,77}),
                $"V17: format {format} RGB-only key at blend");
            check(!NativeSourceAlphaPixel.Apply(src,dst,format,0xab0000ff,256)&&
                dst.SequenceEqual(new byte[]{0,255,1,77}),
                $"V17: format {format} RGB-only key at copy");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply([255,0,0,0],dst,format,0x0000ff00,255)&&
                dst.SequenceEqual(new byte[]{0,255,1,255}),
                $"V17: format {format} zero source alpha still sets fourth destination byte");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply(src,dst,format,0x0000ff00,0)&&
                dst.SequenceEqual(new byte[]{0,255,1,255}),
                $"V17: format {format} zero global intensity is not a skip");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply(src,dst,format,0x0000ff00,255)&&
                dst.SequenceEqual(new byte[]{128,127,1,255}),
                $"V17: format {format} intensity 255 retains source-alpha modulation");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply(src,dst,format,0x0000ff00,256)&&
                dst.SequenceEqual(src),
                $"V17: format {format} intensity 256 copies original fourth byte");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply([255,0,0,0],dst,format,0x0000ff00,300)&&
                dst.SequenceEqual(new byte[]{255,0,0,0}),
                $"V17: format {format} base copy does not reject zero alpha");
            dst=[0,255,1,77];
            check(NativeSourceAlphaPixel.Apply([255,0,0,255],dst,format,0x0000ff00,255)&&
                dst.SequenceEqual(new byte[]{255,0,0,255}),
                $"V17: format {format} full alpha and global byte");
        }
        bool rejected=false;
        try { NativeSourceAlphaPixel.Apply(new byte[4],new byte[4],7,0,128); }
        catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"V17: do not reinterpret BGRX32 format 7 as source alpha");
        rejected=false;
        try { NativeSourceAlphaPixel.Apply(new byte[4],new byte[4],12,0,-1); }
        catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"V17: negative intensity outside adapter contract");
        rejected=false;
        try { NativeSourceAlphaPixel.Apply(new byte[3],new byte[4],12,0,128); }
        catch(ArgumentException){rejected=true;}
        check(rejected,"V17: incomplete source pixel rejected");
    }
}
