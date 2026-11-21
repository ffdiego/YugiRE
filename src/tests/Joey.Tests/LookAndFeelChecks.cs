using Joey.LookAndFeel;

internal static class LookAndFeelChecks
{
    private sealed class CustomLook : DuelLookAndFeel
    {
        public override string Name => "My Rules";
        public override double Speed => 4;
        protected override TimeSpan BaseDuration(DuelPresentationCue cue) =>
            cue == DuelPresentationCue.FlipEffect ? TimeSpan.FromMilliseconds(800) : base.BaseDuration(cue);
    }

    public static void Run(Action<bool, string> check)
    {
        var classic = new DuelLookAndFeel();
        var fast = new FastLookAndFeel();
        var silent = new SilentLookAndFeel();
        var instant = new InstantLookAndFeel();
        check(classic.Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(500) &&
              fast.Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(250),
              "LookAndFeel: speed profiles scale presentation durations without touching duel state");
        check(silent.Duration(DuelPresentationCue.JoeySpeech) == TimeSpan.Zero &&
              silent.Duration(DuelPresentationCue.FlipEffect) > TimeSpan.Zero &&
              instant.Duration(DuelPresentationCue.Battle) == TimeSpan.Zero,
              "LookAndFeel: disabling speech and animation are independent policies");
        check(new CustomLook().Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(200),
              "LookAndFeel: subclass overrides support per-cue timing changes");
        var tickClock = new PresentationTickClock();
        int splitSteps = Enumerable.Range(0, 27)
            .Sum(_ => tickClock.Advance(TimeSpan.FromMilliseconds(10), classic));
        tickClock.Reset();
        int oneFrameSteps = tickClock.Advance(TimeSpan.FromMilliseconds(270), classic);
        tickClock.Reset();
        int fastSteps = tickClock.Advance(TimeSpan.FromMilliseconds(135), fast);
        check(splitSteps == CardTravel.Updates && oneFrameSteps == splitSteps && fastSteps == splitSteps &&
              classic.UpdatesPerSecond == 30 && fast.UpdatesPerSecond == 60,
              "Presentation ticks: both rates reach the same card travel endpoint independently of frame partitioning");
        check(tickClock.Advance(TimeSpan.FromSeconds(1), instant) == 0,
              "Presentation ticks: Instant skips elapsed animations");
        var sequence = new PresentationSequence<string>();
        sequence.Enqueue("travel", CardTravel.Updates);
        sequence.Enqueue("glow", ActivationEffectPlayback.MaxUpdates);
        sequence.Advance(7);
        check(sequence.Current?.Kind == "travel" && sequence.ElapsedUpdates == 7,
              "Presentation sequence: glow waits until the last movement step");
        sequence.Advance(1);
        check(sequence.Current?.Kind == "glow" && sequence.ElapsedUpdates == 0,
              "Presentation sequence: glow starts at zero when movement finishes");
        sequence.Clear();
        sequence.Enqueue("travel", CardTravel.Updates);
        sequence.Enqueue("glow", ActivationEffectPlayback.MaxUpdates);
        sequence.Advance(12);
        check(sequence.Current?.Kind == "glow" && sequence.ElapsedUpdates == 4,
              "Presentation sequence: a skipped render preserves elapsed time across the FIFO boundary");
        sequence.Advance(int.MaxValue);
        check(sequence.Current is null && sequence.ElapsedUpdates == 0,
              "Presentation sequence: completion discards surplus time");
        sequence.Enqueue("first monster", NativeExplosionTimeline.Updates);
        sequence.Enqueue("second monster", NativeExplosionTimeline.Updates);
        sequence.Advance(NativeExplosionTimeline.Updates - 1);
        check(sequence.Pending.Count() == 2 && sequence.Current?.Kind == "first monster" &&
              NativeExplosionTimeline.PassesAt(sequence.ElapsedUpdates * NativeExplosionTimeline.UpdateIncrement) ==
                  NativeExplosionPasses.Fragments,
              "Destruction FIFO: second monster is still pending in the first monster's final sample");
        sequence.Advance(1);
        check(sequence.Pending.Count() == 1 && sequence.Current?.Kind == "second monster" &&
              sequence.ElapsedUpdates == 0,
              "Destruction FIFO: second monster starts after the first finishes");
        check(NativeDuelLayout.GraveBounds(true).X > NativeDuelLayout.MonsterBounds(4, true).X &&
              NativeDuelLayout.GraveBounds(false).X < NativeDuelLayout.MonsterBounds(4, false).X &&
              NativeDuelLayout.GraveBounds(true).Y == NativeDuelLayout.MonsterBounds(4, true).Y &&
              NativeDuelLayout.GraveBounds(false).Y == NativeDuelLayout.MonsterBounds(4, false).Y &&
              DuelCardLayout.Center(DuelCardZone.Field, true) == new CardPoint(286, 353) &&
              DuelCardLayout.Center(DuelCardZone.Field, false) == new CardPoint(766, 236),
              "Duel layout: graveyards align with the monster rows opposite their field spell");
        var clock = new DuelPresentationClock(classic);
        clock.Show(DuelPresentationCue.FlipReveal);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        check(clock.Busy && clock.Remaining == TimeSpan.FromMilliseconds(350) &&
              clock.ActiveCue == DuelPresentationCue.FlipReveal,
              "LookAndFeel: cue clock advances by frame delta without sleeps");
        clock.Advance(TimeSpan.FromSeconds(10));
        check(!clock.Busy && clock.ActiveCue is null,
              "LookAndFeel: cue clock completes without requiring a timer thread");
        clock.SetStyle(instant);
        clock.Show(DuelPresentationCue.FlipReveal);
        check(!clock.Busy && clock.Remaining == TimeSpan.Zero,
              "LookAndFeel: instant policy is immediate");
    }
}
