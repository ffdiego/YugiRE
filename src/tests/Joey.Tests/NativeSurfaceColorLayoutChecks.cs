using Joey.LookAndFeel;

internal static class NativeSurfaceColorLayoutChecks
{
    internal static void Run(Action<bool,string> check)
    {
        int?[] formats = [null, null, 11, 10, 11, 12, 13, 12, 13, null, null];
        check(Enumerable.Range(0, formats.Length).Select(NativeSurfaceColorLayout.SelectSourceAlphaFormat)
            .SequenceEqual(formats), "V17: native software source-alpha allocation across device formats");

        check(NativeSurfaceColorLayout.ClassifyDirectDraw(8,0,0,0)==2 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(16,0xf800,0x7e0,0x1f)==3 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(16,0x7c00,0x3e0,0x1f)==4 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(24,0xff0000,0xff00,0xff)==5 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(24,0xff,0xff00,0xff0000)==6 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(32,0xff0000,0xff00,0xff)==7 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(32,0xff,0xff00,0xff0000)==8 &&
            NativeSurfaceColorLayout.ClassifyDirectDraw(32,0xff0000,0xff,0xff00)==1,
            "V17: native DirectDraw RGB mask classifier, not alpha format");
        // COLORREF = R=E1, G=96, B=25, with an irrelevant high byte.
        uint key=0x7f2596e1;
        check(NativeSurfaceColorLayout.ColorKey(key,2)==key &&
            NativeSurfaceColorLayout.ColorKey(key,3)==0xe4a4 &&
            NativeSurfaceColorLayout.ColorKey(key,10)==0xe4a4 &&
            NativeSurfaceColorLayout.ColorKey(key,4)==0x7244 &&
            NativeSurfaceColorLayout.ColorKey(key,11)==0x7244 &&
            NativeSurfaceColorLayout.ColorKey(key,5)==0xe19625 &&
            NativeSurfaceColorLayout.ColorKey(key,7)==0xe19625 &&
            NativeSurfaceColorLayout.ColorKey(key,12)==0xe19625 &&
            NativeSurfaceColorLayout.ColorKey(key,6)==0x2596e1 &&
            NativeSurfaceColorLayout.ColorKey(key,8)==0x2596e1 &&
            NativeSurfaceColorLayout.ColorKey(key,13)==0x2596e1 &&
            NativeSurfaceColorLayout.ColorKey(key,9)==uint.MaxValue &&
            NativeSurfaceColorLayout.ColorKey(uint.MaxValue,12)==uint.MaxValue,
            "V17: COLORREF to packed/BGR/RGB key and sentinel");
        byte[] source=[0x25,0x96,0xe1,0xa7];
        byte[] dst=[0,0,0,0x77,0x99];
        NativeSurfaceColorLayout.UploadAlphaPixel(source,dst,10);
        check(dst.SequenceEqual(new byte[]{0xa4,0xe4,0x0a,0x77,0x99}),
            "V17: RGB565 plus alpha nibble, preserve fourth byte and neighbor");
        NativeSurfaceColorLayout.UploadAlphaPixel(source,dst,11);
        check(dst.SequenceEqual(new byte[]{0x44,0x72,0x0a,0x77,0x99}),
            "V17: RGB555 plus alpha nibble");
        NativeSurfaceColorLayout.UploadAlphaPixel(source,dst,12);
        check(dst.SequenceEqual(new byte[]{0x25,0x96,0xe1,0xa7,0x99}),
            "V17: format 12 BGRA byte order");
        NativeSurfaceColorLayout.UploadAlphaPixel(source,dst,13);
        check(dst.SequenceEqual(new byte[]{0xe1,0x96,0x25,0xa7,0x99}),
            "V17: format 13 RGBA byte order");
        bool alphas=true;
        foreach(int format in new[]{10,11,12,13})
        for(int alpha=0;alpha<256;alpha++)
        {
            source[3]=(byte)alpha;
            dst[3]=0x77;
            NativeSurfaceColorLayout.UploadAlphaPixel(source,dst,format);
            alphas &= format<=11 ? dst[2]==alpha/16&&dst[3]==0x77 : dst[3]==alpha;
        }
        check(alphas,"V17: all 256 source alphas per upload format");
        // Connect uploaded storage and key conversion to same-format
        // alpha kernel. Green/magenta keys alone cannot detect R/B swaps.
        foreach(int format in new[]{12,13})
        {
            byte[] s=new byte[4],d=[1,2,3,4];
            NativeSurfaceColorLayout.UploadAlphaPixel(source,s,format);
            check(!NativeSourceAlphaPixel.Apply(s,d,format,
                NativeSurfaceColorLayout.ColorKey(key,format),128) &&
                d.SequenceEqual(new byte[]{1,2,3,4}),
                $"V17: converted asymmetric key matches uploaded format {format}");
        }
    }
}
