using Joey.LookAndFeel;

internal static class NativeDisabledChainChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (int side in new[] { 0, 1 })
        foreach (int perspective in new[] { 0, 1 })
        foreach (int zone in Enumerable.Range(0, 11))
        foreach (bool rotated in new[] { false, true })
        {
            var target = NativeDisabledChainTarget.FromField(side, zone, rotated);
            var roundTrip = new NativeDisabledChainTarget(target.Descriptor);
            var expectedZone = zone < 5 ? DuelCardZone.Monster : zone < 10 ? DuelCardZone.Spell : DuelCardZone.Field;
            check(roundTrip == target && target.Side == side && target.Region + target.Slot == zone &&
                  target.Rotated == rotated &&
                  target.Center(perspective) == DuelCardLayout.Center(expectedZone, side == perspective, zone % 5),
                "V12: field descriptor, perspective and region center");
            var frame = NativeDisabledChainFrame.FromCounter(23, target.Descriptor);
            var center = target.Center(perspective);
            var bounds = frame.BoundsAt(center, 15, 26);
            check(frame.FrameIndex == 5 && frame.Degrees == (rotated ? 90 : 0) &&
                bounds == (rotated ? new CardBounds(center.X - 13, center.Y - 7, 26, 15)
                                   : new CardBounds(center.X - 7, center.Y - 13, 15, 26)),
                "V12: whole image, odd dimensions, no atlas slicing");
        }
        check(NativeDisabledChainTarget.FieldFlagsAfterCommand(0x100u, 1) == 0x120u &&
              NativeDisabledChainTarget.FieldFlagsAfterCommand(0x120u, 1) == 0x120u &&
              NativeDisabledChainTarget.FieldFlagsAfterCommand(0x121u, 0) == 0x101u &&
              NativeDisabledChainTarget.CreatesVisual(1, 0) &&
              !NativeDisabledChainTarget.CreatesVisual(0, 0) &&
              !NativeDisabledChainTarget.CreatesVisual(1, 1),
              "V12: bit 5 set/clear separate from visual gate");

        // Synthetic resource names only; no original asset names guessed.
        string[] names = Enumerable.Range(0, 6).Select(i => $"test{i}.yga").Reverse().ToArray();
        string[] paths = names.Select(n => NativeDisabledChainResources.Prefix + n).ToArray();
        string manifest = "// fixture\n" + string.Join("\r\n", names.Select(n => n + " 1"));
        check(NativeDisabledChainResources.Parse(manifest, paths.Reverse()).SequenceEqual(paths),
            "V12: manifest order, not sorted directory order");
        reject(() => NativeDisabledChainResources.Parse(manifest, paths.Skip(1)), "V12: missing resource");
        reject(() => NativeDisabledChainResources.Parse(manifest + "\nunknown", paths), "V12: unknown syntax");
        reject(() => NativeDisabledChainResources.Parse(manifest + "\n" + names[0], paths), "V12: unexpected resource count");
    }
}
