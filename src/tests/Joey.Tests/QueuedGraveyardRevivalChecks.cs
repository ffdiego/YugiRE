using Joey.Core;

internal static class QueuedGraveyardRevivalChecks
{
    private static Card Monster(ushort id,int atk=1000,int def=500) =>
        new(id,id,$"Monster {id}","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card Spell(ushort id) => new(id,id,"Spell","",22u<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Revival queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not SpecialSummonCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int owner in new[]{0,1})
        foreach(bool set in new[]{false,true})
        {
            int enemy=1-owner;
            var spear=Monster(1116,500);var ally=Monster(24);var other=Monster(25);
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(336)],
                owner==0?Slots(spear):Slots(other),owner==1?Slots(spear):Slots(other),8000,
                side0Graveyard:owner==0?[ally,Spell(342),spear]:[other],
                side1Graveyard:owner==1?[ally,Spell(342),spear]:[other]);
            d.MonsterFaceDown[owner,0]=d.DefensePosition[owner,0]=true;
            check(d.RevealMonsterByEffect(owner,0,out _)&&d.MonsterWasFlipped[owner,0]&&
                d.PendingCardSelection is null,"Spear Cretin reveal alone never revives");
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            d.TryCaptureCardTarget(owner,DuelZone.Monster,0,out var triggering);
            check(d.TryQueueRaigekiFromHand(0,out _),"Queue destroys flipped Spear Cretin");
            Drain(q);
            var first=d.PendingCardSelection??throw new InvalidOperationException("Missing revival request.");
            var candidates=d.QueuedGraveyardRevivalCandidates(first);
            check(first.Owner==owner&&candidates.Select(c=>c.SourceIndex).SequenceEqual(new[]{0,2})&&
                candidates.All(c=>c.Target.Occurrence!=triggering.Occurrence)&&
                candidates[1].Card.Id==1116,"Exact triggering occurrence excluded, another Spear eligible");
            var pick=candidates[1];
            d.TryCaptureCardTarget(owner,DuelZone.Graveyard,3,out var sourceInGrave);
            d.TryCaptureCardTarget(enemy,DuelZone.Graveyard,0,out var opposing);
            d.TryCaptureCardTarget(owner,DuelZone.Graveyard,1,out var spell);
            check(sourceInGrave.Occurrence==triggering.Occurrence&&
                !d.TryQueueGraveyardRevival(first,sourceInGrave,0,set,out _)&&
                !d.TryQueueGraveyardRevival(first,opposing,0,set,out _)&&
                !d.TryQueueGraveyardRevival(first,spell,0,set,out _)&&
                !d.TryQueueGraveyardRevival(first,pick.Target,5,set,out _)&&
                !d.TryQueueGraveyardRevival(first with {},pick.Target,0,set,out _),
                "Reject self, opponent GY, spell, invalid zone and obsolete request");
            try{q.Enqueue(new DrawCommand(enemy));check(false,"Pending revival blocks unrelated commands");}
            catch(InvalidOperationException){check(true,"Pending revival blocks unrelated commands");}
            int shuffles=0;var summons=new List<DemoSpecialSummon>();
            d.DeckShuffled+=_=>shuffles++;d.MonsterSpecialSummoned+=summons.Add;
            check(d.TryQueueGraveyardRevival(first,pick.Target,4,set,out _)&&
                d.PendingCardSelection==first&&d.Graveyard(owner).Count==4&&d.Monsters[owner,4] is null,
                "Revival admission leaves request and card unchanged");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(ReferenceEquals(d.Monsters[owner,4],pick.Card)&&
                d.Graveyard(owner).Count==3&&d.PendingCardSelection is null&&
                summons.Count==1&&q.IsBusy,"First revival waits at gate before other player's request");
            gate.Release=true;Drain(q);
            var second=d.PendingCardSelection??throw new InvalidOperationException("Missing second revival.");
            var candidate=d.QueuedGraveyardRevivalCandidates(second).Single();
            check(second.Owner==enemy&&candidate.Target==opposing&&
                !d.TryQueueGraveyardRevival(second,candidate.Target,0,!set,out _)&&
                d.TryQueueGraveyardRevival(second,candidate.Target,1,!set,out _),
                "Other player chooses separately, occupied slot rejected");
            Drain(q);
            check(d.PendingCardSelection is null&&summons.Count==2&&shuffles==0&&
                d.MonsterFaceDown[owner,4]==set&&d.DefensePosition[owner,4]==set&&
                d.MonsterFaceDown[enemy,1]==!set&&d.DefensePosition[enemy,1]==!set&&
                !d.MonsterWasFlipped[owner,4]&&
                d.TryCaptureCardTarget(owner,DuelZone.Monster,4,out var revived)&&revived.Occurrence==pick.Target.Occurrence&&
                d.Graveyard(owner).Any(c=>ReferenceEquals(c,first.EligibleCards[0].Card))&&
                d.Graveyard(enemy).Count==1,
                "Both revivals preserve identity and position, no deck shuffle");
        }

        var battle=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1116,500)),
            Slots(Monster(800,2200)),8000,side0Graveyard:[Monster(24)],side1Graveyard:[Monster(25)]);
        battle.MonsterFaceDown[0,0]=battle.DefensePosition[0,0]=true;
        check(battle.BeginBattle(out _)&&battle.Attack(0,0,out _)&&battle.Monsters[0,0] is null,
            "Spear Cretin battle producer has finished destruction");
        var bq=battle.CreatePrimitiveActionQueue();
        var req=battle.PendingCardSelection!;
        check(battle.TryQueueGraveyardRevival(req,battle.QueuedGraveyardRevivalCandidates(req).Single().Target,0,false,out _),
            "Completed legacy battle admits revival");
        Drain(bq);
        check(battle.PendingCardSelection is {Owner:1}&&battle.Monsters[0,0]?.Id==24,
            "Second request after battle revival");

        var primitive=DemoDuel.FromPartialObservation(0,[],Slots(),Slots(),8000,
            side0Graveyard:[Monster(24),Monster(24),Spell(342)]);
        var pq=primitive.CreatePrimitiveActionQueue();
        primitive.TryCaptureCardTarget(0,DuelZone.Graveyard,0,out var a);
        primitive.TryCaptureCardTarget(0,DuelZone.Graveyard,1,out var b);
        primitive.TryCaptureCardTarget(0,DuelZone.Graveyard,2,out var c);
        pq.EnqueueBatch([new SpecialSummonCommand(0,a,0),new SpecialSummonCommand(0,a,1),
            new SpecialSummonCommand(0,c,2),new SpecialSummonCommand(0,b,0),
            new SpecialSummonCommand(0,b,1,true)]);
        Drain(pq);
        check(primitive.Monsters[0,0]?.Id==24&&primitive.Monsters[0,1]?.Id==24&&
            primitive.MonsterFaceDown[0,1]&&primitive.Monsters[0,2] is null&&
            primitive.Graveyard(0).Single().Id==342&&
            pq.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Result?.Outcome==CommandOutcome.Applied)==2,
            "GY primitive rejects stale occurrence, nonmonster and occupied slot without consuming");
    }
}
