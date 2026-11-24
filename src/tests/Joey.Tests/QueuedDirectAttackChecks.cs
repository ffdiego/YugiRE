using Joey.Core;
using Joey.Lab;
using System.Text.Json;

internal static class QueuedDirectAttackChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>Release;
    }
    private static Card Monster(int attack,ushort id=24)=>new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(attack/10)<<9));
    private static Card?[] Slots(Card? c=null)=>[null,null,c,null,null];
    public static void Run(Action<bool,string> check)
    {
        var d=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1400)),8000);
        var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
        check(!d.TryQueueDirectAttack(2,out _),"A05.7 Main Phase rejects attack");
        check(d.BeginBattle(out _) && d.TryQueueDirectAttack(2,out _),"A05.7 Battle attack admitted");
        check(d.Life[0]==8000,"A05.7 admission read only");
        q.Tick();q.Tick();
        check(d.Life[0]==8000 && q.Active?.Command is DeclareDirectAttackCommand && q.PendingCount==1,
            "A05.7 declaration waits at gate before damage");
        gate.Release=true;
        q.Tick();gate.Release=false;q.Tick();
        check(d.Life[0]==6600 && q.Active?.Command is DamageCommand,"A05.7 one separate damage mutation");
        q.Tick();check(d.Life[0]==6600,"A05.7 no repeated damage while stalled");
        gate.Release=true;q.Tick();
        var rows=ActionTraceProjection.ReadExecuted(q.Trace);
        check(!q.IsBusy && rows.Count==2 && rows[0].Args!.SequenceEqual(new ushort[]{0x8034,2,1,0}) &&
            rows[1].Args!.SequenceEqual(new ushort[]{0x43,1400,0,1}) &&
            !d.TryQueueDirectAttack(2,out _),"A05.7 declaration/damage and once-per-turn");
        foreach(var enemy in new[]{false,true})
        {
            var blocked=DemoDuel.FromPartialObservation(1,[],Slots(enemy?Monster(1000):null),
                Slots(Monster(1000,219)),8000);
            var bq=blocked.CreatePrimitiveActionQueue();blocked.BeginBattle(out _);
            check(!blocked.TryQueueDirectAttack(2,out _) && bq.Trace.Count==0,"A05.7 rejects effects/other monsters");
        }
        var stale=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1000)),8000);
        var sq=stale.CreatePrimitiveActionQueue();stale.BeginBattle(out _);
        check(stale.TryQueueDirectAttack(2,out _),"A05.7 stale admitted");
        stale.Monsters[1,2]=null; // fixture-only invalidation
        sq.Tick();
        check(stale.Life[0]==8000 && sq.Trace.Last().Result?.Outcome==CommandOutcome.Skipped &&
            ActionTraceProjection.ReadExecuted(sq.Trace).Single().Mapping=="non_applied","A05.7 stale attack not projected");
        var zero=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(0)),8000);
        var zq=zero.CreatePrimitiveActionQueue();zero.BeginBattle(out _);
        check(zero.TryQueueDirectAttack(2,out _),"A05.7 zero ATK declaration");
        zq.Tick();
        check(!zq.IsBusy && zero.Life[0]==8000 && ActionTraceProjection.ReadExecuted(zq.Trace).Count==1,
            "A05.7 zero damage emits no LP command");
        var lethal=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1400)),8000);
        lethal.Life[0]=1000;var lq=lethal.CreatePrimitiveActionQueue();lethal.BeginBattle(out _);
        lethal.TryQueueDirectAttack(2,out _);lq.Tick();lq.Tick();
        check(lethal.Winner==1 && lethal.Life[0]==0 && !lq.IsBusy,"A05.7 lethal attack completes");
        using var output=new StringWriter();using var error=new StringWriter();
        check(ActionTraceExport.RunBattlePilot([],output,error)==0,"A05.7 battle pilot CLI");
        using var json=JsonDocument.Parse(output.ToString());
        var mapped=json.RootElement.GetProperty("rows").EnumerateArray().Where(x=>x.GetProperty("mapping").GetString()=="mapped")
            .Select(x=>x.GetProperty("args")[0].GetInt32()).ToArray();
        check(mapped.SequenceEqual([0x8061,0x80CE,0x8034,0x43]),"A05.7 draw summon attack damage order");
    }
}
