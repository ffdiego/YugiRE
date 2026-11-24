using Joey.Core;

internal static class QueuedGraveyardRecoveryChecks
{
    private static Card Monster(ushort id) => new(id,id,"Monster","",(18u<<20)|(4u<<25));
    private static Card Spell(ushort id,int type=22) => new(id,id,"Spell/Trap","",(uint)type<<20);
    private static Card?[] Slots(Card? card=null) => [card,null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Recovery queue did not drain.");
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
        foreach(ushort id in new ushort[]{427,101})
        {
            var desired=Spell(id==427?(ushort)342:(ushort)1056,id==427?22:21);
            var wrong=Spell(999,id==427?21:22);
            Card[] grave=[desired,wrong,desired,Monster(24)];
            var d=DemoDuel.FromPartialObservation(side,[],
                side==0?Slots(Monster(id)):Slots(),side==1?Slots(Monster(id)):Slots(),8000,
                side0Graveyard:side==0?grave:[desired],side1Graveyard:side==1?grave:[desired]);
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueChangePosition(0,out _),"Recovery Flip Summon admitted");
            Drain(q);
            var request=d.PendingCardSelection ?? throw new InvalidOperationException("Missing recovery request.");
            var candidates=d.QueuedGraveyardRecoveryCandidates(request);
            check(candidates.Count==2&&candidates[0].SourceIndex==0&&candidates[1].SourceIndex==2&&
                candidates[0].Target.Occurrence!=candidates[1].Target.Occurrence&&
                candidates.All(c=>c.Target.Side==side&&c.Target.Zone==DuelZone.Graveyard),
                "Recovery offers distinct same-ID copies, own GY and correct class");
            var first=candidates[0];var second=candidates[1];
            check(!d.TryQueueGraveyardRecovery(request,null,out _)&&
                !d.TryQueueGraveyardRecovery(request with {},second.Target,out _)&&
                !d.TryQueueGraveyardRecovery(request,second.Target with {Side=1-side},out _),
                "Recovery requires current request, exact occurrence and owner");
            try{q.Enqueue(new DrawCommand(side));check(false,"Unrelated command blocked");}
            catch(InvalidOperationException){check(true,"Unrelated command blocked");}
            int transfers=0;d.HandTransferred+=_=>transfers++;
            check(d.TryQueueGraveyardRecovery(request,second.Target,out _)&&
                d.PendingCardSelection==request&&d.Graveyard(side).Count==4&&d.Hand.Count==0,
                "Recovery admission does not move card");
            q.Tick();
            check(d.PendingCardSelection is null&&d.Hand.Count==0,"Recovery answer only schedules movement");
            gate.Release=false;q.Tick();q.Tick();
            check(d.Hand.Count==1&&ReferenceEquals(d.Hand[0],second.Card)&&
                d.Graveyard(side).Count==3&&q.IsBusy,"Recovery moves selected copy exactly once");
            gate.Release=true;Drain(q);
            check(d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var recovered)&&
                recovered.Occurrence==second.Target.Occurrence&&
                d.TryCaptureCardTarget(side,DuelZone.Graveyard,0,out var survivor)&&survivor==first.Target&&
                d.Graveyard(1-side).Single()==desired&&transfers==0&&
                !d.TryQueueGraveyardRecovery(request,second.Target,out _),
                "Recovery preserves identity, other copy and opponent GY; not a draw or repeatable");
        }

        var empty=DemoDuel.FromPartialObservation(0,[],Slots(Monster(427)),Slots(),8000,
            side0Graveyard:[Spell(1056,21)]);
        empty.MonsterFaceDown[0,0]=empty.DefensePosition[0,0]=true;
        var eq=empty.CreatePrimitiveActionQueue();empty.TryQueueChangePosition(0,out _);Drain(eq);
        check(empty.PendingCardSelection is null&&empty.Hand.Count==0,"No matching class, no recovery prompt");

        var stale=DemoDuel.FromPartialObservation(0,[],Slots(Monster(427)),Slots(),8000,
            side0Graveyard:[Spell(342),Spell(342)]);
        stale.MonsterFaceDown[0,0]=stale.DefensePosition[0,0]=true;
        var sq=stale.CreatePrimitiveActionQueue();stale.TryQueueChangePosition(0,out _);Drain(sq);
        var req=stale.PendingCardSelection!;
        var pick=stale.QueuedGraveyardRecoveryCandidates(req)[1];
        check(stale.TryQueueGraveyardRecovery(req,pick.Target,out _),"Recovery admitted before suppression");
        stale.Spells[1,0]=Spell(FlipEffectSuppressionRules.RoyalCommand,21);
        Drain(sq);
        check(stale.PendingCardSelection is null&&stale.Hand.Count==0&&stale.Graveyard(0).Count==2&&
            !sq.Trace.Any(t=>t.Entry.Command is MoveToHandCommand),
            "Royal Command becoming effective before response prevents movement");

        var direct=DemoDuel.FromPartialObservation(0,[],Slots(),Slots(),8000,
            side0Graveyard:[Spell(342),Spell(342)]);
        var dq=direct.CreatePrimitiveActionQueue();
        check(direct.TryCaptureCardTarget(0,DuelZone.Graveyard,0,out var a)&&
            direct.TryCaptureCardTarget(0,DuelZone.Graveyard,1,out var b),"Primitive GY identities captured");
        // Capture individually for definite assignment after the check callback.
        direct.TryCaptureCardTarget(0,DuelZone.Graveyard,0,out a);
        direct.TryCaptureCardTarget(0,DuelZone.Graveyard,1,out var secondTarget);
        dq.EnqueueBatch([new MoveToHandCommand(0,a),new MoveToHandCommand(0,a),new MoveToHandCommand(0,secondTarget)]);
        Drain(dq);
        check(direct.Hand.Count==2&&direct.Graveyard(0).Count==0&&
            dq.Trace.Any(t=>t.Stage==ActionTraceStage.Executed&&t.Result?.Outcome==CommandOutcome.Skipped),
            "Duplicate stale movement skips while shifted distinct copy resolves");
    }
}
