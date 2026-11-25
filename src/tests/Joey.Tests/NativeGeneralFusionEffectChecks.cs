using Joey.LookAndFeel;

internal static class NativeGeneralFusionEffectChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] c=NativeCosineCapture.GenerateTable();
        string[] names=["fusion_mark.yga","fusion_uzu.yga","fusion_cir.yga",
            "fu_kira_a1.bmp","fu_kira_a2.bmp","fu_kira_a3.bmp",
            "fu_kira_b1.bmp","fu_kira_b2.bmp","fu_kira_b3.bmp"];
        int[] widths=[76,274,194,10,14,18,10,16,22];
        int[] heights=[86,274,194,10,14,18,10,16,22];
        var bank=names.Select(n=>NativeFusionResources.Prefix+n).ToArray();
        NativeFusionResources.ValidateObservedBank(bank);
        CardBounds Size(int i)=>NativeFusionResources.ValidateObservedSize(i,widths[i],heights[i]);
        check(Size(0)==new CardBounds(0,0,76,86)&&Size(8)==new CardBounds(0,0,22,22),
            "V11: v3 atlas dimensions");
        reject(()=>NativeFusionResources.ValidateObservedBank(bank.Reverse().ToArray()),
            "V11: review rejects reordered manifest");
        reject(()=>NativeFusionResources.ValidateObservedSize(1,273,274),
            "V11: review rejects unexpected dimensions");
        check(NativeFusionResources.IsOrbitalBitmap(bank[3])&&
            !NativeFusionResources.IsOrbitalBitmap(bank[0]),"V11: diagnostic key confined to orbital BMPs");
        check(NativeGeneralFusionEffect.Particles(0,0,c,Size).Length==0&&
            NativeGeneralFusionEffect.Particles(28,0,c,Size).Length==0,
            "V11: first orbital group begins at 32");
        var first=NativeGeneralFusionEffect.Particles(32,-32,c,Size);
        check(first.Select(p=>p.Key).SequenceEqual(new[]{8,7,6})&&
            first[0].Center==new CardPoint(912,300)&&first[0].Radius==512&&
            first[0].Destination==new CardBounds(901,289,22,22),
            "V11: recovered selector, zero slot and global center 400");
        var second=NativeGeneralFusionEffect.Particles(64,0,c,Size);
        check(second.Where(p=>p.Group==64).Select(p=>p.Key).SequenceEqual(new[]{3,6,7})&&
            second.Where(p=>p.Group==32).Select(p=>p.Key).SequenceEqual(new[]{3,4,5,4})&&
            second.Where(p=>p.Group==32).All(p=>p.Age==32),
            "V11: groups step 32, separate selector phases");
        check(NativeGeneralFusionEffect.Particles(384,0,c,Size).Length==0,
            "V11: orbital group ages out at 384");
        var timeline=new NativeGeneralFusionTimeline(0);
        var initial=NativeGeneralFusionEffect.Draw(timeline,c,Size);
        check(initial is { } start&&start.Primary.ScaleFixed16==0&&start.Additional is null,
            "V11: no scale on entry");
        timeline.AdvanceNativeUpdate();
        check(NativeGeneralFusionEffect.Draw(timeline,c,Size)?.SoundRequest==0x10000020u,
            "V11: sound request at Draw counter 4");
        while(timeline.Counter<384)timeline.AdvanceNativeUpdate();
        var late=NativeGeneralFusionEffect.Draw(timeline,c,Size);
        check(late is { } l&&l.SoundRequest==0x10000021u&&l.Additional?.ResourceIndex==2&&
            l.Primary.AngleIndex==timeline.Angle&&l.Additional?.AngleIndex==timeline.Angle,
            "V11: circle and spiral share mutated Draw angle");
        while(timeline.Counter<448)timeline.AdvanceNativeUpdate();
        check(NativeGeneralFusionEffect.Draw(timeline,c,Size) is null,
            "V11: effect-only review cap does not claim native object lifetime");
    }
}
