using Joey.LookAndFeel;

internal static class NativeOpeningBustChecks
{
    internal static void Run(Action<bool,string> check)
    {
        void RunSample(int variant,int acceptedCounter,int expectedUpdates)
        {
            var playback = new NativeOpeningPlayback(variant);
            int updates=0, requests=0;
            while(playback.Active && updates<1000)
            {
                int before=playback.Counter;
                var request=playback.AdvanceNativeUpdate(before>=acceptedCounter);
                updates++;
                if(request.HasValue)
                {
                    requests++;
                    check(before==128 && request==0x10000025u+(uint)variant,
                        "V31 opening voice requested at input counter 128");
                }
                if(before==acceptedCounter)
                    check(playback.Counter==0x10000,"V31 successful poll jumps without increment");
                if(playback.Counter==0x10100)
                    check(playback.Active,"V31 terminal Update alone cannot clear active");
                playback.CompleteNativeDraw();
            }
            check(updates==expectedUpdates && requests==1 && !playback.Active && playback.Counter==0x10100,
                $"V31 opening reproduces {expectedUpdates} observed Update/Draw pairs");
        }
        RunSample(2,388,162);
        RunSample(1,1148,352);

        const string manifest="start/statobg_?.bmp\nstart/kage.yga 000047\n"+
            "start/reaction16.bmp 000000\nnull.bmp 0\nnull.bmp 0\n"+
            "start/reaction16_kuti1.bmp 206208\nstart/reaction16_kuti2.bmp 206208\n"+
            "start/stato_?_01.bmp 320022\nstart/stato_?_02.bmp 320022\nstart/stato_?_03.bmp 320022\n";
        string[] names=["statobg_e.bmp","kage.yga","reaction16.bmp","reaction16_kuti1.bmp",
            "reaction16_kuti2.bmp","stato_e_01.bmp","stato_e_02.bmp","stato_e_03.bmp"];
        var available=names.Select(n=>NativeJoeyCinematicRouting.Prefix+@"start\"+n);
        var bank=NativeOpeningBank.Parse(manifest,available);
        check(bank.Resources.Count()==8 && NativeOpeningBank.CharacterX(0)==-2016 &&
            NativeOpeningBank.CharacterX(64)==-672 && NativeOpeningBank.CharacterX(80)==-336 &&
            NativeOpeningBank.CharacterX(96)==0 && NativeOpeningBank.CharacterX(128)==0,
            "V31 signed slide and upper clamp");
        check(bank.Sample(128,0).Select(l=>l.Key).SequenceEqual(new[]{0,2}),
            "V31 Draw 128 precedes temporary-surface recomposition");
        for(int variant=0;variant<3;variant++)
        {
            var scene=bank.Sample(132,variant);
            check(scene.Select(l=>l.Key).SequenceEqual(new[]{0,1,2,7+variant}) &&
                scene[1].Position==new CardPoint(0,47) &&
                scene[3].Position==new CardPoint(320,22),
                "V31 opening static passes and variant selection");
        }
        check(bank.Sample(136,0).Length==4,"V31 absent mouth table does not invent frame");
        // Synthetic table only exercises indexing; not native selector evidence.
        int[] synthetic=[5,6,5];
        var mouth=bank.Sample(136,0,synthetic)[^1];
        check(mouth.Key==5 && mouth.Position==new CardPoint(206,208) &&
            bank.Sample(144,0,synthetic)[^1].Key==6 &&
            bank.Sample(0x10000,0,synthetic).Length==4,
            "V31 injected mouth keys and exit suppression");
        check(bank.Resources.Where(r=>new NativeOpeningLayer(r.Key,r.Path,r.Position).UsesCornerColorKey)
            .Select(r=>r.Key).SequenceEqual(new[]{2,5,6,7,8,9}),
            "V31 keying selected by draw layer, not filename");

        // The maintainer's PNG showed exactly RGB(0,255,6), not pure green.
        byte[] green=[0,255,6,255, 0,255,6,128, 0,255,0,255, 7,8,9,255];
        BitmapCornerColorKey.Prepare(green);
        check(green.SequenceEqual(new byte[]{0,255,6,0, 0,255,6,0, 0,255,0,255, 7,8,9,255}),
            "V31 decoded corner key removes matching RGB without changing other colors");
        byte[] other=[141,178,43,255, 0,255,6,255, 141,178,43,255, 12,20,30,123];
        BitmapCornerColorKey.Prepare(other);
        check(other[3]==0 && other[7]==255 && other[11]==0 && other[15]==123,
            "V31 non-green corner and preservation of non-key alpha");

        var clock=new NativeReviewClock();
        check(!clock.Advance(1),"V31 paused review never advances");
        clock.Toggle(30);
        check(!clock.Advance(1.0/60) && clock.Advance(1.0/60),
            "V31 30 FPS consumes one native step per two 60 FPS frames");
        clock.Toggle(60);
        check(clock.Advance(1.0/60) && clock.Advance(1.0/60),
            "V31 speedy consumes one native step per 60 FPS frame");
        check(clock.Advance(1) && !clock.Advance(0),
            "V31 slow host does not skip intermediate samples");
        clock.Toggle(60);
        check(clock.FramesPerSecond==0 && !clock.Advance(1),"V31 toggle pauses");
        bool rejected=false;
        try { NativeOpeningBank.Parse(manifest.Replace("206208","206209"),available); }
        catch(InvalidDataException) { rejected=true; }
        check(rejected,"V31 rejects unexpected opening manifest geometry");
    }
}
