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
