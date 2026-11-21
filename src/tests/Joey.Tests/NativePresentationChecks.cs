using Joey.LookAndFeel;

internal static class NativePresentationChecks
{
    public static void Run(Action<bool, string> check)
    {
        check(DuelSurfaceLayout.Board == new CardBounds(230, 0, 570, 600) &&
              Enumerable.Range(0, DuelSurfaceLayout.PhaseCount).Select(DuelSurfaceLayout.PhaseButton)
                  .SequenceEqual(new[] { 82, 157, 232, 307, 382, 457 }
                      .Select(y => new CardBounds(221, y, 40, 75))),
              "Native duel surface: full backdrop and six consecutive phase cells");
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
        foreach (bool near in new[] { true, false })
        foreach (bool defense in new[] { false, true })
        foreach (bool hidden in new[] { false, true })
        {
            var card = DuelCardLayout.OnBoard(DuelCardZone.Monster, near, 2, defense, hidden);
            check(card.Degrees == (near ? 0 : 180) + (defense ? 90 : 0) &&
                  card.FaceDown == hidden &&
                  card.Bounds.Width == (defense ? 72 : 50) && card.Bounds.Height == (defense ? 50 : 72) &&
                  card.AttributesCenter == (hidden ? null : new CardPoint(525, near ? 398 : 202)),
                  $"Native card geometry: near={near}, defense={defense}, hidden={hidden}");
        }
        check(DuelCardLayout.Center(DuelCardZone.Spell, true, 0) == new CardPoint(369, 460) &&
              DuelCardLayout.Center(DuelCardZone.Spell, false, 4) == new CardPoint(369, 140),
              "Native card geometry: Spell/Trap uses the native second row");
        bool badZone = false;
        try { NativeDuelLayout.MonsterCenter(5, true); }
        catch (ArgumentOutOfRangeException) { badZone = true; }
        check(badZone, "Native presentation: out-of-range zone is rejected");

        check(HandPresentation.CenterX(0, 6) == 319 &&
              HandPresentation.CenterX(5, 6) == 731 &&
              HandPresentation.CenterX(0, 1) == 521 &&
              HandPresentation.Approach(600, 568) == 592,
              "Native presentation: hand fan and smoothing moved out of Joey.Core");
        check(HandPresentation.Bounds(0, 5, true).Y == 528 &&
              HandPresentation.Bounds(0, 5, false).Y == -72 &&
              HandPresentation.Bounds(0, 5, true, 32).Y == 496 &&
              Enumerable.Range(0, 12).All(i => HandPresentation.CenterX(i, 12, true) +
                  HandPresentation.CenterX(i, 12, false) == 1050) &&
              Enumerable.Range(0, 12).Select(i => HandPresentation.CenterX(i, 12)).Distinct().Count() == 12,
              "Both hands use the native side anchors and fit all cards in the fan");
        var fiveCpuCards = Enumerable.Range(0, 5)
            .Select(i => HandPresentation.Bounds(i, 5, nearSide: false)).Reverse().ToArray();
        check(fiveCpuCards.Select(card => card.X).SequenceEqual(new[] { 269, 372, 475, 578, 681 }) &&
              fiveCpuCards.Zip(fiveCpuCards.Skip(1), (left, right) => right.X - left.X - left.Width)
                  .All(gap => gap == 3) &&
              HandPresentation.CenterX(1, 6) - HandPresentation.CenterX(0, 6) < HandPresentation.CardWidth,
              "Hand rendering: five opponent cards have visible gaps; six overlap at the native span cap");
        check(new DuelLookAndFeel().MoveCardAxis(600, 568) == 592 &&
              new FastLookAndFeel().MoveCardAxis(600, 568) == 592 &&
              new SilentLookAndFeel().MoveCardAxis(600, 568) == 592 &&
              new InstantLookAndFeel().MoveCardAxis(600, 568) == 568,
              "Native presentation: Classic and Fast share one native hand update step");

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
