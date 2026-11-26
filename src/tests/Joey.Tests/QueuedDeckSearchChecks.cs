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

    private static void CpuFusionSage(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool set in new[] { false, true })
        {
            Card[] deck = [Monster(24), Spell(1003), Monster(25)];
            var duel = DemoDuel.FromPartialObservation(side, [Spell(DeckSearchSpellRules.FusionSage)],
                Slots(), Slots(), 8000, side0Deck: side == 0 ? deck : null, side1Deck: side == 1 ? deck : null);
            if (set) check(duel.SetFromHand(0, out _), "CPU Fusion Sage Set fixture");
            check(duel.Draw(out _), "CPU Fusion Sage Draw fixture");
            var queue = duel.CreatePrimitiveActionQueue();
            var cpu = new CpuDuelController(side);
            check(cpu.Step(duel, queueTurnActions: true) is { Status: CpuStepStatus.Acted } &&
                queue.IsBusy && duel.PendingCardSelection is null && duel.Graveyard(side).Count == 0,
                "CPU submits Fusion Sage without immediate spend");
            var entry = queue.Trace.Single().Entry;
            var presentation = duel.DescribeActionPresentation(entry.Command);
            check(entry.Origin == ActionOrigin.Cpu && presentation is { Kind: ActionPresentationKind.Activate } &&
                presentation.Source.Zone == (set ? DuelZone.SpellTrap : DuelZone.Hand) &&
                presentation.DestinationSlot == 0,
                "App gate reads correct hand/Set origin for the admitted Fusion Sage activation");
            Drain(queue);
            check(duel.PendingCardSelection is { EffectResourceId: DeckSearchSpellRules.FusionSage } &&
                cpu.Step(duel, queueTurnActions: true) is { Status: CpuStepStatus.Acted } &&
                queue.IsBusy && duel.HandFor(side).Count == 1,
                "CPU responds to the mandatory Polymerization choice through the queue");
            Drain(queue);
            check(duel.PendingCardSelection is null && duel.HandFor(side).Any(c => c.Id == 1003) &&
                duel.Graveyard(side).Single().Id == DeckSearchSpellRules.FusionSage,
                "CPU activation and selection compose end to end.");
        }
    }

    private static void CheckBattleSearchOrder(Action<bool, string> check)
    {
        var sangan = Monster(47);
        var witch = Monster(573);
        var lowAttack = Monster(24, 1200, 1900);
        var lowDefense = Monster(25, 1900, 1200);
        Card[] deck = [lowAttack, lowDefense];
        foreach (int side in new[] { 0, 1 })
        {
            int defender = 1 - side;
            var duel = DemoDuel.FromPartialObservation(defender, [],
                side == 0 ? Slots(sangan) : Slots(witch), side == 1 ? Slots(sangan) : Slots(witch), 8000,
                side0Deck: deck, side1Deck: deck);
            var gate = new Gate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(duel.EndTurn(out _) && duel.BeginBattle(out _) && console.Execute("attack 0 0").Enqueued,
                "P006 admits a battle between the two searchers");
            Drain(queue);
            check(duel.Graveyard(side).SequenceEqual([sangan]) && duel.Graveyard(defender).SequenceEqual([witch]) &&
                duel.PendingCardSelection is { EffectResourceId: 47, MinSelections: 1 } first &&
                CpuDuelController.PendingOwner(duel) == side && duel.UsesQueuedHandSelection(first) &&
                duel.QueuedDeckSearchCandidates(first).Select(c => c.SourceIndex).SequenceEqual([0]) &&
                !duel.ResolveCardSelection(0, out _) && !console.Execute("select-hand decline").Enqueued &&
                console.Execute("select-hand 0").Enqueued,
                "Sangan's mandatory ATK search is offered first, after both removals");
            gate.Release = false;
            for (int tick = 0; queue.IsBusy && queue.Active?.Command is not MoveToHandCommand && tick < 20; tick++)
                queue.Tick();
            check(queue.Active?.Command is MoveToHandCommand && duel.HandFor(side).SequenceEqual([lowAttack]) &&
                duel.HandFor(defender).Count == 0 && duel.PendingCardSelection is null &&
                !queue.Trace.Any(entry => entry.Entry.Command is ShuffleDeckCommand),
                "Sangan's transfer gate blocks its shuffle and Witch's search");
            gate.Release = true;
            Drain(queue);
            check(duel.PendingCardSelection is { EffectResourceId: 573, MinSelections: 1 } second &&
                CpuDuelController.PendingOwner(duel) == defender &&
                duel.QueuedDeckSearchCandidates(second).Select(c => c.SourceIndex).SequenceEqual([1]) &&
                new CpuDuelController(defender).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Selection },
                "Witch follows after Sangan's shuffle and the CPU uses her DEF search");
            Drain(queue);
            check(duel.PendingCardSelection is null && duel.HandFor(defender).SequenceEqual([lowDefense]) &&
                queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is ShuffleDeckCommand) == 2 &&
                duel.Life.SequenceEqual([8000, 8000]), "Each search transfers and shuffles once");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        CpuFusionSage(check);
        CheckBattleSearchOrder(check);
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
            var sourceZone = set ? DuelZone.SpellTrap : DuelZone.Hand;
            check(d.CanQueueDeckSearchSpell(sourceZone, 0), "Fusion Sage UI enables the correct zone");
            check(d.TryQueueActivation(0,set,out _)&&!d.CanQueueDeckSearchSpell(sourceZone, 0)&&d.PendingCardSelection is null&&
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
            check(!d.CanQueueDeckSearchSpell(DuelZone.Hand, 0)&&!d.TryQueueActivation(0,false,out _)&&!q.IsBusy&&d.Hand.Count==1,
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
