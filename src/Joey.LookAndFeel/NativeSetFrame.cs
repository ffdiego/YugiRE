namespace Joey.LookAndFeel;

/// <summary>
/// Native Set counter and partial Draw contract extracted from joey_pc.exe:
/// 0044F370 / 0044F390. Native updates are not wall-clock milliseconds.
/// </summary>
public readonly record struct NativeSetParticle(int Sequence, int Variant, int RisePixels);

public readonly record struct NativeSetFrame(
    int Counter, int ScaleFixed16, int MainOpacity, int ScratchOpacity,
    IReadOnlyList<NativeSetParticle> Particles)
{
    public const int Increment = 0x0C;
    public const int Updates = 22; // Draw samples 0,12,...252; next update 264 deactivates.
    public const int ParticleCount = 8;
    public const int MinimumRadius = 0x10;
    public const int MaximumRadius = 0x2F;

    /// <summary>
    /// Set constructor 0044EFD0 fills eight X/Y pairs at object+0x104.
    /// For particle i it samples radius = 0x10 + (MT_random &amp; 0x1f),
    /// x = center.X + (cos[i*64] * radius >> 16),
    /// y = center.Y + (cos[(i*64-128)&511] * radius >> 16).
    /// Supply the eight radii explicitly to keep this calculation testable.
    /// The actual shared native MT sequence is not reproduced here.
    /// </summary>
    public static CardPoint[] ParticleOrigins(CardPoint center, ReadOnlySpan<int> radii,
                                              ReadOnlySpan<int> cosineFixed16)
    {
        if (radii.Length != ParticleCount)
            throw new ArgumentException("Exactly eight native particle radii are required.", nameof(radii));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));

        CardPoint[] origins = new CardPoint[ParticleCount];
        for (int i = 0; i < ParticleCount; i++)
        {
            int radius = radii[i];
            if (radius is < MinimumRadius or > MaximumRadius)
                throw new ArgumentOutOfRangeException(nameof(radii), "Native radius must be 16..47.");
            int index = i * 0x40;
            int x = center.X + (int)((long)cosineFixed16[index] * radius >> 16);
            int y = center.Y + (int)((long)cosineFixed16[(index - 0x80) & 511] * radius >> 16);
            origins[i] = new CardPoint(x, y);
        }
        return origins;
    }

    public static NativeSetFrame FromUpdate(int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= Updates) throw new ArgumentOutOfRangeException(nameof(update));
        return FromCounter(update * Increment, cosineFixed16);
    }

    public static NativeSetFrame FromCounter(int counter, ReadOnlySpan<int> cosineFixed16)
    {
        if (counter < 0 || counter > 0xFC || counter % Increment != 0)
            throw new ArgumentOutOfRangeException(nameof(counter));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));

        // 0044F3A0-0044F40F: transformed image and cosine-shaped intensity.
        int scale = counter < 0x80 ? 0x10000 : counter << 9;
        int cosine = cosineFixed16[(counter - 0x80) & 511];
        int mainOpacity = Math.Clamp((int)((long)cosine * 0xB0 >> 16), 0, 255);
        int scratchOpacity = Math.Clamp(cosine >> 9, 0, 255);

        // 0044F4F0 onward: eight launches, staggered 16 counter units apart.
        // The alternating variant and quadratic rise are recovered. X origins
        // are initialized in constructor 0044EFD0 by eight MT-derived radii;
        // ParticleOrigins ports that layout from the knowledge SQLite.
        var particles = new List<NativeSetParticle>(ParticleCount);
        for (int i = 0; i < ParticleCount; i++)
        {
            int elapsed = counter - i * 0x10;
            if (elapsed < 0) continue;
            int age = elapsed / 8;
            particles.Add(new NativeSetParticle(i, age & 1, age * age / 16));
        }
        return new NativeSetFrame(counter, scale, mainOpacity, scratchOpacity, particles);
    }
}
