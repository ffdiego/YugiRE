using System.Text.Json;
using Joey.Core;

internal static class QueuedBattleManEaterChecks
{
    private static Card Monster(ushort id,int atk=1000,int def=1000) =>
        new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card card,int slot)
    {
        var result=new Card?[5];result[slot]=card;return result;
    }
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}=true;
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>
            command.Command is not (DestroyCommand or DamageCommand) || Release;
    }
    private static void Until(ActionQueue q,Func<bool> stop)
    {
        for(int i=0;i<100&&!stop()&&q.IsBusy;i++)q.Tick();
        if(!stop())throw new InvalidOperationException("Man-Eater battle did not reach expected stage.");
    }
    private static void Drain(ActionQueue q)=>Until(q,()=>!q.IsBusy);
    private static bool Complete(DuelConsoleSession session)
    {
        using var trace=JsonDocument.Parse(session.Execute("trace").Message);
        return trace.RootElement.GetProperty("complete").GetBoolean();
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(int atk in new[]{1400,1000,600})
        foreach(bool self in new[]{false,true})
        {
            int owner=1-side,targetSide=self?owner:side,targetSlot=self?3:2;
            bool battleRemoval=atk>1000&&!self;
            var a=Monster(24,atk);var b=Monster(FlipEffectRules.ManEaterBug);
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                    side==1?Slots(a,2):Slots(b,3),8000);
                if(side==0){d.NextTurn();d.NextTurn();}
                d.MonsterFaceDown[owner,3]=d.DefensePosition[owner,3]=true;
                return d;
            }
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(2,3,out _)&&legacy.PendingFlipTarget is not null&&
                legacy.ResolveFlipTarget(targetSide,targetSlot,out _),"A06 Man-Eater legacy comparison");

            ActionTraceEntry[] Pilot(bool delayed)
            {
                var d=State();var gate=new Gate();var console=new DuelConsoleSession(d,delayed?gate:null);
                var q=console.Queue;
                var destroyed=new List<DemoMonsterDestruction>();d.MonsterDestroyed+=destroyed.Add;
                d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var originalA);
                d.TryCaptureCardTarget(owner,DuelZone.Monster,3,out var originalB);
                check(d.BeginBattle(out _)&&console.Execute("attack 2 3").Enqueued,"A06 Man-Eater battle admitted");
                if(delayed&&atk<1000)
                {
                    gate.Release=false;Until(q,()=>q.Active?.Command is DamageCommand);q.Tick();
                    check(d.PendingFlipTarget is null&&d.Life[side]==7600,"A06 Man-Eater damage gate before choice");
                    gate.Release=true;
                }
                Drain(q);
                var candidates=d.QueuedMonsterFlipCandidates();
                check(console.HasPendingChoice&&!Complete(console)&&d.PendingFlipTarget is {Owner:var p}&&p==owner&&
                    candidates.Count==2&&candidates.Any(c=>c.Target==originalA)&&candidates.Any(c=>c.Target==originalB)&&
                    d.IsMarkedForBattleDestruction(owner,3)==(atk>1000)&&destroyed.Count==0,
                    "A06 Man-Eater may destroy marked self or attacker");
                check(!console.Execute("select-flip decline").Success&&
                    !console.Execute($"select-flip {side} 2 {owner} 3").Success&&
                    !console.Execute("damage 1").Success&&
                    !d.ResolveFlipTarget(targetSide,targetSlot,out _)&&!d.FinishFlipTargets(out _)&&
                    new DuelConsoleScript(console,["damage 1"]).Advance().Status==DuelScriptStatus.WaitingForDecision,
                    "A06 Man-Eater mandatory single target and blocked legacy bypass");
                check(console.Execute($"select-flip {targetSide} {targetSlot}").Enqueued&&
                    d.PendingFlipTarget is not null&&destroyed.Count==0,"A06 Man-Eater admission read-only");
                if(delayed)
                {
                    gate.Release=false;Until(q,()=>q.Active?.Command is DestroyCommand);q.Tick();
                    check(q.IsBusy&&destroyed.Count==1&&d.Graveyard(targetSide).Count==1&&
                        d.Monsters[targetSide,targetSlot] is null&&
                        (self||d.Monsters[owner,3] is not null)&&
                        !q.Trace.Any(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Command.GetType().Name=="SnapshotBattleRemovalCommand"),
                        "A06 effect destruction gate precedes battle cleanup");
                    gate.Release=true;
                }
                Drain(q);
                check(q.Fault is null&&!console.HasPendingChoice&&Complete(console)&&
                    d.Life.SequenceEqual(legacy.Life)&&d.Graveyard(side).SequenceEqual(legacy.Graveyard(side))&&
                    d.Graveyard(owner).SequenceEqual(legacy.Graveyard(owner))&&
                    (d.Monsters[side,2] is null)==!self&&
                    (d.Monsters[owner,3] is null)==(self||battleRemoval)&&
                    !d.IsMarkedForBattleDestruction(owner,3)&&destroyed.Count==(battleRemoval?2:1),
                    "A06 Man-Eater final state and destruction event counts match legacy");
                check(d.TryCaptureCardTarget(side,self?DuelZone.Monster:DuelZone.Graveyard,self?2:0,out var placedA)&&
                    placedA.Occurrence==originalA.Occurrence&&
                    d.TryCaptureCardTarget(owner,self||battleRemoval?DuelZone.Graveyard:DuelZone.Monster,
                        self||battleRemoval?0:3,out var placedB)&&placedB.Occurrence==originalB.Occurrence,
                    "A06 Man-Eater both combatants preserve occurrence identity");
                var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).ToArray();
                int effect=Array.FindIndex(executed,t=>t.Entry.Command is DestroyCommand);
                int finish=Array.FindIndex(executed,t=>t.Entry.Command.GetType().Name=="FinishBattleFlipCommand");
                check(executed.All(t=>t.Result?.Outcome==CommandOutcome.Applied)&&
                    executed.Count(t=>t.Entry.Command is DestroyCommand)==1&&effect>=0&&finish>effect&&
                    executed[effect].Entry.Command is DestroyCommand destroy&&
                    destroy.Target==(self?originalB:originalA)&&
                    executed.Count(t=>t.Entry.Command.GetType().Name=="SnapshotBattleRemovalCommand")==(battleRemoval?1:0)&&
                    executed.Count(t=>t.Entry.Command.GetType().Name=="FinishBattleRemovalCommand")==(battleRemoval?1:0),
                    "A06 effect destruction never duplicates battle destruction");
                return q.Trace.ToArray();
            }
            check(Pilot(false).SequenceEqual(Pilot(true)),"A06 Man-Eater trace independent of gate");
        }
        var lethal=DemoDuel.FromPartialObservation(1,[],Slots(Monster(500),3),Slots(Monster(24,600),2),100);
        lethal.MonsterFaceDown[0,3]=lethal.DefensePosition[0,3]=true;
        var lq=lethal.CreatePrimitiveActionQueue();lethal.BeginBattle(out _);
        check(lethal.TryQueueTargetedAttack(2,3,out _),"A06 Man-Eater lethal admitted");
        Drain(lq);
        check(lethal.Winner==0&&lethal.PendingFlipTarget is null&&lethal.Monsters[0,3] is not null&&
            lethal.Monsters[1,2] is not null&&lq.Trace.Where(t=>t.Stage==ActionTraceStage.Executed)
            .All(t=>t.Result?.Outcome==CommandOutcome.Applied),"A06 Man-Eater terminal policy skips prompt");
    }
}
