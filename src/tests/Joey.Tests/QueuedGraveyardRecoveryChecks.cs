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

    private static void RunBattleRecovery(Action<bool, string> check)
    {
        var attacker = new Card(24, 24, "Normal Monster", "", (3u << 20) | (4u << 25) | (140u << 9));
        foreach (int owner in new[] { 0, 1 })
        foreach (ushort id in new ushort[] { FlipEffectRules.MagicianOfFaith, FlipEffectRules.MaskOfDarkness })
        foreach (bool hasCandidate in new[] { false, true })
        {
            int enemy = 1 - owner;
            var desired = Spell(id == FlipEffectRules.MagicianOfFaith ? (ushort)342 : (ushort)1056,
                id == FlipEffectRules.MagicianOfFaith ? 22 : 21);
            var wrong = Spell(999, desired.ClassCode == 22 ? 21 : 22);
            Card[] grave = hasCandidate ? [desired, wrong, desired] : [wrong];
            var duel = DemoDuel.FromPartialObservation(1, [],
                owner == 0 ? Slots(Monster(id)) : Slots(attacker),
                owner == 1 ? Slots(Monster(id)) : Slots(attacker), 8000,
                side0Graveyard: owner == 0 ? grave : [desired], side1Graveyard: owner == 1 ? grave : [desired]);
            duel.MonsterFaceDown[owner, 0] = duel.DefensePosition[owner, 0] = true;
            if (enemy == 0) check(duel.EndTurn(out _), "Battle recovery fixture advances to the attacking player");
            check(duel.BeginBattle(out _), "Battle recovery fixture enters Battle Phase");
            var gate = new Gate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            check(duel.TryQueueAttack(0, 0, out _), "P018 attack uses queued combat");
            Drain(queue);
            if (!hasCandidate)
            {
                check(duel.PendingCardSelection is null && duel.Graveyard(owner).Last().Id == id &&
                    duel.Monsters[owner, 0] is null, "Without a legal P018 card, battle finishes without a choice");
                continue;
            }

            var request = duel.PendingCardSelection ?? throw new InvalidOperationException("Missing battle recovery.");
            var candidates = duel.QueuedGraveyardRecoveryCandidates(request);
            check(duel.UsesQueuedHandSelection(request) && request.Owner == owner &&
                duel.IsMarkedForBattleDestruction(owner, 0) && candidates.Select(c => c.SourceIndex).SequenceEqual([0, 2]) &&
                !duel.ResolveCardSelection(0, out _),
                "Battle P018 offers the correct class and disallows an immediate legacy answer");
            var picked = candidates[owner == 0 ? 1 : 0].Target;
            check(owner == 0 ? duel.TrySubmitQueuedHandSelection(request, 2, out _) :
                new CpuDuelController(owner).Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Selection },
                "Player and CPU share the P018 producer during the opponent's turn");
            gate.Release = false;
            queue.Tick();
            queue.Tick();
            check(queue.Active?.Command is MoveToHandCommand && duel.IsMarkedForBattleDestruction(owner, 0) &&
                duel.TryCaptureCardTarget(owner, DuelZone.Hand, 0, out var recovered) && recovered.Occurrence == picked.Occurrence,
                "P018 moves one exact occurrence and holds battle cleanup behind the movement gate");
            gate.Release = true;
            Drain(queue);
            check(duel.PendingCardSelection is null && duel.Monsters[owner, 0] is null &&
                duel.Graveyard(owner).Last().Id == id && !queue.Trace.Any(e => e.Entry.Command is ShuffleDeckCommand) &&
                queue.Trace.Where(e => e.Stage == ActionTraceStage.Executed).All(e => e.Result?.Outcome == CommandOutcome.Applied),
                "Battle P018 finishes and removes the defender after completing recovery");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        RunBattleRecovery(check);
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
            check(d.UsesQueuedHandSelection(request)&&!d.TrySubmitQueuedHandSelection(request,null,out _)&&
                !d.TrySubmitQueuedHandSelection(request,1,out _),"UI adapter rejects decline and wrong class");
            check(d.TrySubmitQueuedHandSelection(request,second.SourceIndex,out _)&&
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

        foreach(int side in new[]{0,1})
        {
            var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(Monster(427)):Slots(),
                side==1?Slots(Monster(427)):Slots(),8000,
                side0Graveyard:side==0?[Spell(342)]:[],side1Graveyard:side==1?[Spell(342)]:[]);
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var q=d.CreatePrimitiveActionQueue();d.TryQueueChangePosition(0,out _);Drain(q);
            var cpu=new CpuDuelController(side);
            var step=cpu.Step(d,queueTurnActions:true);
            check(step.Status==CpuStepStatus.Acted&&q.IsBusy&&d.Hand.Count==0&&d.Graveyard(side).Count==1,
                "CPU recovery answer admitted without legacy mutation");
            Drain(q);
            check(d.Hand.Count==1&&d.PendingCardSelection is null&&d.Graveyard(side).Count==0&&
                q.Trace.Any(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Origin==ActionOrigin.Cpu),
                "CPU recovery uses same queue and origin");
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
