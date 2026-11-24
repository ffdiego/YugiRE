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
