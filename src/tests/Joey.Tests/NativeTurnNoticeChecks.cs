using Joey.LookAndFeel;

internal static class NativeTurnNoticeChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        for (int perspective=0;perspective<2;perspective++)
        for (int side=0;side<2;side++)
        {
            int offset=side==perspective ? 0:10;
            var changed=new NativeTurnNoticePlan(1-side,side,perspective,0);
            check(changed.ChangesPlayer && changed.NoticeKind==6 &&
                  changed.SoundRequest==0x10000005u &&
                  changed.PreloadKeys().SequenceEqual(new[]{27,29,31,33,35}.Select(x=>x+offset)),
                "V07: new player preload keys are perspective-relative");
            for(int i=0;i<=65;i++)
            {
                var sample=changed.Sample(i);
                check(sample.TurnNoticeCounter==(i<48 ? (int?)(i*8):null) &&
                      sample.ButtonFlipUpdate==(i<64 ? (int?)i:null) &&
                      sample.PhaseElapsedUpdates==(i>=64 ? (int?)(i-64):null),
                    "V07: parallel notice and panel flip, then next phase");
            }
            int first=side==perspective ? 36 : 26;
            for(int t=0;t<384;t++)
            {
                var draw=changed.DrawAtCounter(t)!.Value;
                int key=t<64 ? first : t<=320 ? first+1+Math.Min((t-64)/16,8) : first+9;
                int angle=t<64 ? 2*(64-t) : t<=320 ? 0 : 2*(320-t);
                check(draw.LookupKey==key && draw.AngleIndex==angle &&
                      draw.Center==new CardPoint(525,300) &&
                      draw.ReleasePriorKey==(key>first ? (int?)(key-1) : null),
                    "V07: assembly selector, signed angle and release request");
            }
            check(changed.DrawAtCounter(384) is null && changed.DrawAtCounter(400) is null,
                "V07: Draw deactivates at 384");
            var noPhase=new NativeTurnNoticePlan(1-side,side,perspective,-1);
            check(noPhase.PhaseStartsAtUpdate is null && noPhase.Sample(64).PhaseElapsedUpdates is null,
                "V07: native -1 suppresses phase after panel flip");
            var battle=new NativeTurnNoticePlan(side,side,perspective,3);
            check(!battle.ChangesPlayer && battle.NoticeKind==3 && battle.SoundRequest is null &&
                  battle.Sample(0)==new NativeTurnNoticeSample(null,null,0) &&
                  battle.PreloadKeys().SequenceEqual(new[]{13,15,17}.Select(x=>x+(side==perspective?0:7))),
                "V07: same player Battle path");
            var main=new NativeTurnNoticePlan(side,side,perspective,2);
            check(main.PreloadKeys().Length==0 && main.PhaseStartsAtUpdate==0,
                "V07: standard phase path");
        }

        // Synthetic order deliberately unrelated to filenames. No claim
        // about the actual proprietary manifest's order in this fixture.
        string[] names=Enumerable.Range(0,46).Select(i=>$"sprite_{i}_e.yga").ToArray();
        string[] paths=names.Select(x=>NativePhaseNoticeResources.Prefix+x).ToArray();
        string manifest=string.Join("\n",Enumerable.Range(0,46)
            .Select(i=>i==27 ? "sprite_27_?.yga 0" : $"sprite_{i}_?.yga"));
        var records=NativePhaseNoticeResources.Parse(manifest,paths.Reverse());
        var keys=NativePhaseNoticeResources.Preloads(new(1,0,0,0),records);
        check(keys[0].Path==paths[27] && keys[0].Parameter==0 &&
              keys[1].Path==paths[29] && keys[1].Parameter is null,
            "V07: preload lookup uses manifest order and preserves missing fields");
        // Exact repeated endpoints in the observed v3 English manifest.
        string[] turns=Enumerable.Range(0,20).Select(i=>
        {
            int frame=i%10;
            char side=i<10?'b':'r',last=i<10?'r':'b';
            return frame==9?$"nex_e_{last}.yga":$"nex_e_{side}{(frame==0?"":frame.ToString())}.yga";
        }).ToArray();
        var observed=records.Take(26).Concat(turns.Select(n=>
            new NativePhaseNoticeResource(NativePhaseNoticeResources.Prefix+n,null))).ToArray();
        NativePhaseNoticeResources.ValidateTurnNoticeBank(observed);
        var near=new NativeTurnNoticePlan(1,0,0,0);
        var far=new NativeTurnNoticePlan(0,1,0,0);
        check(observed[near.DrawAtCounter(0)!.Value.LookupKey].Path.EndsWith("nex_e_r.yga")&&
            observed[near.DrawAtCounter(376)!.Value.LookupKey].Path.EndsWith("nex_e_b.yga")&&
            observed[far.DrawAtCounter(0)!.Value.LookupKey].Path.EndsWith("nex_e_b.yga")&&
            observed[far.DrawAtCounter(376)!.Value.LookupKey].Path.EndsWith("nex_e_r.yga"),
            "V07 observed manifest: start old color, end new color");
        reject(()=>NativePhaseNoticeResources.ValidateTurnNoticeBank(observed[..^1]),
            "V07 turn notice bank rejects missing endpoint");
        var wrong=observed.ToArray();(wrong[27],wrong[28])=(wrong[28],wrong[27]);
        reject(()=>NativePhaseNoticeResources.ValidateTurnNoticeBank(wrong),
            "V07 turn notice bank rejects reordered frames");
        reject(()=>NativePhaseNoticeResources.Parse(manifest,paths[..^1]),
            "V07: missing resource rejected");
        reject(()=>NativePhaseNoticeResources.Parse(string.Join("\n",names[..45]),paths),
            "V07: no truncated manifest");
    }
}
