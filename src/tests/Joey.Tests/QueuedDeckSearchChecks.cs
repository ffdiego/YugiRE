using Joey.Core;

internal static class QueuedDeckSearchChecks
{
    private static Card Monster(ushort id, int atk=1000, int def=800) =>
        new(id,id,$"Monster {id}","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card Spell(ushort id) => new(id,id,"Spell","",22u<<20);
    private static Card?[] Slots(Card? card=null) => [card,null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Search queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not MoveToHandCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort effect in new ushort[]{47,573})
        {
            int other=1-side;
            var low=Monster(24);
            Card[] deck=[low,Monster(25,1900,1000),low,Monster(26,1000,1900),Spell(342),Monster(27,1900,1900)];
            DemoDuel State()=>DemoDuel.FromPartialObservation(other,[Spell(336)],
                side==0?Slots(Monster(effect)):Slots(),side==1?Slots(Monster(effect)):Slots(),8000,
                side0Deck:side==0?deck:[Monster(29)],side1Deck:side==1?deck:[Monster(29)],
                effectShuffleSeed:1979);

            var legacy=State();
            check(legacy.Play(0,out _)&&legacy.PendingCardSelection is not null&&
                legacy.ResolveCardSelection(2,out _),"Legacy search comparator");

            var d=State();var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueRaigekiFromHand(0,out _),"Queued Raigeki creates search");
            Drain(q);
            var request=d.PendingCardSelection ?? throw new InvalidOperationException("Missing search.");
            var candidates=d.QueuedDeckSearchCandidates(request);
            check(candidates.Select(c=>c.SourceIndex).SequenceEqual(effect==47?new[]{0,2,3}:new[]{0,1,2})&&
                candidates.All(c=>c.Target.Side==side&&c.Target.Zone==DuelZone.Deck),
                "Sangan/Witch use their own attack/defense filter and owner deck");
            var first=candidates.Single(c=>c.SourceIndex==0);
            var selected=candidates.Single(c=>c.SourceIndex==2);
            check(first.Target.Occurrence!=selected.Target.Occurrence&&
                !d.TryQueueDeckSearch(request with {},selected.Target,out _)&&
                !d.TryQueueDeckSearch(request,selected.Target with {Side=other},out _),
                "Distinct copies and exact request/owner required");
            try{q.Enqueue(new ShuffleDeckCommand(side));check(false,"Pending choice blocks arbitrary shuffle");}
            catch(InvalidOperationException){check(true,"Pending choice blocks arbitrary shuffle");}
            int shuffles=0, transfers=0;d.DeckShuffled+=p=>{if(p==side)shuffles++;};d.HandTransferred+=_=>transfers++;
            check(d.TryQueueDeckSearch(request,selected.Target,out _)&&d.PendingCardSelection==request&&
                d.DeckCount(side)==6&&d.HandFor(side).Count==0,"Search admission is nonmutating");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.HandFor(side).Count==1&&ReferenceEquals(d.HandFor(side)[0],selected.Card)&&
                d.DeckCount(side)==5&&shuffles==0&&q.IsBusy,"Movement gate precedes shuffle");
            gate.Release=true;Drain(q);
            check(shuffles==1&&transfers==0&&d.DeckSnapshot(side).SequenceEqual(legacy.DeckSnapshot(side))&&
                d.HandFor(side).SequenceEqual(legacy.HandFor(side))&&d.DeckCount(other)==1&&
                d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var recovered)&&
                recovered.Occurrence==selected.Target.Occurrence&&
                !d.TryQueueDeckSearch(request,selected.Target,out _),
                "Same seeded deck order, identity, no draw event, no repeat");
            var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(Array.FindIndex(executed,c=>c is MoveToHandCommand)<Array.FindIndex(executed,c=>c is ShuffleDeckCommand)&&
                executed.Count(c=>c is ShuffleDeckCommand)==1,"One shuffle after search movement");
        }

        var chain=DemoDuel.FromPartialObservation(0,[Spell(335)],Slots(Monster(47)),Slots(Monster(573)),8000,
            side0Deck:[Monster(24)],side1Deck:[Monster(25)]);
        var cq=chain.CreatePrimitiveActionQueue();chain.TryQueueMonsterWipe(DuelZone.Hand,0,out _);Drain(cq);
        var r0=chain.PendingCardSelection!;
        check(r0.Owner==0&&chain.TryQueueDeckSearch(r0,chain.QueuedDeckSearchCandidates(r0).Single().Target,out _),
            "Dark Hole first search");
        Drain(cq);
        var r1=chain.PendingCardSelection!;
        check(r1.Owner==1&&chain.TryQueueDeckSearch(r1,chain.QueuedDeckSearchCandidates(r1).Single().Target,out _),
            "Second search opens after first movement and shuffle");
        Drain(cq);
        check(chain.PendingCardSelection is null&&chain.HandFor(0).Single().Id==24&&
            chain.HandFor(1).Single().Id==25&&chain.DeckCount(0)==0&&chain.DeckCount(1)==0&&!chain.GameOver,
            "Searches can take the last card without deck-out");

        var primitive=DemoDuel.FromPartialObservation(0,[],Slots(),Slots(),8000,
            side0Deck:[Monster(24),Monster(24),Monster(25)]);
        var pq=primitive.CreatePrimitiveActionQueue();
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,0,out var a);
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,1,out var b);
        pq.EnqueueBatch([new MoveToHandCommand(0,a),new MoveToHandCommand(0,a),new MoveToHandCommand(0,b)]);
        Drain(pq);
        check(primitive.Hand.Count==2&&primitive.DeckSnapshot(0).Single().Id==25&&
            pq.Trace.Any(t=>t.Stage==ActionTraceStage.Executed&&t.Result?.Outcome==CommandOutcome.Skipped),
            "Primitive deck movement resolves shifted copies and skips stale identity");
    }
}
