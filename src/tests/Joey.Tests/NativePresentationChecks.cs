using Joey.LookAndFeel;

internal static class NativePresentationChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Coordinates are native centers in the logical 800x600 canvas.
        check(NativeDuelLayout.MonsterCenter(0, true) == new CardPoint(369, 352) &&
              NativeDuelLayout.MonsterCenter(4, true) == new CardPoint(681, 352) &&
              NativeDuelLayout.MonsterCenter(0, false) == new CardPoint(681, 248) &&
              NativeDuelLayout.MonsterCenter(4, false) == new CardPoint(369, 248),
              "Native presentation: five monster slot centers mirror across players");
        var rect = NativeDuelLayout.MonsterBounds(2, true);
        check(rect == new CardBounds(492, 308, 66, 88) &&
              NativeDuelLayout.CanvasWidth == 800 && NativeDuelLayout.CanvasHeight == 600,
              "Native presentation: native centers and demo-sized rect remain distinct");
        bool badZone = false;
        try { NativeDuelLayout.MonsterCenter(5, true); }
        catch (ArgumentOutOfRangeException) { badZone = true; }
        check(badZone, "Native presentation: out-of-range zone is rejected");

        check(HandPresentation.CenterX(0, 6) == 366 &&
              HandPresentation.CenterX(5, 6) == 684 &&
              HandPresentation.CenterX(0, 1) == 521 &&
              HandPresentation.Approach(600, 568) == 592,
              "Native presentation: hand fan and smoothing moved out of Joey.Core");
        check(new DuelLookAndFeel().MoveCardAxis(600, 568) == 592 &&
              new FastLookAndFeel().MoveCardAxis(600, 568) == 586 &&
              new SilentLookAndFeel().MoveCardAxis(600, 568) == 592 &&
              new InstantLookAndFeel().MoveCardAxis(600, 568) == 568,
              "Native presentation: profile overrides change motion without changing rules");

        var activation = new NativeActivationTimeline();
        // Synthetic lookup for deterministic arithmetic tests. Do NOT claim
        // this is the actual native DAT_00A52EE8 table.
        var cosine = new int[512];
        cosine[0] = 65536;
        cosine[384] = 0;
        var initial = activation.Sample(0, cosine);
        check(initial.Counter == 0 && initial.Lower.ResourceIndex == 1 &&
              initial.Upper.ResourceIndex == 0 &&
              initial.Lower.Opacity == 0 && initial.Scratch.Opacity == 0,
              "Native activation: initial blue frame uses bottom/scratch/top in that order");
        for (int i = 0; i < 8; i++) activation.AdvanceNativeUpdate();
        var peak = activation.Sample(1, cosine);
        check(activation.Counter == 128 && activation.Active &&
              peak.Lower == new NativeActivationPass(NativeActivationLayer.LowerSprite, 3, 64, 65536, 255) &&
              peak.Scratch == new NativeActivationPass(NativeActivationLayer.BlackScratch, -1, 0, 65536, 128) &&
              peak.Upper == new NativeActivationPass(NativeActivationLayer.UpperSprite, 2, 128, 65536, 255),
              "Native activation: t=128 uses original three-pass indices, scale, rotation and alpha");
        for (int i = 0; i < 8; i++) activation.AdvanceNativeUpdate();
        check(activation.Counter == 256 && activation.Active,
              "Native activation: counter 256 is still live after sixteen updates");
        activation.AdvanceNativeUpdate();
        check(activation.Counter == 272 && !activation.Active,
              "Native activation: 17th update deactivates at counter 272, not 17 proven frames");
        activation.AdvanceNativeUpdate();
        check(activation.Counter == 272, "Native activation: disabled object stops updating");
        bool invalidCounter = false, invalidLookup = false;
        try { activation.Sample(0, cosine); }
        catch (ArgumentOutOfRangeException) { invalidCounter = true; }
        try { NativeActivationFrame.FromCounter(0, 0, ReadOnlySpan<int>.Empty); }
        catch (ArgumentException) { invalidLookup = true; }
        check(invalidCounter && invalidLookup,
              "Native activation: invalid post-completion draws and missing cosine tables fail closed");
        activation.Reset();
        check(activation.Active && activation.Counter == 0,
              "Native activation: reset permits repeatable playback");

        var explosion = new NativeExplosionTimeline();
        check(explosion.VisiblePasses() == NativeExplosionPasses.Initial,
              "Native explosion: initial pass exists at counter zero");
        for (int i = 0; i < 4; i++) explosion.AdvanceNativeUpdate();
        check(explosion.Counter == 32 &&
              explosion.VisiblePasses() == (NativeExplosionPasses.Initial | NativeExplosionPasses.Flare),
              "Native explosion: t=32 enters overlapping flare and initial passes");
        for (int i = 0; i < 12; i++) explosion.AdvanceNativeUpdate();
        check(explosion.Counter == 128 &&
              explosion.VisiblePasses() == (NativeExplosionPasses.Flare | NativeExplosionPasses.Fragments),
              "Native explosion: t=128 overlaps flare and fragments");
        for (int i = 0; i < 10; i++) explosion.AdvanceNativeUpdate();
        explosion.AfterNativeDraw();
        check(explosion.Counter == 208 && !explosion.Active &&
              explosion.VisiblePasses() == NativeExplosionPasses.None,
              "Native explosion: deactivation at draw after t=207, not a fixed duration in ms");

        check(NativeVisualDispatch.Participates(true, 0b0101, 0b0001) &&
              !NativeVisualDispatch.Participates(false, 0b0101, 0b0001) &&
              !NativeVisualDispatch.Participates(true, 0b0100, 0b0001) &&
              NativeVisualDispatch.CompareLayer(0x110, 0x120) < 0,
              "Native dispatcher: active mask intersection and ascending layer sort key");
    }
}
