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
    private static void CheckQueuedCylinder(Action<bool, string> check)
    {
        var cylinder = new Card(1214, AttackTrapRules.MagicCylinder, "Magic Cylinder", "", 21u << 20);
        var attacker = Monster(1400);
        foreach (int responder in new[] { 0, 1 })
        foreach (bool pass in new[] { false, true })
        {
            int side = 1 - responder;
            var duel = DemoDuel.FromPartialObservation(responder, [cylinder, cylinder],
                side == 0 ? Slots(attacker) : Slots(), side == 1 ? Slots(attacker) : Slots(), 8000);
            check(duel.SetFromHand(0, out _) && duel.SetFromHand(0, out _) &&
                duel.EndTurn(out _) && duel.BeginBattle(out _), "Queued Cylinder fixture");
            var gate = new Gate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("attack 2 direct").Enqueued &&
                duel.DescribeActionPresentation(queue.Trace.Last().Entry.Command) is null,
                "Queued response suppresses the attack cue until a pass");
            queue.Tick();
            check(queue.Active?.Command is DeclareDirectAttackCommand &&
                duel.PendingAttackResponse is null && duel.Life.SequenceEqual([8000, 8000]),
                "Declaration gate precedes the response window");

            gate.Release = true;
            Drain(queue);
            check(console.HasPendingChoice && duel.PendingAttackResponse is { } choice &&
                duel.UsesQueuedAttackResponse(choice) && choice.EligibleTraps.Count == 2 &&
                console.Execute("choices").Message.Contains("1214") &&
                !console.Execute("battle end").Success && !duel.PassAttackResponse(out _) &&
                !duel.ActivateAttackTrap(responder, 0, out _),
                "Queued response owns the only pass or activation path");

            if (!pass)
            {
                check(console.Execute($"respond-attack {responder} 0").Enqueued,
                    "Cylinder selects the original trap before a fixture-only occurrence swap");
                SwapCylinders(duel, responder);
                Drain(queue);
                check(queue.Trace.Last().Result?.Outcome == CommandOutcome.Rejected &&
                    console.HasPendingChoice && duel.Graveyard(responder).Count == 0 &&
                    duel.Life.SequenceEqual([8000, 8000]) &&
                    !console.Execute($"respond-attack {responder} 0").Enqueued,
                    "Cylinder rejects swapped copies at execution and at admission without closing the window");
                SwapCylinders(duel, responder);
            }

            var answer = pass ? console.Execute("respond-attack pass").Enqueued :
                new CpuDuelController(responder).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Response };
            var admitted = queue.Trace.Last().Entry;
            var preview = duel.DescribeActionPresentation(admitted.Command);
            check(answer && admitted.Origin == (pass ? ActionOrigin.Console : ActionOrigin.Cpu) &&
                preview?.Kind == (pass ? ActionPresentationKind.DirectAttack : ActionPresentationKind.Activate) &&
                duel.Life.SequenceEqual([8000, 8000]) && duel.Spells[responder, 0] == cylinder,
                "Queue admits the selected response read-only and owns its presentation");

            gate.Release = false;
            queue.Tick();
            check(duel.PendingAttackResponse is null && duel.Life.SequenceEqual([8000, 8000]) &&
                duel.Spells[responder, 0] == cylinder,
                "Response presentation gate precedes spend and damage");
            gate.Release = true;
            queue.Tick();
            if (!pass)
            {
                gate.Release = false;
                queue.Tick();
                check(queue.Active?.Command is SendToGraveyardCommand &&
                    duel.Graveyard(responder).SequenceEqual([cylinder]) &&
                    duel.Life.SequenceEqual([8000, 8000]),
                    "Cylinder spends its exact occurrence before reflecting damage");
                gate.Release = true;
            }
            Drain(queue);
            check(duel.Life[pass ? responder : side] == 6600 &&
                duel.Life[pass ? side : responder] == 8000 && duel.Phase == DemoPhase.Battle &&
                duel.Monsters[side, 2] == attacker && duel.Spells[responder, 1] == cylinder &&
                (pass ? duel.LastAttackTrap is null && duel.Spells[responder, 0] == cylinder
                    : duel.LastAttackTrap is { TrapResourceId: AttackTrapRules.MagicCylinder, ReflectedDamage: 1400 } &&
                        duel.Spells[responder, 0] is null) &&
                !duel.TryQueueDirectAttack(2, out _) && !console.HasPendingChoice,
                "Pass resumes the same attack and Cylinder reflects once without ending battle");
        }
    }

    private static void SwapCylinders(DemoDuel duel, int side) =>
        (duel.Spells[side, 0], duel.Spells[side, 1]) = (duel.Spells[side, 1], duel.Spells[side, 0]);

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 40 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Attack response queue did not drain.");
    }

    public static void Run(Action<bool,string> check)
    {
        CheckQueuedCylinder(check);
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
