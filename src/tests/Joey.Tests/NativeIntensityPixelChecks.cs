using Joey.LookAndFeel;
using System.Buffers.Binary;

internal static class NativeIntensityPixelChecks
{
    internal static void Run(Action<bool,string> check)
    {
        // Reconstruct the two byte-product tables independently by the
        // additive recurrences from 00546D70, rather than reuse formulas.
        bool tables=true;
        for(int level=0;level<256;level++)
        {
            int product=0,inverse=level*255;
            // initializer's inverse row for channel c is level*(255-c)
            // as the table is indexed channel*256 + level.
            for(int channel=0;channel<256;channel++)
            {
                tables &= NativeIntensityPixel.Product((byte)level,(byte)channel)==product/255;
                tables &= NativeIntensityPixel.InverseProduct((byte)channel,(byte)level)==inverse/255;
                product+=level;inverse-=level;
            }
        }
        check(tables,"V17: all 65,536 entries of each byte product table");
        check(NativeIntensityPixel.BlendChannel(255,0,128)==128 &&
            NativeIntensityPixel.BlendChannel(0,255,128)==127 &&
            NativeIntensityPixel.BlendChannel(0,1,128)==1 &&
            NativeIntensityPixel.BlendChannel(1,0,128)==0 &&
            NativeIntensityPixel.BlendChannel(1,0,255)==1,
            "V17: signed delta truncates toward zero, not weighted channel floors");
        bool scales=true;
        for(int q=0;q<16;q++)
        for(int v=0;v<65536;v++)
        {
            int b=(v&31)*q/15,g=((v>>5)&63)*q/15,r=((v>>11)&31)*q/15;
            scales &= NativeIntensityPixel.ScalePacked((ushort)v,NativeSurfaceFormat.Rgb565,q)==(ushort)(r*2048+g*32+b);
            g=((v>>5)&31)*q/15;r=((v>>10)&31)*q/15;
            scales &= NativeIntensityPixel.ScalePacked((ushort)v,NativeSurfaceFormat.Rgb555,q)==(ushort)(r*1024+g*32+b);
        }
        check(scales,"V17: all packed scaling entries in both 16-bit tables");

        byte[] dst=[0,255,1,77,99];
        bool written=NativeIntensityPixel.Apply([255,0,0],dst,NativeSurfaceFormat.Bgr24,0x0000ff00,128);
        check(written&&dst.SequenceEqual(new byte[]{128,127,1,77,99}),
            "V17: BGR24 channels, no adjacent writes");
        dst=[0,255,1,77];
        check(!NativeIntensityPixel.Apply([0,255,0],dst,NativeSurfaceFormat.Bgr24,0xab00ff00,255)&&
            dst.SequenceEqual(new byte[]{0,255,1,77}),"V17: BGR24 key masks high byte");
        dst=[4,5,6,77];
        check(NativeIntensityPixel.Apply([10,20,30,9],dst,NativeSurfaceFormat.Bgrx32,0x001e140a,0)&&
            dst.SequenceEqual(new byte[]{4,5,6,0}),"V17: BGRX32 compares all bits and clears X even at intensity zero");
        dst=[4,5,6,77];
        check(!NativeIntensityPixel.Apply([10,20,30,9],dst,NativeSurfaceFormat.Bgrx32,0x091e140a,128)&&
            dst[3]==77,"V17: BGRX32 full dword key skips store");
        dst=[0,0];
        check(NativeIntensityPixel.Apply([255,255],dst,NativeSurfaceFormat.Rgb565,0x07e0,128)&&
            BinaryPrimitives.ReadUInt16LittleEndian(dst)==0x8430,"V17: RGB565 q=8");
        dst=[0,0];
        check(NativeIntensityPixel.Apply([255,255],dst,NativeSurfaceFormat.Rgb555,0x03e0,143)&&
            BinaryPrimitives.ReadUInt16LittleEndian(dst)==0x4210,"V17: RGB555 q=8, drops spare bit");
        dst=[2,0];
        check(NativeIntensityPixel.Apply([1,0],dst,NativeSurfaceFormat.Rgb565,0xffff,128)&&
            BinaryPrimitives.ReadUInt16LittleEndian(dst)==0,"V17: two independent packed truncations");
        dst=[255,255];
        check(!NativeIntensityPixel.Apply([255,255],dst,NativeSurfaceFormat.Rgb555,0,128)&&
            dst.SequenceEqual(new byte[]{255,255}),"V17: identical packed pixel skips, preserves storage bit");
        bool rejected=false;
        try { NativeIntensityPixel.Apply([1,2,3],new byte[3],NativeSurfaceFormat.Bgr24,0,256); }
        catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"V17: 256 is separate raster dispatch");
    }
}
