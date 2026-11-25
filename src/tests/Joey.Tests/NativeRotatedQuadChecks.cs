using Joey.LookAndFeel;

internal static class NativeRotatedQuadChecks
{
    internal static void Run(Action<bool,string> check)
    {
        // Cardinal and diagonal values also measured in the V01 runtime table.
        int[] c=new int[512];
        c[0]=65536;c[256]=-65536;c[64]=c[448]=46340;
        c[192]=c[320]=-46340;
        var center=new CardPoint(525,300);
        var source=new CardBounds(0,0,35,71);
        NativeQuadProjection P(int a,int sx=65536,int sy=65536) =>
            NativeRotatedQuad.Project(center,source,35,71,a,sx,sy,c)
                ?? throw new Exception("Unexpected empty quad");
        check(P(0).Destination==new NativeQuad(new(508,265),new(542,265),
            new(542,335),new(508,335)) && !P(0).Degenerate,
            "V04: sword dimensions minus one and inclusive endpoints");
        check(P(256).Destination==new NativeQuad(new(542,335),new(508,335),
            new(508,265),new(542,265)),
            "V04: opposite side 180-degree rotation preserves source corner order");
        check(P(128).Destination==new NativeQuad(new(560,283),new(560,317),
            new(490,317),new(490,283)) && P(-384)==P(128),
            "V04: quarter turn and angle index wrap");
        check(P(64).Destination==new NativeQuad(new(537,263),new(561,287),
            new(512,336),new(488,312)),
            "V04: signed SAR rounds negative rotated offsets down");
        check(P(0,32768,32768).Destination==new NativeQuad(new(517,283),
            new(534,283),new(534,318),new(517,318)),
            "V04: half scale uses asymmetric integer endpoints");
        check(P(0,32768,65536).Destination.BottomRight==new CardPoint(534,335),
            "V04: independent axis scales");
        var crop=NativeRotatedQuad.Project(center,new(9,4,35,71),35,71,0,65536,65536,c);
        check(crop.HasValue && crop.Value.Source==new NativeQuad(new(9,4),new(43,4),
            new(43,74),new(9,74)) && crop.Value.Destination==P(0).Destination,
            "V04: nonzero source rectangle origin independent of pivot");
        check(NativeRotatedQuad.Project(center,source,35,71,0,0,65536,c)==null &&
            NativeRotatedQuad.Project(center,source,35,71,0,1,65536,c)==null,
            "V04: zero scale or rounded extent produces no native blit");
        var odd=NativeRotatedQuad.Project(center,new(0,0,4,6),4,6,0,32768,32768,c);
        check(odd.HasValue && odd.Value.Destination==new NativeQuad(new(524,299),
            new(526,299),new(526,302),new(524,302)),
            "V04: MulDiv rounds a positive half up");
    }
}
