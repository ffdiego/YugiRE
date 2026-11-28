using Joey.Core;
using Joey.Lab;
using System.Text.Json;

internal static class ActionTraceProjectionChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release {get;set;}
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>Release;
    }
    private static void CheckBattleCalculationTrace(Action<bool, string> check)
    {
        foreach (bool direct in new[] { false, true })
        foreach (bool hidden in direct ? new[] { false } : new[] { false, true })
        {
            string Run(bool delayed)
            {
                // The battle-only modifier distinguishes calculation from a
                // post-battle field read (not a new individual card test).
                var attacker = new Card(1253, 1253, "Fixture", "", (7u<<20)|(4u<<25)|(135u<<9)|180u);
                var defender = new Card(77, 77, "Fixture", "", (15u<<20)|(4u<<25)|(200u<<9)|150u);
                Card?[] a = [null, attacker, null, null, null];
                Card?[] b = direct ? new Card?[5] : [null, null, null, defender, null];
                var duel = DemoDuel.FromPartialObservation(1, [], b, a, 8000,
                    checkpoint: new DuelCheckpoint(2, DemoPhase.Battle));
                duel.DefensePosition[0,3] = duel.MonsterFaceDown[0,3] = hidden;
                check(duel.Phase == DemoPhase.Battle, "A09 calculation fixture starts in observed Battle Phase");
                var gate = new Gate();
                var queue = duel.CreatePrimitiveActionQueue(delayed ? gate : null);
                check(duel.TryCaptureCardTarget(1, DuelZone.Monster, 1, out var source) &&
                    duel.TryQueueAttack(1, direct ? -1 : 3, out _), "A09 calculation fixture admits attack");
                check(!queue.Trace.Any(e => e.Result?.BattleCalculation is not null), "A09 admission is not calculation");
                for (int i = 0; i < 100 && queue.IsBusy; i++)
                {
                    gate.Release = false;
                    queue.Tick();
                    if (delayed && queue.Active is not null)
                    {
                        var before = ActionTraceJson.Serialize(ActionTraceJson.Capture(queue, "battle"));
                        queue.Tick();
                        check(before == ActionTraceJson.Serialize(ActionTraceJson.Capture(queue, "battle")),
                            "A09 blocked gate cannot repeat calculation");
                        gate.Release = true;
                        queue.Tick();
                    }
                }
                var trace = ActionTraceJson.Capture(queue, "battle");
                var row = trace.Rows.Single(x => x.BattleCalculation is not null);
                var calc = row.BattleCalculation!;
                check(trace.Complete && calc.Attacker.Target == source && calc.Attacker.Slot == 1 &&
                    calc.Attacker.Attack == (direct ? 1350 : 3350) &&
                    calc.Attacker.DefenseValue == (direct ? 1800 : 3800) &&
                    !calc.Attacker.FaceDown && !calc.Attacker.Defense,
                    "A09 retains the exact occurrence and calculated ATK/DEF");
                check(direct
                    ? calc.Defender is null && calc.Difference is null && calc.RequestedDirectDamage == 1350 &&
                      row.Command == nameof(DeclareDirectAttackCommand)
                    : calc.Defender is { Slot: 3, InternalIndex: 77, Attack: 2000, DefenseValue: 1500, FaceDown: false } target &&
                      target.Defense == hidden && calc.Difference == (hidden ? 1850 : 1350) &&
                      calc.RequestedDirectDamage is null &&
                      row.Command == (hidden ? "ResolveRevealedBattleCommand" : nameof(DeclareTargetedAttackCommand)),
                    "A09 records direct or targeted calculation at the actual stage");
                check(trace.Rows.Where(x => x.Command == nameof(DamageCommand) || x.Command == "SnapshotBattleRemovalCommand")
                    .All(x => x.Sequence > row.Sequence), "A09 calculation precedes damage and removal");
                return ActionTraceJson.Serialize(trace);
            }
            check(Run(false) == Run(true), "A09 calculation trace is independent of gate speed");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        Card monster = new(24,24,"Fixture","",(3u<<20)|(4u<<25));
        Card upstart = new(1065,1065,"Upstart","",22u<<20);
        Card sparks = new(342,342,"Sparks","",22u<<20);
        string Pilot(bool delayed)
        {
            var d=DemoDuel.FromPartialObservation(0,[upstart,sparks,monster],new Card?[5],new Card?[5],8000,side0Deck:[monster]);
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(delayed?gate:null);
            void Drain()
            {
                for(int i=0;i<128 && q.IsBusy;i++)
                {
                    gate.Release=false;q.Tick();
                    if(delayed && q.Active is not null)
                    {
                        var before=ActionTraceExport.Serialize(ActionTraceExport.Capture(q,"test"));
                        q.Tick();
                        check(before==ActionTraceExport.Serialize(ActionTraceExport.Capture(q,"test")),
                            "A05.5 blocked gate does not duplicate executed trace");
                        gate.Release=true;q.Tick();
                    }
                }
                check(!q.IsBusy,"A05.5 drain");
            }
            check(d.TryQueueDrawSpell(DuelZone.Hand,0,out _),"A05.5 Upstart admitted");
            check(!ActionTraceExport.Capture(q,"test").Complete,"A05.5 pending trace is incomplete");
            Drain();
            check(d.TryQueueLifePointSpell(DuelZone.Hand,0,out _),"A05.5 Sparks admitted");Drain();
            check(d.TryQueueNormalPlacement(0,0,false,out _),"A05.5 summon admitted");Drain();
            var doc=ActionTraceExport.Capture(q,"test");
            var mapped=doc.Rows.Where(x=>x.Mapping=="mapped").Select(x=>x.Args!).ToArray();
            check(doc.Complete && mapped.Length==4 && mapped[0].SequenceEqual(new ushort[]{0x61,1,1,0}) &&
                mapped[1].SequenceEqual(new ushort[]{0x8042,1000,0,1}) &&
                mapped[2].SequenceEqual(new ushort[]{0x8043,200,0,1}) &&
                mapped[3].SequenceEqual(new ushort[]{0xCE,24,0,1}) &&
                doc.Rows.Any(x=>x.Command=="SummonCommand" && x.Placement is { HandIndex:0, ResourceId:24, FaceDown:false }),
                "A05.6 actual rule producers and captured summon");
            return ActionTraceExport.Serialize(doc);
        }
        check(Pilot(false)==Pilot(true),"A05.6 trace independent of gate speed");
        CheckBattleCalculationTrace(check);
        // Non-zero source and destination positions, duplicate resources, both
        // sides, and a stale target: no resource-id lookup after mutation.
        foreach(int side in new[]{0,1})
        foreach(bool set in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(side,[monster,monster,sparks],new Card?[5],new Card?[5],8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryCaptureCardTarget(side,DuelZone.Hand,1,out var occurrence),"A05.6 source occurrence");
            check(d.TryQueueNormalPlacement(1,3,set,out _),"A05.6 placement admitted");
            for(int i=0;i<100 && q.IsBusy;i++)q.Tick();
            var mapped=ActionTraceProjection.ReadExecuted(q.Trace).Single(x=>x.Mapping=="mapped");
            check(mapped.Args!.SequenceEqual(new ushort[]{(ushort)((side<<15)|0xCE),24,0x301,(ushort)(set?0x100:1)}) &&
                mapped.Placement!.Source==occurrence && d.TryCaptureCardTarget(side,DuelZone.Monster,3,out var target) &&
                target.Occurrence==occurrence.Occurrence,"A05.6 exact CE source/slot/pose and occurrence");
            check(d.TryQueueSpellTrapSet(1,out _),"A05.6 spell set admitted");
            for(int i=0;i<100 && q.IsBusy;i++)q.Tick();
            var cf=ActionTraceProjection.ReadExecuted(q.Trace).Last(x=>x.Mapping=="mapped");
            check(cf.Args!.SequenceEqual(new ushort[]{(ushort)((side<<15)|0xCF),342,0x501,0}),
                "A05.6 CF normal Set uses physical native slot 5");
        }
        var unobserved=ActionTraceProjection.ReadExecuted([
            new(new(1,new SummonCommand(0,new(new(1),0,DuelZone.Hand),0),ActionOrigin.Test),
                ActionTraceStage.Executed,new(CommandOutcome.Applied))]);
        check(unobserved.Single().Mapping=="unmapped","A05.6 never invents missing placement metadata");
        var empty=DemoDuel.FromPartialObservation(1,[],new Card?[5],new Card?[5],8000);
        var eq=empty.CreatePrimitiveActionQueue();eq.Enqueue(new DrawCommand(1));eq.Tick();
        check(ActionTraceExport.Capture(eq,"deckout").Rows.Single().Mapping=="unmapped",
            "A05.5 deckout not counted as successful draw");
        var unsupported=ActionTraceProjection.ReadExecuted([
            new(new(1,new HealCommand(0,70000),ActionOrigin.Test),ActionTraceStage.Executed,new(CommandOutcome.Applied)),
            new(new(2,new DrawCommand(0),ActionOrigin.Test),ActionTraceStage.Executed,new(CommandOutcome.Rejected)),
            new(new(3,new DamageCommand(0,10),ActionOrigin.Test),ActionTraceStage.Admitted)]);
        check(unsupported.Count==2 && unsupported[0].Mapping=="unmapped" && unsupported[1].Mapping=="non_applied",
            "A05.5 out-of-width and rejected commands preserved");
        using var o=new StringWriter();using var e=new StringWriter();
        check(ActionTraceExport.RunPilot([],o,e)==0 && JsonDocument.Parse(o.ToString()).RootElement.GetProperty("complete").GetBoolean(),
            "A05.5 trace pilot CLI emits JSON");
    }
}
