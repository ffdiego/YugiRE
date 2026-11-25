namespace Joey.LookAndFeel;

/// <summary>One ordered subpass of 004194D0; font index is represented separately.</summary>
public readonly record struct NativeCardStatusPass(bool IsDigit, int ResourceIndex,
    CardBounds Source, CardPoint Destination);

public readonly record struct NativeCardStatusInput(
    ushort Descriptor, int PerspectiveSide, CardPoint Center, int MiniWidth, int MiniHeight,
    bool Settled, bool Active, int OriginalType, int EffectiveType,
    int OriginalAttribute, int EffectiveAttribute, ushort FieldFlags,
    NativeCardStatusTransition Transition, int GlobalCounter);

/// <summary>
/// Ordinary status passes from the consolidated assembly. No invented
/// mapping of A/B flags to card effects. The 0x48A exceptional branch and
/// attribute flashes (+0x50 compositor) are outside this contract.
/// </summary>
public static class NativeCardStatusComposition
{
    public static NativeCardStatusPass[] Compose(NativeCardStatusInput input,
        Func<int, NativeStatusAtlas> atlas, int fontWidth, int fontHeight)
    {
        int region = (input.Descriptor >> 1) & 0x1f;
        int slot = (input.Descriptor >> 6) & 0xff;
        // 004194F6..00419525 returns before ANY subpass if the sum exceeds 10.
        if (!input.Active || !input.Settled || (input.Descriptor & 0x8000) == 0 ||
            region + slot > 10 || (input.Descriptor & 0x3e) >= 0x15)
            return [];
        var geometry = NativeCardStatusGeometry.FromNative(input.Center, input.MiniHeight,
            input.PerspectiveSide, input.Descriptor & 1);
        List<NativeCardStatusPass> passes = [];
        void Sprite(NativeStatusSpritePass pass) =>
            passes.Add(new(false, pass.ResourceIndex, pass.Source, pass.Destination));
        void Counter(NativeCardStatusFrame cue, NativeStatusDrawLayout layout)
        {
            Sprite(layout.Main);
            var digits = layout.NumberCentered
                ? NativeCardStatusDigits.Centered(layout.NumberAnchor, cue.FieldValue, fontWidth, fontHeight)
                : NativeCardStatusDigits.RightAnchored(layout.NumberAnchor.X, layout.NumberAnchor.Y,
                    cue.FieldValue, fontWidth, fontHeight);
            foreach (var digit in digits)
                passes.Add(new(true, NativeCardStatusDigits.OriginalFontResourceIndex, digit.Source, digit.Destination));
            if (layout.Transition is { } tail) Sprite(tail);
        }

        var changes = NativeCardStatusOverlay.IdentityChanges(input.OriginalType,
            input.EffectiveType, input.OriginalAttribute, input.EffectiveAttribute);
        // 00419B82..00419BCD reads ordinary effective identity only in
        // regions 0..4. Exceptional non-monster IDs are intentionally excluded.
        bool ordinaryIdentity = region < 5;
        if (ordinaryIdentity && changes.ShowType)
            Sprite(NativeStatusDrawLayout.IdentityIcon(false, input.EffectiveType,
                input.Center, input.MiniWidth, geometry, atlas(5)));
        if (ordinaryIdentity && changes.ShowAttribute)
            Sprite(NativeStatusDrawLayout.IdentityIcon(true, input.EffectiveAttribute,
                input.Center, input.MiniWidth, geometry, atlas(4)));
        if (NativeStatusDrawLayout.SecondRegionEligible(input.Descriptor) &&
            NativeCardStatusOverlay.Second(input.FieldFlags, input.Transition.State,
                input.Transition.Counter) is { } b)
            Counter(b, NativeStatusDrawLayout.Second(input.Center.X, geometry.StatusY,
                input.MiniWidth, b, atlas(8), b.TailFrame.HasValue ? atlas(9) : null));
        if (NativeCardStatusOverlay.First(input.FieldFlags, input.Transition.State,
            input.Transition.Counter, input.GlobalCounter) is { } a)
            Counter(a, NativeStatusDrawLayout.First(input.Center.X, geometry.StatusY,
                input.MiniWidth, a, atlas(6), a.TailFrame.HasValue ? atlas(7) : null));
        return passes.ToArray();
    }
}
