namespace Joey.LookAndFeel;

/// <summary>
/// Sacrifice/tribute presentation reconstructed from joey_pc.exe:
/// 00486D80 (constructor), 00487060 (Update), 004870B0 (Draw).
/// The original Draw also mutates a rotation counter and uses a native scratch
/// compositor; those remain unverified and are not modelled as fixed geometry.
/// </summary>
public readonly record struct NativeTributeFrame(
    int Counter,
    int CardOpacity,
    IReadOnlyList<NativeSetParticle> Particles)
{
    public const int UpdateIncrement = 8;
    public const int HoldCounter = 0x80;
    public const int EndCounter = 0x180;
    public const int ReleasedUpdates = 49; // 0, 8, ... 384; next Update 392 disables.

    /// <summary>
    /// Native Update 00487060 stops at 0x80 while the constructor mode is 0.
    /// 004879A0 creates mode 0; 00487B00 creates mode 1 (or releases an
    /// existing effect). A committed tribute uses the released mode.
    /// </summary>
    public static NativeTributeFrame FromUpdate(
        int update, bool released, ReadOnlySpan<int> cosineFixed16)
    {
        if (update < 0 || released && update >= ReleasedUpdates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 cosine x16.16 entries.", nameof(cosineFixed16));

        int counter = released ? update * UpdateIncrement :
            update >= HoldCounter / UpdateIncrement ? HoldCounter : update * UpdateIncrement;
        int since = counter - HoldCounter;
        // Main effect opacity from 004870B0, with fixed point x16.16.
        int opacity = since < 0
            ? Math.Clamp(cosineFixed16[(counter - HoldCounter) & 511] >> 9, 0, 255)
            : since == 0 ? 0x80
            : Math.Clamp((int)((long)cosineFixed16[(since / 2) & 511] * 0xB0 >> 16), 0, 255);
        var particles = new List<NativeSetParticle>(NativeSetFrame.ParticleCount);
        if (since > 0)
            for (int i = 0; i < NativeSetFrame.ParticleCount; i++)
            {
                int particleAge = since - i * 0x10;
                if (particleAge < 0) continue;
                int age = particleAge / 8;
                particles.Add(new NativeSetParticle(i, age & 1, age * age / 16));
            }

        return new(counter, opacity, particles);
    }

    /// <summary>
    /// The constructor initializes eight radial X/Y origins with exactly the
    /// same MT mask 0x1f and cosine angles as the Set effect constructor.
    /// Supplying the radii keeps the geometry deterministic in tests.
    /// </summary>
    public static CardPoint[] ParticleOrigins(CardPoint center, ReadOnlySpan<int> radii,
                                               ReadOnlySpan<int> cosineFixed16) =>
        NativeSetFrame.ParticleOrigins(center, radii, cosineFixed16);
}
