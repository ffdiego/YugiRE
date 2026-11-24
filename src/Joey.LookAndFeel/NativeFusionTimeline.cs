namespace Joey.LookAndFeel;

/// <summary>
/// Arguments to 00422C00, not a finished GPU pass. Angles retain the
/// original accumulator units; the caller supplies the native cosine table.
/// </summary>
public readonly record struct NativeFusionSprite(int ResourceIndex, int ScaleFixed16,
    int Intensity, int AngleIndex);

/// <summary>
/// 004877B0/004877F0: fusion attached to an existing visual card object.
/// The waiting flag and the associated card's active state come from the
/// caller; neither is inferred from a gameplay decision.
/// </summary>
public sealed class NativeAttachedFusionTimeline
{
    public const int UpdateIncrement = 8;
    public const int WaitCounter = 128;
    public const int EndCounter = 384;
    public int Counter { get; private set; }
    public int Angle { get; private set; }
    public bool Active => Counter < EndCounter;

    public NativeAttachedFusionTimeline(int initialAngle) => Angle = initialAngle;

    public void AdvanceNativeUpdate(bool releaseFlag)
    {
        if (Active && (Counter < WaitCounter || releaseFlag))
            Counter += UpdateIncrement;
    }

    // One actual Draw mutates the angle, even while the Update is waiting.
    // The native renderer returns early for an inactive associated card.
    public NativeFusionSprite? Draw(bool associatedCardActive, ReadOnlySpan<int> cosine)
    {
        if (!Active || !associatedCardActive) return null;
        NativeFusionMath.Validate(cosine);
        int t = Counter;
        int scale, intensity, delta;
        unchecked
        {
            if (t < 128)
            {
                int c = NativeFusionMath.C(cosine, t - 128);
                scale = t << 9;
                intensity = (160 * c) >> 16;
                delta = (c << 2) >> 16;
            }
            else if (t == 128)
            {
                scale = 65536;
                intensity = 160;
                delta = 4;
            }
            else
            {
                int i = (t - 128) / 2;
                scale = t < 256 ? 65536 : (t - 128) << 9;
                intensity = (176 * NativeFusionMath.C(cosine, i)) >> 16;
                delta = ((NativeFusionMath.C(cosine, i - 128) << 2) >> 16) + 4;
            }
            Angle += delta;
            return new NativeFusionSprite(0, scale, intensity, -Angle);
        }
    }
}

/// <summary>
/// 004524E0/004524F0: separate general fusion object. This ports the
/// assembly-confirmed primary and additional sprite formulas only; orbital
/// materials, scratch surface, selector table and final Draw are not ported.
/// In particular Update does not decide when the object becomes inactive.
/// </summary>
public sealed class NativeGeneralFusionTimeline
{
    public const int UpdateIncrement = 4;
    public int Counter { get; private set; }
    public int Angle { get; private set; }

    public NativeGeneralFusionTimeline(int initialAngle) => Angle = initialAngle;

    public void AdvanceNativeUpdate() => Counter = checked(Counter + UpdateIncrement);

    public (int PrimaryResourceIndex, int PrimaryScale, int PrimaryIntensity, int? AdditionalScale, int? AdditionalIntensity) Draw(
        ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        int t = Counter;
        unchecked
        {
            int scale, intensity;
            if (t < 128)
            {
                int c = NativeFusionMath.C(cosine, t - 128);
                scale = t << 9;
                intensity = (c << 8) >> 16;
                Angle -= (c << 3) >> 16;
            }
            else
            {
                Angle -= 8;
                if (t < 384) { scale = 65536; intensity = 256; }
                else if (t < 448)
                {
                    scale = (448 - t) << 10;
                    intensity = (NativeFusionMath.C(cosine, 2 * t - 256) << 8) >> 16;
                }
                else { scale = 0; intensity = 0; }
            }
            if (t is >= 384 and < 448)
                return (1, scale, intensity, 2 * NativeFusionMath.C(cosine, 2 * t + 128) + 32768,
                    NativeFusionMath.C(cosine, 2 * t - 256) >> 8);
            return (1, scale, intensity, null, null);
        }
    }
}

internal static class NativeFusionMath
{
    internal static void Validate(ReadOnlySpan<int> cosine)
    {
        if (cosine.Length != 512)
            throw new ArgumentException("Expected 512 native cosine samples.", nameof(cosine));
    }

    internal static int C(ReadOnlySpan<int> cosine, int index) => cosine[index & 511];
}
