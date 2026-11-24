using Joey.LookAndFeel;

internal static class NativeNoticeProjectionChecks
{
    public static void Run(Action<bool,string> check)
    {
        // Synthetic cardinal/oblique values, not an assertion about the
        // captured x87 cosine table. Exercise asymmetric perspective.
        int[] c = new int[512];
        c[0]=65536; c[64]=46341; c[448]=46341;
        c[384]=0; c[128]=0; c[320]=-46341; c[256]=-65536;
        var turn = new NativeTurnNoticePlan(1,0,0,0);
        NativeQuadProjection At(int t) =>
            NativeNoticeProjection.Project(turn.DrawAtCounter(t)!.Value,395,65,c);
        var flat = At(64);
        check(flat.Source==new NativeQuad(new(0,0),new(394,0),new(394,64),new(0,64)) &&
              flat.Destination==new NativeQuad(new(328,268),new(722,268),new(722,332),new(328,332)) &&
              !flat.Degenerate,"V07: flat inclusive source/destination endpoints");
        var enter=At(32);
        var exit=At(352);
        check(enter.Destination.TopLeft.X==386 && enter.Destination.TopRight.X==664 &&
              enter.Destination.TopLeft.Y==250 && enter.Destination.BottomLeft.Y==350 &&
              enter.Destination.TopRight.Y==276 && enter.Destination.BottomRight.Y==323,
            "V07: positive angle narrows width and raises left edge");
        check(exit.Destination.TopLeft.Y==enter.Destination.TopRight.Y &&
              exit.Destination.BottomLeft.Y==enter.Destination.BottomRight.Y &&
              exit.Destination.TopRight.Y==enter.Destination.TopLeft.Y &&
              exit.Destination.BottomRight.Y==enter.Destination.BottomLeft.Y,
            "V07: opposite angles mirror perspective");
        check(At(0).Degenerate && At(0).Destination.TopLeft.X==525 &&
              At(0).Destination.TopRight.X==524,
            "V07: edge-on native endpoints; no artificial minimum width");
        for(int perspective=0;perspective<2;perspective++)
        for(int side=0;side<2;side++)
        {
            var draw=new NativeTurnNoticePlan(1-side,side,perspective,0).DrawAtCounter(32)!.Value;
            check(NativeNoticeProjection.Project(draw,395,65,c)==enter,
                "V07: both relative sides share projection, not resource key");
        }
        check(NativeQuadRasterDispatch.Route(0)==NativeQuadRasterRoute.IntensityFamily &&
              NativeQuadRasterDispatch.Route(255)==NativeQuadRasterRoute.IntensityFamily &&
              NativeQuadRasterDispatch.Route(256)==NativeQuadRasterRoute.BaseFamily &&
              NativeQuadRasterDispatch.Route(512)==NativeQuadRasterRoute.BaseFamily,
            "V17: 00422E30 intensity boundary, no implicit source alpha");
    }
}
