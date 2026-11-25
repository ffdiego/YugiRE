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

        foreach (int side in new[]{0,1})
        foreach (bool set in new[]{false,true})
        {
            Card[] deck=[Spell(1003),Spell(1034),Spell(1003),Spell(1547),Monster(1003)];
            var d=DemoDuel.FromPartialObservation(side,[Spell(1040)],Slots(),Slots(),8000,
                side0Deck:side==0?deck:[Monster(24)],side1Deck:side==1?deck:[Monster(24)]);
            var gate=new Gate();
            var q=d.CreatePrimitiveActionQueue(gate);
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Fusion Sage Set");Drain(q);}
            check(!(set?d.ActivateSetSpell(0,out _):d.Play(0,out _)),"No legacy activation bypass");
            check(d.TryQueueActivation(0,set,out _)&&d.PendingCardSelection is null&&
                d.Graveyard(side).Count==0,"Fusion Sage admission does not spend or search");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==1040&&d.PendingCardSelection is null,
                "Spend exact spell before opening the search");
            Drain(q);
            var request=d.PendingCardSelection ?? throw new InvalidOperationException("Missing Fusion Sage search.");
            var candidates=d.QueuedDeckSearchCandidates(request);
            check(request.Owner==side&&request.Filter==CardSelectionFilter.Polymerization&&
                request.MinSelections==1&&candidates.Select(c=>c.SourceIndex).SequenceEqual(new[]{0,1,2}),
                "Both Polymerization IDs, neither Fusion Gate nor wrong class");
            var chosen=candidates[2];
            check(!d.ResolveCardSelection(chosen.SourceIndex,out _)&&
                !d.SkipOptionalCardSelection(out _)&&!d.TryQueueDeckSearch(request,null,out _)&&
                !d.TryQueueDeckSearch(request,chosen.Target with {Side=1-side},out _),
                "Mandatory exact-owner queued response only");
            int shuffled=0,drawn=0;
            d.DeckShuffled+=p=>{if(p==side)shuffled++;};
            d.HandTransferred+=_=>drawn++;
            check(d.UsesQueuedHandSelection(request)&&!d.TrySubmitQueuedHandSelection(request with {},chosen.SourceIndex,out _)&&
                !d.TrySubmitQueuedHandSelection(request,null,out _),"UI adapter rejects stale request and mandatory decline");
            check(d.TrySubmitQueuedHandSelection(request,chosen.SourceIndex,out _)&&d.PendingCardSelection==request&&
                d.HandFor(side).Count==0&&!d.TrySubmitQueuedHandSelection(request,chosen.SourceIndex,out _),
                "UI adapter admits duplicate occurrence without mutation or repeated input");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.HandFor(side).Count==1&&shuffled==0&&q.IsBusy,"Gate before shuffle");
            gate.Release=true;Drain(q);
            check(d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var received)&&
                received.Occurrence==chosen.Target.Occurrence&&shuffled==1&&drawn==0&&
                d.PendingCardSelection is null&&d.DeckCount(side)==4&&d.DeckCount(1-side)==1&&
                !d.TryQueueDeckSearch(request,chosen.Target,out _),"Fusion Sage transfer once, shuffle once, no draw");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1040)],Slots(),Slots(),8000,
                side0Deck:[Spell(1547),Monster(1003)]);
            var q=d.CreatePrimitiveActionQueue();
            check(!d.TryQueueActivation(0,false,out _)&&!q.IsBusy&&d.Hand.Count==1,
                "No matching Spell: activation rejected");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1040)],Slots(),Slots(),8000,
                side0Deck:[Spell(1003)]);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueSpellTrapSet(0,out _),"Stale Sage Set");Drain(q);
            check(d.TryQueueActivation(0,true,out _),"Stale Sage admitted");
            q.Tick();
            d.Spells[0,0]=Spell(1040);
            Drain(q);
            check(d.PendingCardSelection is null&&d.Graveyard(0).Count==0&&d.DeckCount(0)==1,
                "Failed source spend must not open a search");
        }

        foreach(int side in new[]{0,1})
        {
            var d=DemoDuel.FromPartialObservation(side,[Spell(1040)],Slots(),Slots(),8000,
                side0Deck:side==0?[Spell(1003),Spell(1003)]:[Monster(24)],
                side1Deck:side==1?[Spell(1003),Spell(1003)]:[Monster(24)]);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueActivation(0,false,out _),"CPU selection setup");Drain(q);
            var request=d.PendingCardSelection!;
            var chosen=d.QueuedDeckSearchCandidates(request)[0];
            var cpu=new CpuDuelController(side);
            var step=cpu.Step(d,queueTurnActions:true);
            check(step.Status==CpuStepStatus.Acted&&step.Action==CpuAction.Selection&&q.IsBusy&&
                d.PendingCardSelection==request&&d.HandFor(side).Count==0,
                "CPU admits search response without immediate movement");
            Drain(q);
            check(d.PendingCardSelection is null&&d.HandFor(side).Count==1&&
                d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var received)&&received.Occurrence==chosen.Target.Occurrence&&
                q.Trace.Any(t=>t.Entry.Origin==ActionOrigin.Cpu&&t.Stage==ActionTraceStage.Executed),
                "CPU answer is queued, exact occurrence and origin preserved");
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
