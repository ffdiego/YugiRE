using Joey.LookAndFeel;

internal static class NativeFusionPlaybackChecks
{
    internal static void Run(Action<bool,string> check)
    {
        int[] c=NativeCosineCapture.GenerateTable();
        CardBounds Size(int key)=>new(0,0,10,10);
        var a=new NativeFusionPlayback(2);
        var b=new NativeFusionPlayback(2);
        List<uint> soundsA=[],soundsB=[];
        a.AdvanceTo(0,c,Size,soundsA.Add);
        check(a.Cards?.Materials.Count==2&&a.Cards?.Counter==0,"V11 live playback starts at zero");
        for(int i=1;i<=NativeFusionPlayback.LastSample;i++)a.AdvanceTo(i,c,Size,soundsA.Add);
        b.AdvanceTo(NativeFusionPlayback.LastSample,c,Size,soundsB.Add);
        b.AdvanceTo(NativeFusionPlayback.LastSample,c,Size,soundsB.Add);
        check(a.Cards?.OrbitAngle==b.Cards?.OrbitAngle&&a.Cards?.Result==b.Cards?.Result&&
            a.Cards?.DeactivateAfterDraw==true&&a.Effect is null&&b.Effect is null&&
            soundsA.SequenceEqual(new uint[]{0x10000020,0x10000021})&&soundsA.SequenceEqual(soundsB),
            "V11 live sampling invariant across frame batches, sounds once");
    }
}
