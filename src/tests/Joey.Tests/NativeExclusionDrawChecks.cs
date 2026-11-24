using Joey.LookAndFeel;

internal static class NativeExclusionDrawChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] cosine=ActivationEffectPlayback.ApproximateCosineTable();
        var activation=new NativeAlternativeActivationTimeline();
        for(int i=0;i<33;i++)
        {
            int t=-128+i*16;
            var sample=activation.Sample(200,290,1,cosine)!.Value;
            int scale=t<0?cosine[t&511]>>9:128;
            check(sample.Counter==t && sample.ScaleFixed8==scale &&
                sample.CardIntensity==(t<256?2*scale:cosine[(t-256)&511]>>8) &&
                sample.CardBounds.Width==200*scale>>8 &&
                sample.CardBounds.Height==290*scale>>8 &&
                sample.ScratchBounds==new CardBounds(475,228,100,145) &&
                sample.Glow.HasValue==(t>=0&&t<=256),
                "V25: full card scale, fade and inherited glow interval");
            check(activation.Counter==t,"V25: Draw sample does not mutate update");
            activation.AdvanceNativeUpdate();
        }
        check(activation.Sample(200,290,0,cosine) is null,"V25: inactive sample");

        var center=new CardPoint(525,352);
        CardBounds Size(int key)=>key switch {0=>new(0,0,58,80),1=>new(0,0,15,144),
            2=>new(0,0,50,72),_=>throw new Exception("Must size particles using key 2")};
        for(int side=0;side<2;side++)
        for(int perspective=0;perspective<2;perspective++)
        for(int visible=0;visible<2;visible++)
        {
            var start=NativeExclusionStart.FromConstructor((ushort)(side|visible<<15),123);
            var e=new NativeExclusionTimeline(start);
            int count=0;
            while(e.Active)
            {
                int t=e.Counter+8;
                var preview=e.SampleNextDraw(center,50,72,perspective,cosine,Size)!.Value;
                check(e.Counter==t-8,"V26: pure query");
                var frame=e.Draw(center,50,72,perspective,cosine,Size)!.Value;
                check(frame.Counter==t && frame.Passes.SequenceEqual(preview.Passes) &&
                    frame.ReleasesCardBank==(t==256) && e.Counter==t,
                    "V26: exactly one Draw increment; terminal release");
                int?[] keys=t<96?[null,1,1,1]:t<128?[null,1,1,1,0]:
                    t==128?[1,1,1,0]:[t/8%6+2,0];
                check(frame.Passes.Select(p=>p.ResourceKey).SequenceEqual(keys),"V26: ordered pass selectors");
                if(t<128)check(frame.Passes[0].RotationDegrees==180*(side^perspective) &&
                    frame.Passes[0].Bounds==new CardBounds(500,316,50,72),
                    "V26: mini orientation ignores position bit");
                if(t==128)check(frame.Passes.Take(3).Select(p=>p.Bounds.X).SequenceEqual([502,518,534]),
                    "V26: three beam centers separated by original width+1");
                if(t>128)
                {
                    int c=cosine[(t-128)&511],s=(c>>9)+128;
                    check(frame.Passes[0].Bounds.Width==50*s>>8 &&
                        frame.Passes[0].Intensity==c>>7 && frame.Passes[1].Intensity==c>>8,
                        "V26: scaled particles and separate intensities");
                }
                count++;
            }
            check(count==24 && e.Draw(center,50,72,perspective,cosine,Size) is null &&
                start.VisibleCardArgument==(visible==1?123u:0u),
                "V26: no hidden card identity; 24 Draws, no Update dependency");
        }
        string[] names=["jyoga_hikacard.yga","jyoga_hikatate.yga","jyoga_ryusi1.bmp",
            "jyoga_ryusi2.bmp","jyoga_ryusi3.bmp","jyoga_ryusi4.bmp","jyoga_ryusi5.bmp","jyoga_ryusi6.bmp"];
        string[] paths=names.Select(n=>NativeExclusionResources.Prefix+n).ToArray();
        string manifest=string.Join("\n",names);
        check(NativeExclusionResources.Parse(manifest,paths.Reverse()).SequenceEqual(paths),
            "V26: manifest order validated");
        reject(()=>NativeExclusionResources.Parse(manifest,paths[..^1]),"V26: missing resource");
        reject(()=>NativeExclusionResources.Parse(string.Join("\n",names.Reverse()),paths),"V26: unknown key order");
    }
}
