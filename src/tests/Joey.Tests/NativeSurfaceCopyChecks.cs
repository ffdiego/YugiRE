using Joey.LookAndFeel;
using System.Buffers.Binary;

internal static class NativeSurfaceCopyChecks
{
    public static void Run(Action<bool,string> check)
    {
        // 004C7600 / 004C7740: conversion drops low green bit,
        // and expanding to 565 does not replicate it.
        byte[] source=[0xff,0xff],result=new byte[4];
        NativeSurfaceCopy.CopyPixel(source,NativeSurfaceFormat.Rgb565,result,NativeSurfaceFormat.Rgb555);
        check(BinaryPrimitives.ReadUInt16LittleEndian(result)==0x7fff,"Surface: 565 to 555");
        NativeSurfaceCopy.CopyPixel(result,NativeSurfaceFormat.Rgb555,source,NativeSurfaceFormat.Rgb565);
        check(BinaryPrimitives.ReadUInt16LittleEndian(source)==0xffdf,"Surface: 555 to 565 zero fill");
        NativeSurfaceCopy.CopyPixel(new byte[]{0xff,0xff},NativeSurfaceFormat.Rgb565,result,NativeSurfaceFormat.Bgrx32);
        check(result.SequenceEqual(new byte[]{248,252,248,0}),"Surface: native 565 expansion");
        NativeSurfaceCopy.CopyPixel(new byte[]{0xff,0xff},NativeSurfaceFormat.Rgb555,result,NativeSurfaceFormat.Bgrx32);
        check(result.SequenceEqual(new byte[]{248,248,248,0}),"Surface: native 555 expansion");
        NativeSurfaceCopy.CopyPixel(new byte[]{1,2,3,222},NativeSurfaceFormat.Bgrx32,result,NativeSurfaceFormat.Bgrx32);
        check(result.SequenceEqual(new byte[]{1,2,3,222}),"Surface: raw 32-bit copy preserves fourth byte");
        NativeSurfaceCopy.CopyPixel(new byte[]{1,2,3},NativeSurfaceFormat.Bgr24,result,NativeSurfaceFormat.Bgrx32);
        check(result.SequenceEqual(new byte[]{1,2,3,0}),"Surface: 24 to 32 clears fourth byte");
        NativeSurfaceCopy.CopyPixel(new byte[]{0x9f,0x81,0x42},NativeSurfaceFormat.Bgr24,result,NativeSurfaceFormat.Rgb565);
        check(BinaryPrimitives.ReadUInt16LittleEndian(result)==0x4413,"Surface: BGR24 quantization");
        check(NativeSurfaceCopy.AxisSamples(5,3,0,3,false).SequenceEqual(new[]{0,2,3})&&
            NativeSurfaceCopy.AxisSamples(3,5,0,5,false).SequenceEqual(new[]{0,1,1,2,2}),
            "Surface: pre-increment remainder, differs from floor nearest");
        check(NativeSurfaceCopy.AxisSamples(5,3,1,2,false).SequenceEqual(new[]{2,3})&&
            NativeSurfaceCopy.AxisSamples(5,5,1,4,true).SequenceEqual(new[]{1,2,3,4}),
            "Surface: clipped origin and direct fast path");
        // 00547180 uses the scaled branch for BOTH axes if either extent
        // differs; don't replace one axis with the fast-copy path.
        check(NativeSurfaceCopy.AxisSamples(5,5,1,4,false).SequenceEqual(new[]{0,1,2,3}),
            "Surface: preserve recovered mixed-scale clipped recurrence");

        for(int s=1;s<=32;s++)
        for(int d=1;d<=32;d++)
        for(int skip=0;skip<d;skip++)
        {
            var samples=NativeSurfaceCopy.AxisSamples(s,d,skip,d-skip,false);
            check(samples.Length==d-skip&&samples.All(x=>x>=0&&x<s)&&
                samples.SequenceEqual(samples.OrderBy(x=>x)),"Surface: clipped axis valid and monotone");
        }

        byte[] pixels=Enumerable.Repeat((byte)0xee,34).ToArray();
        var from=new NativeCpuSurface(5,2,NativeSurfaceFormat.Bgr24,17,pixels);
        for(int y=0;y<2;y++)
            for(int x=0;x<5;x++)
            {pixels[y*17+x*3]=(byte)(x+10*y);pixels[y*17+x*3+1]=22;pixels[y*17+x*3+2]=33;}
        byte[] output=Enumerable.Repeat((byte)0xaa,24).ToArray();
        var to=new NativeCpuSurface(3,2,NativeSurfaceFormat.Bgr24,12,output);
        check(NativeSurfaceCopy.Blit(from,new(0,0,5,2),to,new(-1,0,3,2))==4&&
              output[0]==2&&output[3]==3&&output[12]==12&&output[15]==13&&
              output[6]==0xaa&&output[9]==0xaa&&output[21]==0xaa,
            "Surface: clipped scaled blit keeps stride padding and untouched pixels");
        Array.Fill(output,(byte)0xaa);
        check(NativeSurfaceCopy.Blit(from,new(1,0,3,2),to,new(0,0,3,2),new(1,1,1,1))==1&&
              output[15]==12&&output[16]==22&&output[17]==33&&output[0]==0xaa,
            "Surface: unscaled subrect and destination clip");
        check(NativeSurfaceCopy.Blit(from,new(0,0,5,2),to,new(0,0,0,2))==0&&
              NativeSurfaceCopy.Blit(from,new(0,0,5,2),to,new(10,10,5,2))==0,
            "Surface: zero extent and offscreen no-op");
    }
}
