using Joey.LookAndFeel;

internal static class NativeFusionCardTimelineChecks
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] c=NativeCosineCapture.GenerateTable();
        var two=new NativeFusionCardTimeline(2);
        var start=two.Draw(0,50,72,c)!.Value;
        check(start.OrbitAngle==-128&&start.Materials[0].Center==new CardPoint(656,300)&&
            start.Materials[1].Center==new CardPoint(144,300)&&
            start.Materials[0].Destination==new CardBounds(656,228,0,144),
            "V11: entry assembly, two materials horizontal and zero width");
        var mid=two.Draw(16,50,72,c)!.Value;
        check(mid.Materials[0].Destination==new CardBounds(644,246,25,108),
            "V11: entry independent width/height truncations");
        check(two.Draw(28,50,72,c)!.Value.Materials[0].Destination==
            new CardBounds(635,260,43,81),"V11: final stretched entry");
        var steady=two.Draw(32,50,72,c)!.Value;
        check(steady.Materials[0].Destination==new CardBounds(631,264,50,72)&&
            two.Draw(124,50,72,c)!.Value.OrbitAngle==-128,
            "V11: steady orbit before acceleration");
        var three=new NativeFusionCardTimeline(3);
        var tri=three.Draw(0,50,72,c)!.Value;
        check(tri.Materials.Select(m=>m.AngleIndex).SequenceEqual(new[]{-128,42,213})&&
            tri.Materials[0].Center==new CardPoint(400,44),
            "V11: three material integer angular spacing");
        var contracting=two.Draw(128,50,72,c)!.Value;
        check(contracting.OrbitAngle==-129&&contracting.Materials[0].Radius==256,
            "V11: candidate contraction starts with minus one per Draw");
        var repeat=two.Draw(128,50,72,c)!.Value;
        check(repeat.OrbitAngle==-130&&repeat.Materials[0].AngleIndex!=contracting.Materials[0].AngleIndex,
            "V11: repeated Draw mutates material orbit independently");
        var late=two.Draw(380,50,72,c)!.Value;
        check(late.Materials[0].Radius==(c[126]>>8)&&late.Materials[0].Radius<=7,
            "V11: candidate final contraction");
        var result=two.Draw(384,50,72,c)!.Value;
        check(result.Materials.Count==0&&result.Result==new CardBounds(375,264,50,72)&&
            result.ResultOverlayPending&&!result.DeactivateAfterDraw,
            "V11: candidate result base and unresolved overlay");
        check(two.Draw(508,50,72,c)!.Value.ResultOverlayPending&&
            !two.Draw(512,50,72,c)!.Value.ResultOverlayPending&&
            !two.Draw(636,50,72,c)!.Value.DeactivateAfterDraw&&
            two.Draw(640,50,72,c)!.Value.DeactivateAfterDraw,
            "V11: decompiled result overlay and final Draw boundary");
        reject(()=>new NativeFusionCardTimeline(1),"V11: unsupported material count");
        reject(()=>two.Draw(0,0,72,c),"V11: reject invalid mini dimensions");
    }
}
