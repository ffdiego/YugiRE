using Joey.Core;
using Joey.LookAndFeel;

internal static class CommandScopedPresentationChecks
{
    private sealed class Gate(DemoDuel d) : ICommandPresentationGate
    {
        private readonly CommandPresentationSteps<string> steps = new();
        private readonly ActiveCommandPresentation<DuelActionPresentation> active = new();
        private readonly NativeLifePointPresentation[] lp = [new(d.Life[0]),new(d.Life[1])];
        public List<long> Finished { get; } = [];
        public List<ActionPresentationKind> Previews { get; } = [];
        public List<long> Transfers { get; } = [];
        public bool CanAdvance => !Busy;
        private bool Busy => (steps.Sequence is long seq && steps.For(seq).Current is not null) ||
            Enumerable.Range(0,2).Any(p => lp[p].Animating ||
                lp[p].Displayed != NativeLifePointPresentation.ClampToNativeDisplay(d.Life[p]));
        public void OnStarting(QueuedDuelCommand c)
        {
            steps.Begin(c.Sequence);
            active.Capture(c.Sequence,d.DescribeActionPresentation(c.Command));
        }
        public void Transfer(DemoHandTransfer move)
        {
            long seq=steps.Sequence ?? throw new Exception("No active command for transfer.");
            Transfers.Add(seq);
            steps.For(seq).Enqueue("transfer",CardTravel.Updates);
        }
        public bool CanComplete(QueuedDuelCommand c,CommandResult r)
        {
            if (!active.TryRelease(c.Sequence,r.Outcome==CommandOutcome.Applied, a =>
                {
                    Previews.Add(a.Kind);
                    steps.For(c.Sequence).Enqueue("preview",CardTravel.Updates+ActivationEffectPlayback.MaxUpdates);
                },()=>Busy)) return false;
            steps.Release(c.Sequence);
            Finished.Add(c.Sequence);
            return true;
        }
        public void Update()
        {
            if (steps.Sequence is long seq && steps.For(seq).Current is not null)
            { steps.For(seq).Advance(1); return; }
            for(int p=0;p<2;p++)
            { lp[p].SetLogicalLife(d.Life[p]); lp[p].AdvanceNativeUpdate(); }
        }
    }

    public static void Run(Action<bool,string> check)
    {
        var owned = new CommandPresentationSteps<int>();
        owned.Begin(1); owned.For(1).Enqueue(7,2);
        bool bad=false;try{owned.Begin(2);}catch(InvalidOperationException){bad=true;}
        check(bad,"A03.2 refuses cross-command visual overlap");
        bad=false;try{owned.Release(1);}catch(InvalidOperationException){bad=true;}
        check(bad,"A03.2 cannot release unfinished substeps");
        owned.For(1).Advance(2);owned.Release(1);owned.Begin(2);
        check(owned.For(2).Current is null,"A03.2 next command begins with empty visual scope");
        owned.Release(2);

        Card low=new(24,24,"Fixture","",(3u<<20)|(4u<<25));
        Card upstart=new(1065,1065,"Upstart","",22u<<20);
        Card sparks=new(342,342,"Sparks","",22u<<20);
        Card?[] empty=new Card?[5];
        var d=DemoDuel.FromPartialObservation(0,[upstart,sparks],empty,empty,8000,side0Deck:[low]);
        var gate=new Gate(d);d.HandTransferred+=gate.Transfer;
        var q=d.CreatePrimitiveActionQueue(gate);
        check(d.TryQueueDrawSpell(DuelZone.Hand,0,out _),"A03.2 Upstart admitted");
        q.Tick();check(d.Hand.Count==2 && d.Life[1]==8000 && gate.Previews.Count==1,
            "A03.2 activation preview before source spending");
        void Drain()
        {
            for(int i=0;i<1000 && q.IsBusy;i++){q.Tick();gate.Update();}
            check(!q.IsBusy,"A03.2 gate drains all visual substeps");
        }
        Drain();
        check(d.Hand.Count==2 && d.Life[1]==9000 && gate.Transfers.Count==1 &&
            gate.Transfers.Single()==q.Trace.Single(t=>t.Stage==ActionTraceStage.Executed && t.Entry.Command is DrawCommand).Entry.Sequence,
            "A03.2 draw event belongs to its DrawCommand and LP completes");
        check(d.TryQueueLifePointSpell(DuelZone.Hand,0,out _),"A03.2 Sparks admitted");
        Drain();
        check(d.Life[1]==8800 && gate.Previews.Count==2 &&
            gate.Finished.SequenceEqual(q.Trace.Where(t=>t.Stage==ActionTraceStage.Completed).Select(t=>t.Entry.Sequence)),
            "A03.2 one completed visual scope per completed command");
    }
}
