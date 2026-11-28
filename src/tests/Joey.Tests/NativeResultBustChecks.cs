using Joey.LookAndFeel;

internal static class NativeResultBustChecks
{
    internal static void Run(Action<bool,string> check)
    {
        void Count(int poll,int expected)
        {
            var p=new NativeResultPlayback(2,1);
            int steps=0,requests=0;
            while(p.Active && steps<1000)
            {
                int before=p.Counter;
                if(p.AdvanceNativeUpdate(before>=poll) is uint sound)
                {
                    requests++;
                    check(before==96 && sound==0x1000005a,"V31 result voice input and variant");
                }
                if(before==poll)check(p.Counter==0x10004,"V31 result poll increments on successful branch");
                if(p.Counter==0x10080)check(p.Active,"V31 result terminal requires Draw");
                p.CompleteNativeDraw();steps++;
            }
            check(steps==expected && requests==1 && !p.Active,"V31 observed result Update/Draw count "+expected);
        }
        Count(484,153);Count(1504,408);
        const string lose="win/make_bg#.bmp 1003\nwin/make_kage.yga 438047\nwin/make_?_moji.yga 3\n"+
            "win/reaction14.bmp 401043\nwin/reaction14_eyes2.bmp 120072\nwin/reaction14_eyes1.bmp 120072\n"+
            "win/reaction14_kuti2.bmp 152117\nwin/reaction14_kuti1.bmp 152117\n"+
            "win/fukidashi/?_make01.bmp 043039\nwin/fukidashi/?_make02.bmp 043039\nwin/fukidashi/?_make03.bmp 043039\n";
        var names=lose.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(l=>
            NativeJoeyCinematicRouting.Prefix+l.Split(' ')[0].Replace('?','e').Replace('/','\\'));
        var bank=NativeResultBank.Parse(2,lose,names);
        var c=NativeCosineCapture.GenerateTable();
        check(bank.VariantCount==3 && bank.CharacterX(64)==800 &&
            bank.CharacterX(80)==601 && bank.CharacterX(96)==401,
            "V31 right-hand result sprite slide");
        check(bank.Sample(60,1,c).Select(l=>l.Key).SequenceEqual(new[]{0}) &&
            bank.Sample(64,1,c).Select(l=>l.Key).SequenceEqual(new[]{0,3}) &&
            bank.Sample(96,1,c).Select(l=>l.Key).SequenceEqual(new[]{0,3}) &&
            bank.Sample(100,1,c).Select(l=>l.Key).SequenceEqual(new[]{0,1,9,3,2}),
            "V31 result background, recomposition and banner gates");
        check(bank.BannerPosition(96,c)==new CardPoint(-341,320) &&
            bank.BannerPosition(160,c)==new CardPoint(0,320),
            "V31 result banner bottom-left cosine motion");
        check(Enumerable.Range(0,4).Select(i=>NativeResultBank.FaceOffset(i*8))
            .SequenceEqual(new[]{-1,0,1,0}),"V31 captured result facial table");
        var mouth=bank.Sample(104,1,c).Single(l=>l.Key==6);
        check(mouth.Position==new CardPoint(553,160) &&
            bank.Sample(112,1,c).Any(l=>l.Key==7) &&
            bank.Sample(120,1,c).Any(l=>l.Key==6) &&
            !bank.Sample(128,1,c).Any(l=>l.Key is 6 or 7),
            "V31 mouth position, selector and phase zero");
        var eye=bank.Sample(264,1,c).Single(l=>l.Key==4);
        check(eye.Position==new CardPoint(521,115) &&
            bank.Sample(272,1,c).Any(l=>l.Key==5) &&
            !bank.Sample(104,1,c).Any(l=>l.Key is 4 or 5) &&
            bank.Sample(0x10008,1,c).Any(l=>l.Key==4) &&
            !bank.Sample(0x10008,1,c).Any(l=>l.Key is 6 or 7),
            "V31 eye blink gate and mouth exit gate are independent");
        for(uint request=0x10000059;request<=0x1000005f;request++)
            check(NativeResultPlayback.VoicePath(request)==DuelSoundAssets.InternalPrefix+
                $"joey_{53+request-0x10000059u:00}.wav","V31 exact result voice descriptor");
        check(new NativeResultPlayback(1,2).VoiceRequest==0x1000005f &&
            new NativeResultPlayback(3,0).VoiceRequest==0x1000005c,
            "V31 win/draw raw request arithmetic");
    }
}
