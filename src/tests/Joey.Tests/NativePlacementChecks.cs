using Joey.LookAndFeel;

internal static class NativePlacementChecks
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] c=NativeCosineCapture.GenerateTable();
        var origin=new CardBounds(100,400,100,144);
        var target=new CardBounds(500,300,50,72);
        var a=NativePlacementTravel.Sample(origin,target,true,true,true,0,c);
        var b=NativePlacementTravel.Sample(origin,target,true,true,true,4,c);
        var d=NativePlacementTravel.Sample(origin,target,true,true,true,8,c);
        check(a.Portrait==origin&&a.AngleIndex==0&&!a.FaceDown&&a.FaceWidthFixed16==65536,
            "placement: local set starts face-up upright");
        check(b.Portrait.Width==75&&b.Portrait.Height==108&&b.AngleIndex==64&&
            b.FaceProgress==128&&b.FaceWidthFixed16==0,"placement: linear size and edge-on midpoint");
        check(d.Portrait==target&&d.AngleIndex==128&&d.FaceDown&&d.FaceWidthFixed16==65536,
            "placement: set arrives face-down rotated");
        var cpu=NativePlacementTravel.Sample(origin,target,false,false,false,0,c);
        check(cpu.FaceDown&&cpu.AngleIndex==256&&
            !NativePlacementTravel.Sample(origin,target,false,false,false,8,c).FaceDown,
            "placement: enemy summon reveals in transit, not at departure");
        var cpuSet=NativePlacementTravel.Sample(origin,target,false,true,true,4,c);
        check(cpuSet.FaceDown&&cpuSet.FaceWidthFixed16==65536&&cpuSet.AngleIndex==320,
            "placement: enemy set never reveals identity");
        string[] bank=NativePlacementAssets.SetBank();
        check(NativePlacementAssets.Aura(false).EndsWith("eff_set_y.bmp")&&
              NativePlacementAssets.Aura(true).EndsWith("eff_set_b.bmp")&&
              NativePlacementAssets.TributeStar(1).EndsWith("eff_star1_y.bmp"),
            "placement: manifest order and type colors");
        NativePlacementAssets.Validate(string.Join("\n",bank.Select(p=>p[NativePlacementAssets.Prefix.Length..])),bank);
        reject(()=>NativePlacementAssets.Validate("eff_set_b.bmp",bank),"placement: reject incomplete manifest");
        var strips=NativeSummonRainbow.Strips(80,100,94,50,72,new(525,300));
        check(strips.Length==2&&strips[0].Source==new CardBounds(25,80,50,14)&&
            strips[1].Source==new CardBounds(25,0,50,58)&&
            strips[1].Destination==new CardBounds(500,278,50,58),
            "placement: rainbow wraps effect height, not card height");
    }
}
