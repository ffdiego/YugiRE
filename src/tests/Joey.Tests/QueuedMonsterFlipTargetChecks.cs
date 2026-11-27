using Joey.Core;

internal static class QueuedMonsterFlipTargetChecks
{
    private static Card Monster(ushort id,int type=15,int atk=1000,int def=800) =>
        new(id,id,$"Monster {id}","",((uint)type<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card Spell(ushort id,int type=22) => new(id,id,"Spell/Trap","",(uint)type<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static CardTarget Capture(DemoDuel d,int side,int zone)
    {
        if(!d.TryCaptureCardTarget(side,DuelZone.Monster,zone,out var target))
            throw new InvalidOperationException("Untracked test monster.");
        return target;
    }
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("FLIP targets did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not ReturnToHandCommand && c.Command is not DestroyCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        CheckBiteShoes(check);
        foreach(int side in new[]{0,1})
        foreach(ushort id in new ushort[]{500,540,601})
        {
            int enemy=1-side;
            var flip=Monster(id);var normal=Monster(24);var other=Monster(25);
            var own=Slots(flip,normal);var opposing=Slots(other);
            var d=DemoDuel.FromPartialObservation(side,[Spell(300)],side==0?own:opposing,
                side==1?own:opposing,8000,equipmentContextResolved:true);
            check(d.Equip(0,enemy,0,out _)&&d.AttackValue(enemy,0)==1300,"FLIP setup: opposing equipment target");
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var removals=new List<DemoMonsterDestruction>();
            d.MonsterDestroyed+=removals.Add;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueChangePosition(0,out _),"FLIP summon admitted");
            Drain(q);
            var source=Capture(d,side,0);var target=Capture(d,enemy,0);
            check(d.PendingFlipTarget?.SourceCard.Id==id&&d.QueuedMonsterFlipCandidates().Count==3,
                "FLIP choice exposes actual occurrences on both sides");
            try {q.Enqueue(new DrawCommand(side));check(false,"Pending FLIP blocks unrelated queue admission");}
            catch(InvalidOperationException){check(true,"Pending FLIP blocks unrelated queue admission");}
            check(!d.TryQueueMonsterFlipAnswer(source,[target,target],out _),"FLIP duplicate selection rejected");
            if(id!=601)check(!d.TryQueueMonsterFlipAnswer(source,[],out _),"Mandatory target required");
            CardTarget[] picks=id==601?[target,source]:[target];
            check(d.TryQueueMonsterFlipAnswer(source,picks,out _)&&d.PendingFlipTarget is not null&&
                d.Monsters[enemy,0] is not null,"Admission leaves pending choice and board untouched");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.Monsters[enemy,0] is null&&d.EquipmentCards.Count==1&&q.IsBusy,
                "FLIP first movement applied once; linked equipment waits");
            check(id==500 ? removals.Count==1 && removals[0].EffectSourceResourceId==500 &&
                removals[0].Card==other && removals[0].Player==enemy && removals[0].Zone==0
                : removals.Count==0, "V22 removal provenance and pre-mutation snapshot; bounce not destruction");
            gate.Release=true;Drain(q);
            check(d.PendingFlipTarget is null&&d.EquipmentCards.Count==0&&
                d.Graveyard(side).Single().Id==300&&
                (id==500?d.Graveyard(enemy).Single().Id==25:d.HandFor(enemy).Single().Id==25)&&
                (d.Monsters[side,0] is null)==(id==601),
                $"FLIP {id}: chosen moves and separately queued equipment cleanup");
            check(q.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&
                t.Entry.Command is ReturnToHandCommand)==(id==500?0:id==540?1:2),
                "Exactly one return command per selected monster");
        }


        foreach (int side in new[]{0,1})
        foreach (ushort id in new ushort[]{500,540,601})
        {
            int enemy=1-side;
            var own=Slots(Monster(id));var opposing=Slots(Monster(24),Monster(25));
            var d=DemoDuel.FromPartialObservation(side,[],side==0?own:opposing,side==1?own:opposing,8000);
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var q=d.CreatePrimitiveActionQueue();d.TryQueueChangePosition(0,out _);Drain(q);
            var request=d.PendingFlipTarget!;
            check(d.UsesQueuedMonsterFlipSelection(request)&&
                !d.TrySelectQueuedMonsterFlipTarget(request with {},enemy,0,out _),
                "Shared producer rejects stale request identity");
            var controller=new CpuDuelController(side);
            var step=controller.Step(d,queueTurnActions:true);
            check(step.Status==CpuStepStatus.Acted&&d.PendingFlipTarget==request&&d.Monsters[enemy,0] is not null,
                "CPU choice never moves target at admission");
            if(id==601)
            {
                check(!q.IsBusy&&d.SelectedFlipTargets.Count==1&&
                    !d.TrySelectQueuedMonsterFlipTarget(request,enemy,0,out _),
                    "Penguin first selection is only a distinct pre-admission pick");
                step=controller.Step(d,queueTurnActions:true);
                check(step.Status==CpuStepStatus.Acted,"CPU admits second target");
            }
            check(q.IsBusy&&!d.TrySelectQueuedMonsterFlipTarget(request,enemy,0,out _),
                "Busy producer rejects a second input");
            Drain(q);
            check(d.PendingFlipTarget is null&&d.Monsters[enemy,0] is null&&
                (d.Monsters[enemy,1] is null)==(id==601)&&
                q.Trace.Any(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Origin==ActionOrigin.Cpu&&
                    t.Entry.Command.GetType().Name=="ResolveMonsterFlipTargetsCommand"),
                "CPU choice uses same queued answer and origin");
        }

        var one=DemoDuel.FromPartialObservation(0,[],Slots(Monster(601)),Slots(Monster(24)),8000);
        one.MonsterFaceDown[0,0]=one.DefensePosition[0,0]=true;
        var oq=one.CreatePrimitiveActionQueue();one.TryQueueChangePosition(0,out _);Drain(oq);
        var op=one.PendingFlipTarget!;
        check(one.TrySelectQueuedMonsterFlipTarget(op,1,0,out _)&&!oq.IsBusy&&
            one.TryFinishQueuedMonsterFlipSelection(op,out _)&&oq.IsBusy&&one.Monsters[1,0] is not null,
            "Player may finish with one chosen occurrence before any move");
        Drain(oq);
        check(one.PendingFlipTarget is null&&one.HandFor(1).Single().Id==24&&one.Monsters[0,0] is not null,
            "One-target player answer completes through queue");

        var replace=DemoDuel.FromPartialObservation(0,[],Slots(Monster(601)),Slots(Monster(24)),8000);
        replace.MonsterFaceDown[0,0]=replace.DefensePosition[0,0]=true;
        var rq=replace.CreatePrimitiveActionQueue();replace.TryQueueChangePosition(0,out _);Drain(rq);
        var rp=replace.PendingFlipTarget!;
        check(replace.TrySelectQueuedMonsterFlipTarget(rp,1,0,out _),"First occurrence captured");
        replace.Monsters[1,0]=Monster(24);
        check(!replace.TryFinishQueuedMonsterFlipSelection(rp,out _)&&!rq.IsBusy&&
            replace.PendingFlipTarget==rp&&replace.SelectedFlipTargets.Count==0&&replace.Monsters[1,0] is not null,
            "Same-ID replacement invalidates staged pick without movement");
        check(replace.TryFinishQueuedMonsterFlipSelection(rp,out _),"Player can recover with zero targets");
        Drain(rq);

        var zero=DemoDuel.FromPartialObservation(0,[],Slots(Monster(601)),Slots(Monster(24)),8000);
        zero.MonsterFaceDown[0,0]=zero.DefensePosition[0,0]=true;
        var zq=zero.CreatePrimitiveActionQueue();zero.TryQueueChangePosition(0,out _);Drain(zq);
        check(zero.TryFinishQueuedMonsterFlipSelection(zero.PendingFlipTarget!,out _),"Penguin may select zero");
        Drain(zq);
        check(zero.PendingFlipTarget is null&&zero.Monsters[0,0] is not null&&zero.Monsters[1,0] is not null,
            "Empty Penguin answer moves nothing");

        var stale=DemoDuel.FromPartialObservation(0,[],Slots(Monster(540)),Slots(Monster(24)),8000);
        stale.MonsterFaceDown[0,0]=stale.DefensePosition[0,0]=true;
        var sq=stale.CreatePrimitiveActionQueue();stale.TryQueueChangePosition(0,out _);Drain(sq);
        var ss=Capture(stale,0,0);var st=Capture(stale,1,0);
        stale.Monsters[1,0]=Monster(24);
        check(!stale.TryQueueMonsterFlipAnswer(ss,[st],out _)&&stale.PendingFlipTarget is not null,
            "Same-ID replacement rejected before resolution");

        var thunder=DemoDuel.FromPartialObservation(0,[],Slots(Monster(540)),Slots(Monster(461)),8000);
        thunder.MonsterFaceDown[0,0]=thunder.DefensePosition[0,0]=true;
        thunder.MonsterWasFlipped[1,0]=true;
        var tq=thunder.CreatePrimitiveActionQueue();thunder.TryQueueChangePosition(0,out _);Drain(tq);
        check(thunder.TryQueueMonsterFlipAnswer(Capture(thunder,0,0),[Capture(thunder,1,0)],out _),
            "Immortal of Thunder return admitted");
        Drain(tq);
        check(thunder.Life[1]==8000&&thunder.HandFor(1).Single().Id==461&&thunder.Graveyard(1).Count==0,
            "Bounce never triggers field-to-graveyard LP loss");

        var search=DemoDuel.FromPartialObservation(0,[],Slots(Monster(500)),Slots(Monster(47)),8000,
            side1Deck:[Monster(24)]);
        search.MonsterFaceDown[0,0]=search.DefensePosition[0,0]=true;
        var g=new Gate();var fq=search.CreatePrimitiveActionQueue(g);
        search.TryQueueChangePosition(0,out _);Drain(fq);
        check(search.TryQueueMonsterFlipAnswer(Capture(search,0,0),[Capture(search,1,0)],out _),
            "Man-Eater Bug on Sangan admitted");
        fq.Tick();g.Release=false;fq.Tick();
        check(search.PendingCardSelection is null&&search.Graveyard(1).Single().Id==47,
            "Sangan choice waits for destruction gate");
        g.Release=true;Drain(fq);
        check(search.PendingCardSelection is {Owner:1,EffectResourceId:47},"Sangan choice exposed after gate");

        var battle=DemoDuel.FromPartialObservation(1,[],Slots(Monster(601,def:500)),
            Slots(Monster(24,atk:2000)),8000);
        battle.MonsterFaceDown[0,0]=battle.DefensePosition[0,0]=true;
        check(battle.BeginBattle(out _)&&battle.Attack(0,0,out _)&&
            battle.PendingFlipTarget is {Effect:DemoFlipEffect.ReturnUpToTwo}&&
            battle.IsMarkedForBattleDestruction(0,0),"Battle FLIP fixture");
        var bq=battle.CreatePrimitiveActionQueue();
        check(battle.QueuedMonsterFlipCandidates().All(c=>c.Target.Side!=0)&&
            !battle.TryQueueMonsterFlipAnswer(Capture(battle,0,0),[],out _)&&!bq.IsBusy,
            "Marked Penguin excluded; battle response remains legacy");
        check(battle.ResolveFlipTarget(1,0,out _)&&battle.FinishFlipTargets(out _)&&
            battle.Graveyard(0).Single().Id==601&&battle.HandFor(1).Single().Id==24,
            "Existing battle FLIP resolution preserved");
    }

