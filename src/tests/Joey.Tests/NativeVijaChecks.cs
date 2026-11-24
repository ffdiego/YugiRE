using Joey.LookAndFeel;

internal static class NativeVijaChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        for (ushort index = 0; index < 10; index++)
        {
            int i = index % 5;
            var single = new NativeVijaContract(index, false, 0x800a);
            var group = new NativeVijaContract(index, true, 0x810b);
            check(single.LetterIndex == i && single.InitialCounter == -4 &&
                  single.PhaseLimit == 320 && single.BlockingThrough == 384 &&
                  single.BlocksCompletion(384) && !single.BlocksCompletion(385) &&
                  single.SoundRequest == 0x10000022 && single.Layer == 0x120,
                  "V30: single-letter constructor and inclusive gate");
            check(group.LetterIndex == i && group.InitialCounter == -4-72*i &&
                  group.PhaseLimit == 640-72*i && group.BlockingThrough == 704-72*i &&
                  group.BlocksCompletion(group.BlockingThrough) &&
                  !group.BlocksCompletion(group.BlockingThrough+1) &&
                  group.SoundRequest == 0x10000023 && group.Side == 1 &&
                  group.Region == 5 && group.Slot == 4,
                  "V30: group phase displacement applies at both ends");
        }
        check(new NativeVijaContract(0,true,0x80d6).Layer == 0x231,
              "V30: hand-like region 11 uses slot-relative layer");
        check(NativeVijaAtlas.FromParameter(5004,400,80) == new NativeVijaAtlas(5,4,80,80) &&
              NativeVijaAtlas.FromParameter(8031,256,37) == new NativeVijaAtlas(8,31,32,37),
              "V30: historically documented packed offsets; synthetic geometry fixture");
        reject(()=>NativeVijaAtlas.FromParameter(5004,399,80),"V30: no truncated atlas");
        reject(()=>NativeVijaAtlas.FromParameter(4,400,80),"V30: no zero divisor");
        string[] names = ["vija_skel.bmp","vija_smoke.yga","vija_moji_iro.bmp",
            "vija_moji_mask.bmp","vija_moji_smo.yga"];
        string[] paths=names.Select(n=>NativeVijaResources.Prefix+n).ToArray();
        const string fixture="// synthetic order\nvija_smoke.yga 5004\nvija_skel.bmp\nvija_moji_iro.bmp 0\nvija_moji_mask.bmp\nvija_moji_smo.yga 8031\n";
        var resources=NativeVijaResources.Parse(fixture,paths);
        check(resources[0].Path==paths[1] && resources[0].Parameter==5004 &&
              resources[1].Parameter is null && resources[2].Parameter==0,
              "V30: preserve native manifest order and absent vs zero parameter");
        reject(()=>NativeVijaResources.Parse(fixture,paths[..^1]),"V30: missing resource");
        reject(()=>NativeVijaResources.Parse(fixture+"vija_skel.bmp\n",paths),"V30: duplicate");
        // Deliberately synthetic logical-key geometry. No assertion about
        // the private DAT's order or exact compositor pixels.
        int[] cos=ActivationEffectPlayback.ApproximateCosineTable();
        NativeVijaTexture Tex(int key)=>key switch {
            0=>new(400,80,5004),1=>new(210,41,5007),
            2=>new(160,37,5000),3=>new(160,37,5031),4=>new(256,37,8031),
            _=>throw new Exception("unexpected key")};
        check(NativeVijaTimeline.VictoryResourceIds.SequenceEqual(new ushort[]{1528,1541,1542,1543,1544}),
            "V30: five resource IDs in 4-byte table entries");
        for(ushort letter=0;letter<5;letter++)
        for(int mode=0;mode<2;mode++)
        {
            var v=new NativeVijaTimeline(new(letter,mode==1,0x800a),new(369,460));
            int draws=0,visible=0;
            while(v.DrawFlag)
            {
                int t=v.Counter+4;
                int previous=v.Counter;
                var before=v.SampleNextDraw(cos,Tex);
                check(v.Counter==previous,"V30: pure sample");
                var draw=v.Draw(cos,Tex);
                check(v.Counter==t && (draw is null)==(t<0) &&
                    (before is null)==(draw is null),"V30: stagger increments even before zero");
                if(draw is not null)
                {
                    visible++;
                    check(draw.Center==v.CenterAt(t,cos) &&
                        draw.Base.Source.X==80*(t/8%5) &&
                        draw.Base.Destination.Width==80*Math.Min(t*4,256)/256 &&
                        draw.Base.Intensity==(t<64?t*4:v.FadeAt(t)) &&
                        draw.DrawFlagAfter==(t<v.Contract.BlockingThrough),
                        "V30: exact key0 scale, frame, fade and draw flag");
                    check((draw.Overlay is not null)==(t>=32) &&
                        (draw.Scratch is not null)==(t>=64),"V30: pass thresholds");
                    if(t==32)check(draw.Overlay!.Value.Source.X==0 &&
                        draw.Overlay.Value.Destination.Y==draw.Base.Destination.Y+15,
                        "V30: initial overlay descent");
                    if(t==64)check(draw.Scratch!.Value.MaskSource.X==letter*32 &&
                        draw.Letters.Length==37 && draw.Letters.All(x=>x.Destination.Width==0),
                        "V30: selected letter and initial per-row expansion");
                    if(t>=64)
                    {
                        var s=draw.Scratch!.Value;
                        check(s.FirstBackgroundX==t%256 && s.SecondBackgroundX==t%256-256 &&
                            s.ColorSource.X==t/8%5*32,"V30: scratch wrap uses full width");
                    }
                    if(t==128)check(draw.Letters[0].Destination.Width==16 &&
                        draw.Letters.All(x=>x.DistortedRow),"V30: line-wise width halfway");
                    if(t==192)check(draw.Letters.Length==1 && !draw.Letters[0].DistortedRow &&
                        draw.Letters[0].Destination.Width==32,"V30: undistorted letter at 192");
                    if(mode==1&&t==256)check(draw.Center.X==(letter+2)*100 &&
                        draw.Center.Y==300+(cos[(8*t-128)&511]<<3>>16),"V30: group endpoint");
                }
                draws++;
            }
            check(draws==(mode==0?97:177) && v.Counter==v.Contract.BlockingThrough &&
                v.BlocksCompletion && v.Draw(cos,Tex) is null && visible==v.Counter/4+1,
                "V30: terminal DrawFlag distinct from inclusive virtual predicate");
        }
        check(!NativeCinematicRouting.DestinyBoardVictory(0).HasPlayableDraw,
              "V30: no invisible wait before renderer is complete");
    }
}
