using Joey.Core;
using Joey.LookAndFeel;

internal static class ActionPresentationGateChecks
{
    private sealed class Gate(DemoDuel duel, DuelLookAndFeel style) : ICommandPresentationGate
    {
        private readonly ActiveCommandPresentation<DuelActionPresentation> active = new();
        private readonly PresentationTickClock clock = new();
        private int remaining;
        public List<ActionPresentationKind> Started { get; } = [];
        public bool CanAdvance => remaining == 0;
        public void OnStarting(QueuedDuelCommand command) =>
            active.Capture(command.Sequence, duel.DescribeActionPresentation(command.Command));
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            active.TryRelease(command.Sequence, result.Outcome == CommandOutcome.Applied, cue =>
            {
                Started.Add(cue.Kind);
                remaining = !style.PlayAnimations ? 0 : cue.Kind switch
                {
                    ActionPresentationKind.Activate => CardTravel.Updates + ActivationEffectPlayback.MaxUpdates,
                    ActionPresentationKind.Summon => CardTravel.Updates + NativeNormalSummonFrame.Updates,
                    _ => 0
                };
                clock.Reset();
            }, () => remaining > 0);
        public void Advance(TimeSpan elapsed) => remaining = Math.Max(0, remaining - clock.Advance(elapsed, style));
    }
    public static void Run(Action<bool,string> check)
    {
        Card monster = new(24,24,"Fixture","",(3u << 20) | (4u << 25));
        Card spell = new(336,336,"Raigeki","",22u << 20);
        (ActionTraceEntry[] Trace, ActionPresentationKind[] Cues, int Frames) Pilot(DuelLookAndFeel style)
        {
            var d = DemoDuel.FromPartialObservation(0,[spell,monster],new Card?[5],
                [monster,null,null,null,null],8000);
            var gate = new Gate(d,style);
            var queue = d.CreatePrimitiveActionQueue(gate);
            int frames = 0;
            void Drain()
            {
                for(int i=0;i<1000 && queue.IsBusy;i++)
                {
                    queue.Tick();
                    gate.Advance(TimeSpan.FromMilliseconds(34));
                    frames++;
                }
                check(!queue.IsBusy,"A03 gate drains");
            }
            check(d.TryQueueRaigekiFromHand(0,out _),"A03 activation admitted");
            queue.Tick();
            check(d.Hand.Count == 2 && d.Monsters[1,0] is not null &&
                  gate.Started.SequenceEqual([ActionPresentationKind.Activate]),
                  "A03 preview begins before source spend/destruction");
            if(style.PlayAnimations)
            {
                for(int i=0;i<3;i++) queue.Tick();
                check(d.Hand.Count == 2 && d.Monsters[1,0] is not null && gate.Started.Count == 1,
                    "A03 stalled preview does not repeat or resolve effect");
            }
            Drain();
            check(d.Hand.Count == 1 && d.Monsters[1,0] is null,"A03 wipe after presentation");
            check(d.TryQueueNormalPlacement(0,2,false,out _),"A03 normal summon admitted");
            Drain();
            check(d.Monsters[0,2] == monster && d.Hand.Count == 0 &&
                  gate.Started.SequenceEqual([ActionPresentationKind.Activate,ActionPresentationKind.Summon]),
                  "A03 one preview and one summon cue");
            return (queue.Trace.ToArray(),gate.Started.ToArray(),frames);
        }
        var classic=Pilot(new DuelLookAndFeel());
        var fast=Pilot(new FastLookAndFeel());
        var silent=Pilot(new SilentLookAndFeel());
        var instant=Pilot(new InstantLookAndFeel());
        check(classic.Trace.SequenceEqual(fast.Trace) && classic.Trace.SequenceEqual(silent.Trace) &&
            classic.Trace.SequenceEqual(instant.Trace) && classic.Cues.SequenceEqual(instant.Cues) &&
            fast.Frames < classic.Frames && instant.Frames < fast.Frames,
            "A03 profiles change only elapsed presentation, not commands or cues");
    }
}
