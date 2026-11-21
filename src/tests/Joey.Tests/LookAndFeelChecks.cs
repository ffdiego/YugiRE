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
              DuelCardLayout.FrameCenter(NativeDuelLayout.GraveBounds(true)) == new CardPoint(760, 366) &&
              DuelCardLayout.FrameCenter(NativeDuelLayout.GraveBounds(false)) == new CardPoint(290, 234) &&
              DuelCardLayout.Center(DuelCardZone.Field, true) == new CardPoint(286, 353) &&
              DuelCardLayout.Center(DuelCardZone.Field, false) == new CardPoint(766, 236),
              "Duel layout: graveyards align with the monster rows opposite their field spell");
        // Normal Summon: 0044F9F0 increments by 8 through t=256,
        // and 0044FA10 has different rendering branches either side of 64.
        var summonCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var summonStart = NativeNormalSummonFrame.FromUpdate(0, summonCosine);
        var summonLastExpand = NativeNormalSummonFrame.FromUpdate(8, summonCosine);
        var summonFirstWrap = NativeNormalSummonFrame.FromUpdate(9, summonCosine);
        var summonEnd = NativeNormalSummonFrame.FromUpdate(32, summonCosine);
        check(NativeNormalSummonFrame.Updates == 33 &&
              summonStart.Counter == 0 && summonEnd.Counter == 256 &&
              summonStart.Stage == NativeNormalSummonStage.ExpandMiniature &&
              summonStart.ScaleFixed16 == 0x4000 &&
              summonLastExpand.Counter == 64 && summonLastExpand.ScaleFixed16 == 0x8000 &&
              summonFirstWrap.Counter == 72 &&
              summonFirstWrap.Stage == NativeNormalSummonStage.WrappedMiniature,
              "Normal summon: native 33 updates, initial scale and branch at t=64/72");
        check(summonStart.Opacity > summonLastExpand.Opacity &&
              summonFirstWrap.Opacity > 0 &&
              NativeNormalSummonFrame.FromUpdate(20, summonCosine).Opacity >
                  summonFirstWrap.Opacity &&
              summonFirstWrap.WrappedSourceOffset(72) == 45 &&
              summonEnd.WrappedSourceOffset(72) == 16,
              "Normal summon: cosine intensities and 5/8 scroll wrapping use native counters");
        bool invalidSummonSampleRejected = false;
        try { NativeNormalSummonFrame.FromUpdate(33, summonCosine); }
        catch (ArgumentOutOfRangeException) { invalidSummonSampleRejected = true; }
        check(invalidSummonSampleRejected,
              "Normal summon: never sample deactivated native counter t=264");
        var summonQueue = new PresentationSequence<string>();
        summonQueue.Enqueue("hand flight", CardTravel.Updates);
        summonQueue.Enqueue("normal summon", NativeNormalSummonFrame.Updates);
        summonQueue.Advance(CardTravel.Updates);
        check(summonQueue.Current?.Kind == "normal summon" &&
              summonQueue.ElapsedUpdates == 0,
              "Normal summon: card landing precedes native summon visual timeline");

        var setCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var setStart = NativeSetFrame.FromUpdate(0, setCosine);
        var setMid = NativeSetFrame.FromUpdate(11, setCosine);
        var setEnd = NativeSetFrame.FromUpdate(21, setCosine);
        check(NativeSetFrame.Updates == 22 && setStart.Counter == 0 &&
              setEnd.Counter == 252 && setStart.ScaleFixed16 == 65536 &&
              setMid.ScaleFixed16 == (132 << 9) &&
              setStart.MainOpacity == 0 && setMid.MainOpacity > 0 &&
              setMid.Particles.Count == NativeSetFrame.ParticleCount &&
              setStart.Particles.Count == 1 &&
              setStart.Particles[0] == new NativeSetParticle(0, 0, 0),
              "Native Set: 0044F370 counter, cosine fade, scale and staggered launches");
        check(setMid.Particles[1].Variant == 0 &&
              setMid.Particles[0].RisePixels > 0 && setEnd.MainOpacity < setMid.MainOpacity,
              "Native Set: alternating particles rise quadratically; X origin awaits native constructor");
        // 0044EFD0: eight MT-derived radii on the 8-way cosine ring,
        // at offsets obj+104h..140h. Test independent of random MT state.
        int[] fixedRadii = Enumerable.Repeat(32, NativeSetFrame.ParticleCount).ToArray();
        var origin = new CardPoint(400, 300);
        var setOrigins = NativeSetFrame.ParticleOrigins(origin, fixedRadii, setCosine);
        check(setOrigins.Length == 8 &&
              setOrigins[0] == new CardPoint(432, 300) &&
              setOrigins[2] == new CardPoint(400, 332) &&
              setOrigins[4] == new CardPoint(368, 300) &&
              setOrigins[6] == new CardPoint(400, 268),
              "Set constructor: eight origins form a native 8-way cosine ring around the card");
        fixedRadii[0] = NativeSetFrame.MinimumRadius;
        fixedRadii[4] = NativeSetFrame.MaximumRadius;
        setOrigins = NativeSetFrame.ParticleOrigins(origin, fixedRadii, setCosine);
        check(setOrigins[0] == new CardPoint(416, 300) &&
              setOrigins[4] == new CardPoint(353, 300),
              "Set constructor: MT low five bits select native radius 16..47");
        bool rejected = false;
        try
        {
            NativeSetFrame.ParticleOrigins(origin, new int[7], setCosine);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Set constructor: fail closed on an incomplete list of native radii");

        var setQueue = new PresentationSequence<string>();
        setQueue.Enqueue("travel", CardTravel.Updates);
        setQueue.Enqueue("set", NativeSetFrame.Updates);
        setQueue.Advance(CardTravel.Updates);
        check(setQueue.Current?.Kind == "set" && setQueue.ElapsedUpdates == 0,
              "Set placement: effect starts after hand-to-field flight");

        // 0047C660 uses displayed (not logical) LP for the native bar,
        // saturating at 8000 even though the numeric display reaches 65535.
        check(NativeLifeHud.FilledBarPixels(0, 137) == 0 &&
              NativeLifeHud.FilledBarPixels(4000, 137) == 68 &&
              NativeLifeHud.FilledBarPixels(7999, 137) == 136 &&
              NativeLifeHud.FilledBarPixels(8000, 137) == 137 &&
              NativeLifeHud.FilledBarPixels(65535, 137) == 137 &&
              NativeLifeHud.FilledBarPixels(-100, 137) == 0,
              "Native LP HUD: bar uses truncated displayed/8000 ratio and saturates");
        check(NativeLifeHud.DecimalGlyphs(0).SequenceEqual([0]) &&
              NativeLifeHud.DecimalGlyphs(8000).SequenceEqual([8, 0, 0, 0]) &&
              NativeLifeHud.DecimalGlyphs(int.MaxValue).SequenceEqual([6, 5, 5, 3, 5]),
              "Native LP HUD: decimal digits use visual clamp to 65535");

        string fontManifest = "duel_fontl.bmp 10\\n" +
            "j/duel/font/duel_fontm_y.bmp 12 // sprite count\\n" +
            "duel_fonts.bmp 8\\n";
        check(NativeLifeHud.TryFontColumns(fontManifest, "DUEL_FONTM_Y.BMP") == 12 &&
              NativeLifeHud.TryFontColumns(fontManifest, "duel_fontl.bmp") == 10 &&
              NativeLifeHud.TryFontColumns(fontManifest, "missing.bmp") is null &&
              NativeLifeHud.TryFontColumns("duel_fontm_y.bmp ?\\n", "duel_fontm_y.bmp") is null &&
              NativeLifeHud.GlyphWidth(240, 12) == 20,
              "Native LP HUD: glyph columns derive from DAT manifest rather than guessed image width");
        bool invalidGlyphAtlasRejected = false;
        try { NativeLifeHud.GlyphWidth(239, 12); }
        catch (ArgumentException) { invalidGlyphAtlasRejected = true; }
        check(invalidGlyphAtlasRejected,
              "Native LP HUD: reject an invalid atlas width instead of slicing wrong glyphs");

        // 0047C4F0/0047C560: Core LP updates immediately, but the native
        // visual counter has a 32-update ramp before consuming signed deltas.
        var lp = new NativeLifePointPresentation(8000);
        lp.SetLogicalLife(7750);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates; i++)
        {
            lp.SetLogicalLife(7750); // no restart on unchanged logical target
            lp.AdvanceNativeUpdate();
        }
        check(lp.Displayed == 8000 && lp.PendingDelta == -250 &&
              lp.RampCounter == NativeLifePointPresentation.RampEnd,
              "Native LP: a delta is held throughout 32 Update ramp samples");
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7900 && lp.PendingDelta == -150,
              "Native LP: first damage display update subtracts 100");
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7800 && lp.PendingDelta == -50,
              "Native LP: second damage display update subtracts another 100");
        for (int i = 0; i < 5; i++) lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7750 && lp.PendingDelta == 0 && lp.FlashCounter > 0,
              "Native LP: the remaining 50 LP is displayed in five -10 steps");

        var healing = new NativeLifePointPresentation(1000);
        healing.SetLogicalLife(1205);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates + 7; i++)
            healing.AdvanceNativeUpdate();
        check(healing.Displayed == 1205 && healing.PendingDelta == 0,
              "Native LP: healing uses +100/+10/+1 visual steps");

        var retargeted = new NativeLifePointPresentation(8000);
        retargeted.SetLogicalLife(7000);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates + 1; i++)
            retargeted.AdvanceNativeUpdate();
        retargeted.SetLogicalLife(7900);
        check(retargeted.Displayed == 7900 && retargeted.PendingDelta == 0 &&
              retargeted.RampCounter == 0,
              "Native LP: a second logical change retargets from currently displayed LP");
        retargeted.SetLogicalLife(200000, animate: false);
        check(retargeted.Displayed == 65535 && retargeted.PendingDelta == 0 &&
              retargeted.RampCounter == 256,
              "Native LP: Instant presentation jumps to target and clips only the display to 16-bit");
        retargeted.SetLogicalLife(0, animate: false);
        check(retargeted.Displayed == 0 && !retargeted.Animating,
              "Native LP: zero reaches the screen without altering Core win conditions");

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
