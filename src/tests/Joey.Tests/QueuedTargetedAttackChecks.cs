using Joey.Core;

internal static class QueuedTargetedAttackChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult r)=>Release;
    }
    private static Card Monster(ushort id,int atk,int def)=>new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card c,int slot) {var s=new Card?[5];s[slot]=c;return s;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(var (atk,other,defense,removeA,removeB,lossA,lossB) in new (int,int,bool,bool,bool,int,int)[]{
            (1400,1000,false,false,true,0,400),(1000,1400,false,true,false,400,0),
            (1000,1000,false,true,true,0,0),(0,0,false,false,false,0,0),
            (1400,1000,true,false,true,0,0),(1000,1000,true,false,false,0,0),
            (1000,1400,true,false,false,400,0)})
        {
            var a=Monster(24,atk,500);var b=Monster(1351,other,other);
            var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                side==1?Slots(a,2):Slots(b,3),8000);
            if(side==0) d.NextTurn(); // turn 2 (side 1), then turn 3 (side 0)
            if(side==0) d.NextTurn();
            d.DefensePosition[1-side,3]=defense;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.BeginBattle(out _) && d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var at) &&
                d.TryCaptureCardTarget(1-side,DuelZone.Monster,3,out var dt),"A05.8 fixture battle");
            // Capture independently for definite assignment and identity comparison.
            d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var originalA);
            d.TryCaptureCardTarget(1-side,DuelZone.Monster,3,out var originalB);
            check(d.TryQueueTargetedAttack(2,3,out _),"A05.8 targeted admission");
            q.Tick();q.Tick();
            check(d.Monsters[side,2]!=null && d.Monsters[1-side,3]!=null &&
                d.Life.SequenceEqual([8000,8000]),"A05.8 declaration gate precedes mutation");
            gate.Release=true;q.Tick();
            var stages=new List<string>();
            for(int i=0;i<30 && q.IsBusy;i++)
            {
                gate.Release=false;q.Tick();
                if(q.Active?.Command is SnapshotBattleRemovalCommand snap)
                    check(d.Monsters[snap.Side,snap.Role==0?2:3] is null && d.Graveyard(snap.Side).Count==0 &&
                        d.Life.SequenceEqual([8000,8000]),"A05.8 temporary record before LP and GY");
                gate.Release=true;q.Tick();
            }
            check(!q.IsBusy && (d.Monsters[side,2] is null)==removeA &&
                (d.Monsters[1-side,3] is null)==removeB && d.Life[side]==8000-lossA &&
                d.Life[1-side]==8000-lossB,"A05.8 common battle matrix");
            if(removeA) check(d.TryCaptureCardTarget(side,DuelZone.Graveyard,0,out var g) &&
                g.Occurrence==originalA.Occurrence,"A05.8 attacker identity preserved");
            if(removeB) check(d.TryCaptureCardTarget(1-side,DuelZone.Graveyard,0,out var g) &&
                g.Occurrence==originalB.Occurrence,"A05.8 defender identity preserved");
            var trace=ActionTraceProjection.ReadExecuted(q.Trace);
            check(trace[0].Args!.SequenceEqual(new ushort[]{(ushort)((side<<15)|0x33),2,3,(ushort)(2-side)}) &&
                trace.Count(x=>x.Command=="SnapshotBattleRemovalCommand")==(removeA?1:0)+(removeB?1:0) &&
                trace.Count(x=>x.Command=="FinishBattleRemovalCommand")==(removeA?1:0)+(removeB?1:0),
                "A05.8 declaration and exactly one snapshot/final per removal");
            if(removeB) check(trace.Last().Args!.SequenceEqual(new ushort[]{(ushort)(((1-side)<<15)|0x7c),3,0,1}),
                "A05.8 defender final routing mapped");
            if(!removeA) check(!d.TryQueueTargetedAttack(2,3,out _),"A05.8 attack consumed");
        }
        var d2=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1351,500,500),3),Slots(Monster(24,1400,500),2),8000);
        d2.Life[0]=100;var q2=d2.CreatePrimitiveActionQueue();d2.BeginBattle(out _);
        d2.TryQueueTargetedAttack(2,3,out _);for(int i=0;i<30 && q2.IsBusy;i++)q2.Tick();
        check(d2.Winner==1 && d2.Graveyard(0).Count==1 && !q2.IsBusy,"A05.8 reserved cleanup after lethal (sandbox policy)");
        var d3=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1114,500,500),3),Slots(Monster(24,1400,500),2),8000);
        var q3=d3.CreatePrimitiveActionQueue();d3.BeginBattle(out _);
        check(!d3.TryQueueTargetedAttack(2,3,out _) && q3.Trace.Count==0,"A05.8 effect ID rejected");
    }
}