    private static void CheckBiteShoes(Action<bool, string> check)
    {
        var bite = Monster(FlipEffectRules.BiteShoes);
        var normal = Monster(24);
        foreach (int side in new[] { 0, 1 })
        {
            int enemy = 1 - side;
            var own = Slots(bite, normal);
            var opposing = Slots(normal, normal);
            var duel = DemoDuel.FromPartialObservation(side, [], side == 0 ? own : opposing,
                side == 1 ? own : opposing, 8000);
            duel.MonsterFaceDown[side, 0] = duel.DefensePosition[side, 0] = true;
            duel.MonsterFaceDown[enemy, 0] = duel.DefensePosition[enemy, 0] = true;
            duel.DefensePosition[enemy, 1] = true;
            var queue = duel.CreatePrimitiveActionQueue();
            check(duel.TryQueueChangePosition(0, out _), "Bite Shoes can be Flip Summoned through the queue");
            Drain(queue);
            var request = duel.PendingFlipTarget ?? throw new InvalidOperationException("Bite Shoes did not ask for a target.");
            var candidates = duel.QueuedMonsterFlipCandidates();
            check(duel.UsesQueuedMonsterFlipSelection(request) && candidates.Count == 3 &&
                candidates.All(candidate => !candidate.FaceDown) &&
                !duel.TrySelectQueuedMonsterFlipTarget(request, enemy, 0, out _) &&
                !duel.ResolveFlipTarget(enemy, 1, out _),
                "Bite Shoes lists face-up cards of both owners and rejects hidden or legacy selection");

            bool targetsOwnMonster = side == 0;
            int targetSide = targetsOwnMonster ? side : enemy;
            var target = Capture(duel, targetSide, 1);
            check(duel.TrySelectQueuedMonsterFlipTarget(request, targetSide, 1, out _) &&
                duel.DefensePosition[targetSide, 1] != targetsOwnMonster,
                "Bite Shoes admits an occurrence before its position changes");
            Drain(queue);
            check(Capture(duel, targetSide, 1) == target && !duel.MonsterFaceDown[targetSide, 1] &&
                duel.DefensePosition[targetSide, 1] == targetsOwnMonster && duel.PendingFlipTarget is null &&
                (!targetsOwnMonster || duel.CanChangePosition(1)),
                "Bite Shoes toggles only the selected position and preserves manual position allowance");
        }

        foreach (bool targetsSelf in new[] { false, true })
        {
            var duel = DemoDuel.FromPartialObservation(1, [], Slots(bite), Slots(Monster(24, atk: 1500)), 8000);
            duel.MonsterFaceDown[0, 0] = duel.DefensePosition[0, 0] = true;
            var queue = duel.CreatePrimitiveActionQueue();
            DemoMonsterDestruction? destroyed = null;
            duel.MonsterDestroyed += item => destroyed = item;
            check(duel.BeginBattle(out _) && duel.TryQueueTargetedAttack(0, 0, out _), "Battle Bite Shoes is admitted");
            Drain(queue);
            var request = duel.PendingFlipTarget ?? throw new InvalidOperationException("Battle Bite Shoes did not ask for a target.");
            check(duel.IsMarkedForBattleDestruction(0, 0), "Bite Shoes opens its choice after battle marking");
            check(targetsSelf
                    ? duel.TrySelectQueuedMonsterFlipTarget(request, 0, 0, out _)
                    : new CpuDuelController(0).Step(duel) is { Action: CpuAction.Selection, Status: CpuStepStatus.Acted },
                "Battle Bite Shoes can change its own position or have the CPU change the attacker's");
            Drain(queue);
            check(duel.Life.SequenceEqual([8000, 8000]) && duel.Graveyard(0).Single() == bite &&
                duel.DefensePosition[1, 0] == !targetsSelf && destroyed?.Defense == !targetsSelf,
                "Bite Shoes position changes do not recalculate damage or undo marked destruction");
        }

        var stale = DemoDuel.FromPartialObservation(0, [], Slots(bite), Slots(normal), 8000);
        stale.MonsterFaceDown[0, 0] = stale.DefensePosition[0, 0] = true;
        var staleQueue = stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueChangePosition(0, out _), "Bite Shoes stale target fixture flips");
        Drain(staleQueue);
        var staleRequest = stale.PendingFlipTarget!;
        check(stale.TrySelectQueuedMonsterFlipTarget(staleRequest, 1, 0, out _), "Admit the original Bite Shoes target");
        stale.Monsters[1, 0] = normal with { };
        Drain(staleQueue);
        check(!stale.DefensePosition[1, 0] && ReferenceEquals(stale.PendingFlipTarget, staleRequest) &&
            stale.TrySelectQueuedMonsterFlipTarget(staleRequest, 0, 0, out _),
            "Same-ID replacement does not move, and Bite Shoes can select another occurrence");
        Drain(staleQueue);
    }
}
