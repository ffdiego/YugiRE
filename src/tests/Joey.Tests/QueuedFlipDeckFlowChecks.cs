using Joey.Core;

internal static class QueuedFlipDeckFlowChecks
{
    private static Card Monster(ushort id) => new(id,id,"Monster","",(3u<<20)|(4u<<25));
    private static Card Spell(ushort id,int cls=22) => new(id,id,"Spell/Trap","",(uint)cls<<20);
    private static Card?[] Slots(Card? card=null) => [card,null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("FLIP flow did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}=true;
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not (DrawCommand or DiscardCommand or SendToGraveyardCommand) || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort effect in new ushort[]{561,610})
        foreach(int size in new[]{0,1,2,3,4,5,6})
        {
            int enemy=1-side;
            bool mill=effect==561;
            var oldSpell=Spell(342);
            Card[] deck=[oldSpell,Spell(1056,21),Spell(343),Monster(24),Monster(25),Spell(344)];
            Card[] initial=[oldSpell,Monster(27)];
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(enemy,initial,
                    side==0?Slots(Monster(effect)):Slots(),side==1?Slots(Monster(effect)):Slots(),8000,
                    side0Deck:side==0?[Monster(30)]:deck.Take(size).ToArray(),
                    side1Deck:side==1?[Monster(30)]:deck.Take(size).ToArray());
                d.NextTurn();
                d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
                return d;
            }
            var legacy=State();
            check(legacy.ChangePosition(0,out _),"Legacy deck flow comparator");
            var d=State();var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            var existing=d.HandFor(enemy).ToArray();
            var captured=new List<CardTarget>();
            for(int i=0;i<Math.Min(size,mill?5:3);i++)
            {
                if(!d.TryCaptureCardTarget(enemy,DuelZone.Deck,i,out var t))
                    throw new InvalidOperationException("Missing deck identity.");
                captured.Add(t);
            }
            int draws=0,discards=0;
            d.HandTransferred+=e=>{if(e.FromDeck)draws++;else discards++;};
            check(d.TryQueueChangePosition(0,out _)&&d.MonsterFaceDown[side,0]&&
                d.DeckCount(enemy)==size&&d.LastDeckFlow is null,"FLIP admission nonmutating");
            q.Tick();q.Tick();
            gate.Release=false;
            q.Tick();q.Tick();q.Tick();
            if(size>0)
            {
                check(d.DeckCount(enemy)==size-1&&d.LastDeckFlow is null&&q.IsBusy,
                    "First movement waits for its own gate");
                int hand=d.HandFor(enemy).Count,grave=d.Graveyard(enemy).Count;
                q.Tick();
                check(d.HandFor(enemy).Count==hand&&d.Graveyard(enemy).Count==grave,
                    "Blocked gate cannot repeat movement");
            }
            gate.Release=true;Drain(q);
            int moved=Math.Min(size,mill?5:3);
            int sent=mill?moved:Enumerable.Range(0,moved).Count(i=>deck[i].ClassCode==22);
            check(d.LastDeckFlow==legacy.LastDeckFlow&&d.HandFor(enemy).SequenceEqual(legacy.HandFor(enemy))&&
                d.Graveyard(enemy).SequenceEqual(legacy.Graveyard(enemy))&&
                d.DeckSnapshot(enemy).SequenceEqual(legacy.DeckSnapshot(enemy))&&
                d.Life.SequenceEqual(legacy.Life)&&d.Winner==legacy.Winner&&d.GameOver==legacy.GameOver&&
                d.DeckCount(side)==1&&ReferenceEquals(d.HandFor(enemy)[0],existing[0]),
                "Deck flow preserves final state, audit, previous hand and deck-out policy");
            var commands=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(commands.Count(c=>c is SendToGraveyardCommand)==(mill?moved:0)&&
                commands.Count(c=>c is DrawCommand)==(mill?0:moved)&&
                commands.Count(c=>c is DiscardCommand)==(mill?0:sent)&&
                draws==(mill?0:moved)&&discards==(mill?0:sent),
                "One logical command per movement; mill does not emit hand events");
            if(!mill&&sent>0)
            {
                int lastDraw=Array.FindLastIndex(commands,c=>c is DrawCommand);
                int firstDiscard=Array.FindIndex(commands,c=>c is DiscardCommand);
                check(lastDraw<firstDiscard,"Scout draws all available cards before discarding");
            }
            if(mill&&moved>0)
            {
                check(d.TryCaptureCardTarget(enemy,DuelZone.Graveyard,0,out var first)&&
                    first.Occurrence==captured[0].Occurrence&&!d.GameOver,"Mill preserves top identity, empty deck not loss");
            }
        }
        // Legacy P088 checks Exodia only after all available draws and Spell
        // discards. Completing Exodia wins even if the forced draw is short.
        Card[] pieces=ExodiaRules.RequiredNames.Select((name,i)=>
            Monster((ushort)(3100+i)) with {Name=name}).ToArray();
        DemoDuel ExodiaState()
        {
            var d=DemoDuel.FromPartialObservation(1,pieces.Take(4).ToArray(),
                Slots(Monster(610)),Slots(),8000,side1Deck:[pieces[4],Spell(342)]);
            d.NextTurn();
            d.MonsterFaceDown[0,0]=d.DefensePosition[0,0]=true;
            return d;
        }
        var exLegacy=ExodiaState();exLegacy.ChangePosition(0,out _);
        var ex=ExodiaState();var exGate=new Gate();var exq=ex.CreatePrimitiveActionQueue(exGate);
        int wins=0;ex.ExodiaCompleted+=_=>wins++;
        check(ex.TryQueueChangePosition(0,out _),"Scout Exodia fixture");
        exq.Tick();exq.Tick();exq.Tick();
        exGate.Release=false;exq.Tick();
        check(ex.HandFor(1).Count==5&&ex.Winner is null&&wins==0&&exq.IsBusy,
            "Exodia not published in the middle of reserved Scout draws");
        exGate.Release=true;Drain(exq);
        check(ex.ExodiaWinner==1&&ex.Winner==1&&wins==1&&
            ex.LastDeckFlow is {DrawFailed:true,MovedFromDeck:2,SentToGraveyard:1}&&
            ex.LastDeckFlow==exLegacy.LastDeckFlow&&ex.Graveyard(1).Single().Id==342&&
            ex.HandFor(1).SequenceEqual(exLegacy.HandFor(1)),
            "Scout completes all draws and discard; Exodia takes precedence over shortfall");

