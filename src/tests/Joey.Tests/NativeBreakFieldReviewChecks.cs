using Joey.LookAndFeel;

internal static class NativeBreakFieldReviewChecks
{
    public static void Run(Action<bool,string> check)
    {
        int[] cosine=NativeCosineCapture.GenerateTable();
        int[] nativeParameters=[256,231,207,182,159,136,114,94,75,59,44,31,20,12,5,2];
        check(NativeBreakResources.Parse("shine.bmp\r\n",[NativeBreakResources.Shine])==NativeBreakResources.Shine,
            "V22: original one-entry manifest");
        foreach(string wrong in new[]{"","expl1.yga","shine.bmp 1","shine.bmp\nshine.bmp","../shine.bmp"})
        {
            bool rejected=false;
            try { _=NativeBreakResources.Parse(wrong,[NativeBreakResources.Shine]); }
            catch(InvalidDataException) { rejected=true; }
            check(rejected,"V22: reject unconfirmed manifest");
        }
        bool missing=false;
        try { _=NativeBreakResources.Parse("shine.bmp",[]); }
        catch(InvalidDataException) { missing=true; }
        check(missing,"V22: missing DAT asset is explicit");

        // 50x72 original portrait miniature and a non-square odd-sized
        // synthetic texture exercise the final partial tiles.
        foreach((int w,int h) in new[]{(50,72),(51,73)})
        for(int side=0;side<2;side++)
        for(int position=0;position<2;position++)
        for(int perspective=0;perspective<2;perspective++)
        for(int visible=0;visible<2;visible++)
        {
            ushort descriptor=(ushort)(visible<<15 | position<<14 | side);
            var timeline=new NativeBreakTimeline();
            CardPoint center=new(525,352);
            int rotation=90*(position+2*(side^perspective));
            bool swap=rotation is 90 or 270;
            int rw=swap?h:w,rh=swap?w:h;
            for(int step=0;step<16;step++)
            {
                var frame=NativeBreakFieldReview.Sample(timeline,center,descriptor,perspective,w,h,cosine)!;
                check(frame.Counter==step*16 && timeline.Active && frame.BlocksRelease==(step<4),
                    "V22: visible 16 updates, busy only four");
                check(frame.DistortionParameter==nativeParameters[step] &&
                      frame.Kind==(step==0?NativeBreakScatterKind.Direct:NativeBreakScatterKind.Fragmented),
                    "V22: parameter and direct vs tiled route");
                check(frame.RotationDegrees==rotation && frame.OrientedCardBounds==new CardBounds(
                    center.X-rw/2,center.Y-rh/2,rw,rh),"V22: orientation before fragmentation");
                check(frame.Particles.Length==5 && frame.Particles.All(p=>p.Bounds.Width==74 && p.Bounds.Height==73),
                    "V22: real shine dimensions");
                check(frame.Particles.Select(p=>p.RawCompositorArgument).SequenceEqual(
                    new[]{0,0,0,256,256}.Select(high=>high+255-step*16)),
                    "V22: raw +AC argument preserves angle high byte");
                if(step==0)
                    check(frame.Fragments.Length==1 &&
                          frame.Fragments[0].Source==new CardBounds(0,0,w,h) &&
                          frame.Fragments[0].Destination==frame.OrientedCardBounds,
                        "V22: whole source before fragmentation");
                else
                {
                    check(frame.Fragments.Length==(rw+3)/4*((rh+1)/2),
                        "V22: row-major tile count after rotation");
                    int[,] seen=new int[w,h];
                    bool valid=true;
                    foreach(var f in frame.Fragments)
                    {
                        var s=f.Source;
                        valid &= s.X>=0 && s.Y>=0 && s.Width>0 && s.Height>0 &&
                            s.X+s.Width<=w && s.Y+s.Height<=h &&
                            f.Destination.Width==(swap?s.Height:s.Width) &&
                            f.Destination.Height==(swap?s.Width:s.Height);
                        if(!valid)break;
                        for(int x=s.X;x<s.X+s.Width;x++)
                        for(int y=s.Y;y<s.Y+s.Height;y++)seen[x,y]++;
                    }
                    check(valid && seen.Cast<int>().All(n=>n==1),
                        "V22: rotated tiles cover every source pixel once without gaps");
                }
                check(timeline.Counter==step*16,"V22: sample never advances");
                timeline.AdvanceNativeUpdate();
            }
            check(!timeline.Active && !timeline.BlocksRelease &&
                NativeBreakFieldReview.Sample(timeline,center,descriptor,perspective,w,h,cosine) is null,
                "V22: terminal draw absent and gate clear");
            timeline.AdvanceNativeUpdate();
            check(timeline.Counter==256,"V22: terminal update idempotent");
        }
        for(int side=0;side<2;side++)
        for(int perspective=0;perspective<2;perspective++)
        for(int region=0;region<=10;region++)
        for(int slot=0;slot+region<=10;slot++)
        {
            int zone=region+slot;
            var actual=new NativeBreakFieldTarget((ushort)(side | region<<1 | slot<<6));
            var expected=NativeDisabledChainTarget.FromField(side,zone,false);
            check(actual.Center(perspective)==expected.Center(perspective),
                "V22: region+slot normalization preserves field center");
        }
        for(int side=0;side<2;side++)
        for(int perspective=0;perspective<2;perspective++)
        for(int region=0;region<5;region++)
        {
            var snapshot=new NativeBreakFieldTarget((ushort)(0x8000 | 0xff<<6 | region<<1 | side));
            check(snapshot.IsCombatSnapshot && snapshot.FieldZone==region &&
                  snapshot.Center(perspective)==NativeDisabledChainTarget.FromField(side,region,false).Center(perspective),
                "V22: snapshot FF uses monster-region center");
            check(NativeBreakFieldReview.Sample(new(),snapshot.Center(perspective),snapshot.Descriptor,perspective,50,72,cosine)
                  is { Fragments.Length:1 },"V22: combat snapshot review is supported");
        }
        foreach(ushort unsupported in new ushort[]{0x8016,0x803e,0x8154,0xbfca})
        {
            bool rejected=false;
            try { _=NativeBreakFieldReview.Sample(new(),new(525,352),unsupported,0,50,72,cosine); }
            catch(ArgumentException) { rejected=true; }
            check(rejected,"V22: hand and unsupported field/snapshot descriptors rejected");
        }
    }
}
