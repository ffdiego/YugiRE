namespace Joey.LookAndFeel;

/// <summary>
/// Native activation animation counter observed at 00450590, not a wall-time
/// animation. Each native update increases the counter by 16; after the 17th
/// update (272), the active flag clears. Draw cadence is not yet known.
/// </summary>
public sealed class NativeActivationTimeline
{
    public const int UpdateIncrement = 16;
    public const int ActiveUntilExclusive = 257;

    public int Counter { get; private set; }
    public bool Active { get; private set; } = true;

    public void Reset()
    {
        Counter = 0;
        Active = true;
    }

    public void AdvanceNativeUpdate()
    {
        if (!Active) return;
        Counter += UpdateIncrement;
        Active = Counter < ActiveUntilExclusive;
    }

    /// <summary>
    /// A single native DRAW sample, not a frame or a duration in milliseconds.
    /// Supply a 512-entry x16.16 native cosine table (DAT_00A52EE8) for fidelity.
    /// Using an independently calculated cosine table is not bit-exact proof.
    /// </summary>
    public NativeActivationFrame Sample(int category, ReadOnlySpan<int> cosineFixed16) =>
        NativeActivationFrame.FromCounter(Counter, category, cosineFixed16);
}

public enum NativeActivationLayer
{
    LowerSprite,
    BlackScratch,
    UpperSprite
}

/// <summary>
/// A draw pass in original order. The black scratch is dynamically generated,
/// not an image file. Rotation is in 1/512-turn units; scale is signed x16.16.
/// </summary>
public readonly record struct NativeActivationPass(
    NativeActivationLayer Layer,
    int ResourceIndex,
    int RotationIndex,
    int ScaleFixed16,
    int Opacity);

/// <summary>
/// The three-pass composition reconstructed at 004505D0 / 00422C00.
/// This describes drawing parameters, not a playable GPU renderer.
/// </summary>
public readonly record struct NativeActivationFrame(
    int Counter,
    NativeActivationPass Lower,
    NativeActivationPass Scratch,
    NativeActivationPass Upper)
{
    public static NativeActivationFrame FromCounter(int counter, int category,
                                                     ReadOnlySpan<int> cosineFixed16)
    {
        if (counter < 0 || counter > 256 || counter % 16 != 0)
            throw new ArgumentOutOfRangeException(nameof(counter),
                "Only a live activation counter (0,16,...256) can be sampled.");
        if (category is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(category),
                "Only the native blue/yellow activation pairs are mapped.");
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Native cosine table must have 512 entries.", nameof(cosineFixed16));

        int scale = cosineFixed16[(counter - 128) & 511];
        int mainOpacity = Math.Min(scale >> 8, 255);
        int scratchOpacity = scale >> 9;

        return new(counter,
            new(NativeActivationLayer.LowerSprite, category * 2 + 1,
                counter / 2, scale, mainOpacity),
            new(NativeActivationLayer.BlackScratch, -1, 0, 65536, scratchOpacity),
            new(NativeActivationLayer.UpperSprite, category * 2,
                counter, scale, mainOpacity));
    }
}

[Flags]
public enum NativeExplosionPasses
{
    None = 0,
    Initial = 1,
    Flare = 2,
    Fragments = 4
}

/// <summary>Native update counts for the phase notice at 00480B40 / 00480C20 / 00480C60.</summary>
public static class NativePhaseNotice
{
    public const int StandardUpdates = 32;
    public const int BattleUpdates = 40;
}

/// <summary>
/// Only the static counters and draw-phase windows are ported. In the native
/// object (00456730 / 004514B0), draw itself can deactivate the effect; the
/// caller must decide update/draw order. We do NOT derive seconds from ticks.
/// </summary>
public sealed class NativeExplosionTimeline
{
    public const int UpdateIncrement = 8;
    public const int Updates = 0xD0 / UpdateIncrement;
    public int Counter { get; private set; }
    public bool Active { get; private set; } = true;

    public void AdvanceNativeUpdate()
    {
        if (Active) Counter += UpdateIncrement;
    }

    public NativeExplosionPasses VisiblePasses() =>
        Active ? PassesAt(Counter) : NativeExplosionPasses.None;

    public static NativeExplosionPasses PassesAt(int counter)
    {
        NativeExplosionPasses result = NativeExplosionPasses.None;
        if (counter < 0x80) result |= NativeExplosionPasses.Initial;
        if (counter > 0x1f && counter < 0x90) result |= NativeExplosionPasses.Flare;
        if (counter > 0x7f && counter < 0xd0) result |= NativeExplosionPasses.Fragments;
        return result;
    }

    public void AfterNativeDraw()
    {
        if (Counter > 0xcf) Active = false;
    }
}

/// <summary>
/// Verified base-object dispatch predicates from 0045D040/0045D170.
/// Native sorting is ascending by key at offset +0x0C; equal-key ordering is
/// undocumented, so clients should not rely on a tie-break for fidelity.
/// </summary>
public static class NativeVisualDispatch
{
    public static bool Participates(bool active, uint objectMask, uint dispatchMask) =>
        active && (objectMask & dispatchMask) != 0;

    public static int CompareLayer(int firstKey, int secondKey) =>
        firstKey.CompareTo(secondKey);
}
