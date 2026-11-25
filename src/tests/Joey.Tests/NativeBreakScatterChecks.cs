using Joey.LookAndFeel;
using System.Security.Cryptography;

internal static class NativeBreakScatterChecks
{
    public static void Run(Action<bool,string> check)
    {
        var n=NativeBreakScatter.GenerateNoise();
        check(n.Length==256 &&
            Convert.ToHexString(SHA256.HashData(n)).Equals(
                "62ad1265fa2adde0c900daee2cee11a9405a49573388877efbb410318968c854",
                StringComparison.OrdinalIgnoreCase),
            "V22: CRT srand(0) and 256 low rand bytes");
        n[0]=0;
        check(NativeBreakScatter.GenerateNoise()[0]==38,"V22: local table, no external mutation");
        check(NativeBreakScatter.Classify(0)==NativeBreakScatterKind.None &&
            NativeBreakScatter.Classify(-1)==NativeBreakScatterKind.None &&
            NativeBreakScatter.Classify(1)==NativeBreakScatterKind.Fragmented &&
            NativeBreakScatter.Classify(255)==NativeBreakScatterKind.Fragmented &&
            NativeBreakScatter.Classify(256)==NativeBreakScatterKind.Direct,
            "V22: raw scatter dispatch boundaries");
        var blocks=NativeBreakScatter.Blocks(50,72).ToArray();
        check(blocks.Length==468 && blocks[0].Source==new CardBounds(0,0,4,2) &&
            blocks[^1].Source==new CardBounds(48,70,2,2),
            "V22: mini portrait fragments, clipped right edge");
        check(blocks.Sum(b=>b.Source.Width*b.Source.Height)==50*72 &&
            NativeBreakScatter.Blocks(72,50).Count()==450 &&
            NativeBreakScatter.Blocks(5,3).Last().Source==new CardBounds(4,2,1,1),
            "V22: orientation and odd edge fragments");
        check(blocks[0].RadialX==-25 && blocks[0].RadialY==-36 &&
            blocks[0].RadiusSquared==1921 &&
            blocks[0].NoiseIndexX==0 && blocks[0].NoiseIndexY==1 &&
            blocks[0].NoiseX==38 && blocks[0].NoiseY==39 &&
            blocks[13].NoiseIndexX==26 && blocks[13].NoiseIndexY==39 &&
            blocks[13].NoiseX==209 && blocks[13].NoiseY==99 &&
            blocks[^1].NoiseIndexX==190 && blocks[^1].NoiseIndexY==99,
            "V22: independent row phases, byte lookup and radial origin");
        // Inject a synthetic divisor; not an assertion about native x87.
        var block=blocks[0];
        check(NativeBreakScatter.Project(block,new CardPoint(100,200),50,72,128,43)
            ==new CardBounds(94,192,4,2),
            "V22: candidate integer displacement preserves signed shift");
        var center=NativeBreakScatter.Blocks(48,72).Single(b=>b.Source.X==24&&b.Source.Y==36);
        check(NativeBreakScatter.Project(center,new CardPoint(100,200),48,72,1,0)
            ==new CardBounds(124,236,4,2),"V22: zero radial length stays centered");
    }
}
