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
        check(NativePhaseTurnFlip.Updates == 64 &&
              NativePhaseTurnFlip.Frame(0, 7) is null &&
              NativePhaseTurnFlip.Frame(0, 8) == 8 &&
              NativePhaseTurnFlip.Frame(0, 12) == 4 &&
              NativePhaseTurnFlip.Frame(0, 16) == 0,
              "Phase turn flip: native counter, initial delay and reversed atlas order");
        check(Enumerable.Range(0, DuelSurfaceLayout.PhaseCount).All(button =>
                  NativePhaseTurnFlip.Frame(button, 7 + button * 3) is null &&
                  NativePhaseTurnFlip.Frame(button, 8 + button * 3) == 8 &&
                  NativePhaseTurnFlip.Frame(button, 16 + button * 3) == 0) &&
              NativePhaseTurnFlip.Frame(5, 63) == 0,
              "Phase turn flip: each button starts three updates later and holds its final face");
        check(NativePhaseNotice.BattleFrame(7) is null &&
              Enumerable.Range(8, 14).Select(NativePhaseNotice.BattleFrame)
                  .SequenceEqual(Enumerable.Range(0, 7).SelectMany(i => new int?[] { i, i })) &&
              NativePhaseNotice.BattleFrame(22) is null,
            "Battle banner: seven native sprites appear for two updates each in the slowed window");
        var phaseMotionCosine = ActivationEffectPlayback.ApproximateCosineTable();
        check(NativePhaseNotice.Motion(0, false, phaseMotionCosine) == (64, 0) &&
              NativePhaseNotice.Motion(4, false, phaseMotionCosine) == (45, 96) &&
              NativePhaseNotice.Motion(8, false, phaseMotionCosine) == (0, 255) &&
              NativePhaseNotice.Motion(17, false, phaseMotionCosine) == (0, 254) &&
              NativePhaseNotice.Motion(25, true, phaseMotionCosine) ==
                  NativePhaseNotice.Motion(17, false, phaseMotionCosine) &&
              NativePhaseNotice.Motion(32, false, phaseMotionCosine) == (0, 0),
            "Phase banner: mirrored native entrance, center hold and cosine fade");
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

        var repeated = new object();
        var other = new object();
        foreach (bool near in new[] { true, false })
        {
            var fan = new HandReflow<object>();
            fan.Synchronize([repeated], near, 0, animate: true);
            var first = fan.Bounds(0, 1);
            fan.Synchronize([repeated, other, repeated], near, 0, animate: true);
            var rightCopy = fan.Bounds(2, 3);
            check(fan.Bounds(0, 3) == first && rightCopy == HandPresentation.Bounds(2, 3, near),
                "Hand reflow: an arriving card lands in its new slot while survivors keep their last positions");
            fan.RemoveAt(0);
            fan.Synchronize([other, repeated], near, 1, animate: true);
            var target = HandPresentation.Bounds(1, 2, near);
            check(fan.Bounds(1, 2).X == HandPresentation.Approach(rightCopy.X, target.X),
                "Hand reflow: removing the first copy preserves the position of the other copy");
            fan.Synchronize([other, repeated], near, 64, animate: true);
            check(fan.Bounds(1, 2) == target, "Hand reflow: repeated native updates reach the new fan");
            fan.Synchronize([repeated], near, 0, animate: false);
            check(fan.Bounds(0, 1) == first, "Hand reflow: Instant reaches the new fan without any updates");
        }

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

        var explosionCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var initialExplosion = NativeExplosionFrame.FromCounter(0, explosionCosine);
        var layeredExplosion = NativeExplosionFrame.FromCounter(32, explosionCosine);
        var fragmentStart = NativeExplosionFrame.FromCounter(128, explosionCosine);
        var fragmentEnd = NativeExplosionFrame.FromCounter(184, explosionCosine);
        check(initialExplosion == new NativeExplosionFrame(null, null, new(0, 0, 32768, 181)) &&
              initialExplosion.Initial?.ScaledPixels(164) == 83 &&
              layeredExplosion.Flare == new NativeEffectSpritePass(1, 0, 32768, 128) &&
              layeredExplosion.Initial is { ResourceIndex: 0, RotationIndex: 32, ScaleFixed16: 40960 },
              "Native explosion: centered scaled initial and flare passes use native rotation and opacity");
        check(fragmentStart.Flare is { ResourceIndex: 1 } &&
              fragmentStart.Fragments == new NativeEffectSpritePass(2, 0, 65536, 255) &&
              fragmentStart.Initial is null &&
              fragmentEnd.Fragments is { ResourceIndex: 6 } &&
              fragmentEnd.Fragments?.ScaledPixels(196) == 196 &&
              NativeExplosionFrame.FromCounter(192, explosionCosine) == default &&
              NativeExplosionFrame.FromCounter(200, explosionCosine) == default,
              "Native explosion: fragment slots 2–6, then two sprite-free updates before deactivation");
        var from = new CardPoint(369, 352);
        var to = new CardPoint(681, 248);
        NativeAttackFrame AttackAt(int update) => NativeAttackFrame.Sample(update, from, to, true, explosionCosine);
        check(AttackAt(0).Position == from && AttackAt(0).RotationIndex == 0 &&
              AttackAt(8).Position == from && AttackAt(8).RotationIndex > 0 &&
              AttackAt(9).Position.X > from.X && AttackAt(15).Position.X < to.X &&
              !AttackAt(15).PartialSword, "Native attack: turn at the origin, then advance with the full sword");
        check(NativeAttackFrame.SwordAnchor(0, true) == from && NativeAttackFrame.SwordAnchor(0, false) == to &&
              NativeAttackFrame.Sample(0, from, to, true, explosionCosine, alreadyAimed: true) == AttackAt(8) &&
              NativeAttackFrame.Sample(9, from, to, true, explosionCosine, alreadyAimed: true) == AttackAt(9),
              "Client attack: a confirmed aim retains position and angle as it hands off to the flight");
        check(AttackAt(16) is { PartialSword: true, Position: { X: 681, Y: 248 }, ImpactScaleFixed16: 0, Opacity: 255 } &&
              AttackAt(17) is { PartialSword: true, ImpactScaleFixed16: 8192, Opacity: 255 } &&
              AttackAt(31) is { PartialSword: true, ImpactScaleFixed16: 122880, Opacity: 32 } &&
              NativeAttackFrame.Updates == 32,
              "Native attack: the partial sword appears at contact and the growing impact fades with it");
        check(NativeAttackFrame.DirectTarget(true) == new CardPoint(525, 64) &&
              NativeAttackFrame.DirectTarget(false) == new CardPoint(525, 536) &&
              NativeAttackFrame.Sample(8, to, from, false, explosionCosine).RotationIndex ==
                  AttackAt(8).RotationIndex + 256,
              "Native attack: opposite side and direct target coordinates");
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
