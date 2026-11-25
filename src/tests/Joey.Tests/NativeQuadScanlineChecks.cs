using Joey.LookAndFeel;

internal static class NativeQuadScanlineChecks
{
    private static NativeQuadVertex V(int x,int y,int u,int v)=>new(new(x,y),new(u,v));
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        NativeQuadVertex[] rect=[V(0,0,0,0),V(2,0,2,0),V(2,2,2,2),V(0,2,0,2)];
        var c=new CardBounds(0,0,3,3);
        var samples=NativeQuadScanlines.Samples(rect,c).ToArray();
        check(samples.Length==9 && samples.All(s=>s.X==s.U&&s.Y==s.V),
            "V17: inclusive rectangular endpoints");
        check(samples.SequenceEqual(NativeQuadScanlines.Samples(rect.Reverse().ToArray(),c)),
            "V17: reversed winding, same samples");
        var withFlag=NativeQuadScanlines.Samples(rect,c,true).ToArray();
        check(withFlag.Select(s=>s.U).SequenceEqual(new[]{0,0,1,0,0,1,0,0,1}) &&
            withFlag.Select(s=>s.V).SequenceEqual(new[]{0,0,0,1,1,1,1,1,1}),
            "V17: extra divisor applies only to UV, distinct edge/span rounding");
        NativeQuadVertex[] trap=[V(0,0,0,0),V(4,1,4,0),V(4,3,4,4),V(0,4,0,4)];
        var t=NativeQuadScanlines.Samples(trap,new(0,0,5,5)).ToArray();
        check(t.GroupBy(s=>s.Y).Select(g=>g.Count()).SequenceEqual(new[]{1,5,5,5,1}),
            "V17: trapezoid inclusive rows and edge change at vertex");
        check(t.Single(s=>s.X==4&&s.Y==1).V==0&&
            t.Single(s=>s.X==4&&s.Y==3).V==4,"V17: right edge reinitializes UV at intermediate vertices");
        NativeQuadVertex[] slant=[V(0,0,0,0),V(4,0,4,0),V(5,4,4,4),V(1,4,0,4)];
        var clipped=NativeQuadScanlines.Samples(slant,new(1,1,3,2)).ToArray();
        check(clipped[0]==new NativeQuadSample(1,1,0,1)&&
            clipped.All(s=>s.X>=1&&s.X<4&&s.Y>=1&&s.Y<3),
            "V17: left clip uses fixed-point skip, not rounded integer difference");
        reject(()=>NativeQuadScanlines.Samples([rect[0],rect[2],rect[1],rect[3]],c).ToArray(),
            "V17: reject nonconvex/self-intersecting quad");
        reject(()=>NativeQuadScanlines.Samples([rect[0],rect[0],rect[2],rect[3]],c).ToArray(),
            "V17: reject degenerate polygon");
        byte[] input=[255,0,0, 0,255,0, 20,30,40, 99,
                      1,2,3,  4,5,6,  7,8,9, 99,
                      10,11,12, 13,14,15,16,17,18,99];
        byte[] output=Enumerable.Repeat((byte)100,30).ToArray();
        var src=new NativeCpuSurface(3,3,NativeSurfaceFormat.Bgr24,10,input);
        var dst=new NativeCpuSurface(3,3,NativeSurfaceFormat.Bgr24,10,output);
        check(NativeBgr24QuadRaster.Blit(src,dst,rect,0x00ff00,256)==8&&
            output[..9].SequenceEqual(new byte[]{255,0,0,100,100,100,20,30,40})&&
            output[9]==100&&output[19]==100&&output[29]==100,
            "V17: keyed polygon copy respects pitch and skips key");
        Array.Fill(output,(byte)100);
        check(NativeBgr24QuadRaster.Blit(src,dst,rect,0x00ff00,128)==8&&
            output[..6].SequenceEqual(new byte[]{177,50,50,100,100,100}),
            "V17: BGR24 intensity polygon uses native signed delta");
        var bad=rect.Select(v=>v with {Source=new CardPoint(v.Source.X+2,v.Source.Y)}).ToArray();
        byte[] snapshot=(byte[])output.Clone();
        reject(()=>NativeBgr24QuadRaster.Blit(src,dst,bad,0,256),
            "V17: reject out-of-bounds source samples");
        check(output.SequenceEqual(snapshot),"V17: invalid UV fails before writing");
    }
}
