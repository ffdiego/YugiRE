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
              "V30: original historical strip dimensions and packed offsets");
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
        check(!NativeCinematicRouting.DestinyBoardVictory(0).HasPlayableDraw,
              "V30: no invisible wait before renderer is complete");
    }
}
