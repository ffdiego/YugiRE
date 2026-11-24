using Joey.Core;

internal static class QueuedRitualSearchChecks
{
    private static Card Monster(ushort id) => new(id,id,"Monster","",(3u<<20)|(4u<<25));
    private static Card Spell(ushort id,int cls=22) => new(id,id,"Spell","",(uint)cls<<20);
    private static Card?[] Slots(Card? card=null) => [card,null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Ritual search did not drain.");
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort id in new ushort[]{1122,1109})
        foreach(bool flip in new[]{false,true})
        foreach(bool accept in new[]{false,true})
        {
            var source=Monster(id);
            var valid=id==1122?Spell(958):Monster(730);
            var wrong=id==1122?Monster(730):Spell(958);
            var fake=id==1122?Spell(958,21):Spell(730);
            Card[] deck=[wrong,valid,fake,valid,Monster(24)];
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,flip?[]:[source],
                    side==0&&flip?Slots(source):Slots(),side==1&&flip?Slots(source):Slots(),8000,
                    side0Deck:side==0?deck:null,side1Deck:side==1?deck:null,effectShuffleSeed:42);
                if(flip)d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
                return d;
            }
            var legacy=State();
            check(flip?legacy.ChangePosition(0,out _):legacy.SummonLeftmost(0,false,out _),"Legacy P165 producer");
            check(accept?legacy.ResolveCardSelection(3,out _):legacy.SkipOptionalCardSelection(out _),"Legacy P165 answer");
            var d=State();var q=d.CreatePrimitiveActionQueue();
            check(flip?d.TryQueueChangePosition(0,out _):d.TryQueueNormalPlacement(0,0,false,out _),
                "Queued P165 producer");
            Drain(q);
            var request=d.PendingCardSelection??throw new InvalidOperationException("Missing P165 request.");
            var options=d.QueuedDeckSearchCandidates(request);
            check(request.Owner==side&&request.MinSelections==0&&options.Select(c=>c.SourceIndex).SequenceEqual(new[]{1,3})&&
                options[0].Target.Occurrence!=options[1].Target.Occurrence,
                "P165 filters by exact ritual ID/class and distinct copies");
            check(!d.TryQueueDeckSearch(request with {},options[1].Target,out _),"P165 stale request rejected");
            int shuffle=0;d.DeckShuffled+=_=>shuffle++;
            check(d.TryQueueDeckSearch(request,accept?options[1].Target:null,out _)&&d.DeckCount(side)==5&&
                d.PendingCardSelection==request,"P165 admission does not mutate");
            Drain(q);
            check(d.PendingCardSelection is null&&d.DeckSnapshot(side).SequenceEqual(legacy.DeckSnapshot(side))&&
                d.HandFor(side).SequenceEqual(legacy.HandFor(side))&&shuffle==(accept?1:0)&&
                q.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Command is MoveToHandCommand)==(accept?1:0)&&
                q.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Command is ShuffleDeckCommand)==(accept?1:0),
                "P165 selected search or decline matches seeded sandbox, no RNG on decline");
            if(accept)check(d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var chosen)&&chosen.Occurrence==options[1].Target.Occurrence,
                "P165 preserves occurrence");
        }
        var mandatory=DemoDuel.FromPartialObservation(0,[Spell(336)],Slots(),Slots(Monster(47)),8000,side1Deck:[Monster(24)]);
        var mq=mandatory.CreatePrimitiveActionQueue();mandatory.TryQueueRaigekiFromHand(0,out _);Drain(mq);
        check(mandatory.PendingCardSelection is {MinSelections:1} required &&
            !mandatory.TryQueueDeckSearch(required,null,out _)&&!mq.IsBusy,
            "P006 remains mandatory");
    }
}