        var suppressed=DemoDuel.FromPartialObservation(0,[],Slots(Monster(561)),Slots(),8000,side1Deck:[Monster(24)]);
        suppressed.MonsterFaceDown[0,0]=suppressed.DefensePosition[0,0]=true;
        suppressed.Spells[1,0]=Spell(FlipEffectSuppressionRules.RoyalCommand,21);
        var sq=suppressed.CreatePrimitiveActionQueue();suppressed.TryQueueChangePosition(0,out _);Drain(sq);
        check(suppressed.LastDeckFlow is null&&suppressed.DeckCount(1)==1&&
            !sq.Trace.Any(t=>t.Entry.Command is SendToGraveyardCommand),"Royal Command suppresses mill");

        var primitive=DemoDuel.FromPartialObservation(0,[],Slots(),Slots(),8000,side0Deck:[Monster(24),Monster(24)]);
        var pq=primitive.CreatePrimitiveActionQueue();
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,0,out var a);
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,1,out var b);
        pq.EnqueueBatch([new SendToGraveyardCommand(0,b),new SendToGraveyardCommand(0,a),
            new SendToGraveyardCommand(0,a),new SendToGraveyardCommand(0,b)]);
        Drain(pq);
        check(primitive.Graveyard(0).Count==2&&primitive.DeckCount(0)==0&&!primitive.GameOver&&
            pq.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Result?.Outcome==CommandOutcome.Skipped)==2,
            "Top-only mill skips non-top and stale occurrence");
    }
}
