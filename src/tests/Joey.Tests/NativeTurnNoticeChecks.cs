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
        reject(()=>NativePhaseNoticeResources.Parse(manifest,paths[..^1]),
            "V07: missing resource rejected");
        reject(()=>NativePhaseNoticeResources.Parse(string.Join("\n",names[..45]),paths),
            "V07: no truncated manifest");
    }
}
