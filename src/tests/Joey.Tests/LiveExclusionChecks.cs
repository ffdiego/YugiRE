using Joey.Core;
using Joey.LookAndFeel;

internal static class LiveExclusionChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(NativeExclusionDestination.Center(true)==new CardPoint(762,308) &&
              NativeExclusionDestination.Center(false)==new CardPoint(292,292),
            "V26: region 0F native destination pair");
        int[] cosine=NativeCosineCapture.GenerateTable();
        foreach(int side in new[]{0,1})
        {
            var template=new Card(77,77,"Warrior","",(15u<<20)|(2u<<29)|(4u<<25)|(100u<<9)|100u);
            var d=DemoDuel.FromPartialObservation(side,[template],new Card?[5],new Card?[5],8000,
                side0Deck:side==0?[template]:[],side1Deck:side==1?[template]:[],
                side0Graveyard:side==0?[template]:[],side1Graveyard:side==1?[template]:[]);
            var events=new List<DemoBanishment>();
            d.CardBanished+=events.Add;
            var q=d.CreatePrimitiveActionQueue();
            d.TryCaptureCardTarget(side,DuelZone.Deck,0,out var deck);
            d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var hand);
            d.TryCaptureCardTarget(side,DuelZone.Graveyard,0,out var grave);
            q.EnqueueBatch([new BanishCommand(side,deck),new BanishCommand(side,deck),
                new BanishCommand(side,hand),new BanishCommand(side,grave)]);
            check(events.Count==0,"V26: no event before execution");
            for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
            check(!q.IsBusy && d.Banished(side).Count==3 && events.Count==3 &&
                events[0]==new DemoBanishment(side,DuelZone.Deck,null) &&
                events[1]==new DemoBanishment(side,DuelZone.Hand,null) &&
                events[2].Side==side && events[2].Source==DuelZone.Graveyard &&
                events[2].VisibleCard==template,
                "V26: applied only, public grave source, masked private sources");
            foreach(var e in events)
            {
                var start=NativeExclusionDestination.Start(e.Side,e.VisibleCard is not null,77);
                check((start.Descriptor&0x3f)==(0x1e|side) &&
                    start.VisibleCardArgument==(e.VisibleCard is null?0u:77u),"V26: destination and masked argument");
                var timeline=new NativeExclusionTimeline(start);
                int draws=0;
                while(timeline.Active)
                {
                    var frame=timeline.Draw(NativeExclusionDestination.Center(side==0),50,72,0,cosine,
                        NativeExclusionResources.ExpectedSize);
                    check(frame.HasValue && !timeline.BlocksRelease,"V26: visual lifetime never gates queue");
                    draws++;
                }
                check(draws==24 && !timeline.Active,"V26: terminal 24 Draws");
            }
        }
    }
}
