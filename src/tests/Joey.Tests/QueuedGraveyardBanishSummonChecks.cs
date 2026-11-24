using Joey.Core;

internal static class QueuedGraveyardBanishSummonChecks
{
    private static Card Monster(ushort id, int attr=1,int type=3,int category=0) =>
        new(id,id,$"Monster {id}","",((uint)type<<20)|(4u<<25)|((uint)attr<<29)|((uint)category<<18)|(100u<<9)|80u);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Inherent summon did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>c.Command is not BanishCommand||Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(var (id,attr,count) in new (ushort,int,int)[]{(1514,2,3),(1515,1,2),(1516,4,1),(1517,3,1),(1518,5,1),(1519,6,1)})
        foreach(int side in new[]{0,1})
        foreach(bool defense in new[]{false,true})
        {
            var source=Monster(id,attr,3,1);
            var valid=Monster(24,attr,3);
            var wrong=Monster(25,attr==6?1:attr+1,15);
            var spell=new Card(342,342,"Spell","",(22u<<20)|((uint)attr<<29));
            var normal=Monster(26);
            Card[] grave=[valid,wrong,valid,spell,valid];
            var d=DemoDuel.FromPartialObservation(side,[source,normal],Slots(),Slots(),8000,
                side0Graveyard:side==0?grave:[valid],side1Graveyard:side==1?grave:[valid]);
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var hand);
            var options=d.QueuedGraveyardBanishSummonCandidates(hand);
            check(GraveyardBanishSummonRules.QueuedRequiredCount(source)==count&&
                options.Select(c=>c.SourceIndex).SequenceEqual(new[]{0,2,4})&&
                !d.CanSummonFromHand(0)&&!d.CanSetFromHand(0),"Native count and type/attribute filter");
            var selected=options.Reverse().Take(count).Select(c=>c.Target).ToArray();
            d.TryCaptureCardTarget(side,DuelZone.Graveyard,1,out var wrongTarget);
            d.TryCaptureCardTarget(1-side,DuelZone.Graveyard,0,out var opposite);
            check(!d.TryQueueGraveyardBanishSummon(hand,2,selected.Take(count-1).ToArray(),defense,out _)&&
                !d.TryQueueGraveyardBanishSummon(hand,5,selected,defense,out _)&&
                !d.TryQueueGraveyardBanishSummon(hand,2,[wrongTarget,..selected.Skip(1)],defense,out _)&&
                !d.TryQueueGraveyardBanishSummon(hand,2,[opposite,..selected.Skip(1)],defense,out _)&&
                (count==1||!d.TryQueueGraveyardBanishSummon(hand,2,Enumerable.Repeat(selected[0],count).ToArray(),defense,out _)),
                "Wrong/insufficient/duplicate cost and slot reject");
            int summons=0;d.MonsterSpecialSummoned+=_=>summons++;
            check(d.TryQueueGraveyardBanishSummon(hand,2,selected,defense,out _)&&
                d.Hand.Count==2&&d.Graveyard(side).Count==5&&d.Banished(side).Count==0,
                "Admission nonmutating");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.Banished(side).Count==1&&d.Monsters[side,2] is null&&d.Hand.Count==2&&q.IsBusy,
                "First cost waits for presentation, no early summon");
            gate.Release=true;Drain(q);
            check(d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var placed)&&placed.Occurrence==hand.Occurrence&&
                !d.MonsterFaceDown[side,2]&&d.DefensePosition[side,2]==defense&&summons==1&&
                d.Banished(side).Count==count&&d.Graveyard(side).Count==5-count&&d.Graveyard(1-side).Count==1&&
                d.Hand.Single()==normal&&d.CanSummonFromHand(0),
                "Paid summon preserves occurrence, face-up position and Normal Summon allowance");
            for(int i=0;i<count;i++)check(d.TryCaptureCardTarget(side,DuelZone.Banished,i,out var removed)&&
                removed.Occurrence==selected[i].Occurrence,"Cost order and physical identity");
            var commands=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(commands.Count(c=>c is BanishCommand)==count&&commands.Count(c=>c is SpecialSummonCommand)==1&&
                Array.FindLastIndex(commands,c=>c is BanishCommand)<Array.FindIndex(commands,c=>c is SpecialSummonCommand),
                "All costs before the summon");
        }

        var direct=DemoDuel.FromPartialObservation(0,[Monster(1514,2,3,1)],Slots(),Slots(),8000,
            side0Graveyard:[Monster(24),Monster(24),Monster(24)]);
        var dq=direct.CreatePrimitiveActionQueue();
        direct.TryCaptureCardTarget(0,DuelZone.Hand,0,out var src);
        dq.Enqueue(new SpecialSummonCommand(0,src,0));Drain(dq);
        check(direct.Hand.Count==1&&direct.Monsters[0,0] is null&&direct.Banished(0).Count==0&&
            dq.Trace.Last().Result?.Outcome==CommandOutcome.Rejected,"Cannot bypass inherent cost by bare Hand summon");
        var opts=direct.QueuedGraveyardBanishSummonCandidates(src);
        check(direct.TryQueueGraveyardBanishSummon(src,0,opts.Select(c=>c.Target).ToArray(),false,out _),"Valid cost admitted");
        direct.Monsters[0,0]=Monster(27);
        Drain(dq);
        check(direct.Hand.Count==1&&direct.Banished(0).Count==0&&direct.Graveyard(0).Count==3,
            "Destination changed before execution rejects entire cost");

        var after=DemoDuel.FromPartialObservation(0,[Monster(26),Monster(1517,3,14,1)],Slots(),Slots(),8000,
            side0Graveyard:[Monster(24,3)]);
        check(after.SummonLeftmost(0,false,out _),"Normal Summon fixture");
        var aq=after.CreatePrimitiveActionQueue();
        after.TryCaptureCardTarget(0,DuelZone.Hand,0,out var aqua);
        check(after.TryQueueGraveyardBanishSummon(aqua,1,[after.QueuedGraveyardBanishSummonCandidates(aqua).Single().Target],
            true,out _),"Special Summon after Normal Summon");
        Drain(aq);
        check(after.Monsters[0,1]?.Id==1517&&after.DefensePosition[0,1]&&after.Hand.Count==0,"Face-up Defense supported");
    }
}
