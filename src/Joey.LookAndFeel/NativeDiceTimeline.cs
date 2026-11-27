namespace Joey.LookAndFeel;

/// <summary>Logical resource key before the native bank remap, source
/// rectangle and integer destination rectangle. Intensity is in native
/// 0..256 units. The host owns resource loading and composition.</summary>
public readonly record struct NativeDicePass(int Key, CardBounds Source,
    CardBounds Destination, int Intensity);

public readonly record struct NativeDiceTexture(int Width, int Height, int Patterns)
{
    public int PatternWidth
    {
        get
        {
            if (Width <= 0 || Height <= 0 || Patterns <= 0 || Width % Patterns != 0)
                throw new InvalidDataException("Invalid native dice strip geometry.");
            return Width / Patterns;
        }
    }

    public CardBounds Pattern(int frame)
    {
        int width = PatternWidth;
        if (frame < 0 || frame >= Patterns)
            throw new ArgumentOutOfRangeException(nameof(frame));
        return new(frame * width, 0, width, Height);
    }

    public CardBounds Whole => Width > 0 && Height > 0 ? new(0, 0, Width, Height) :
        throw new InvalidDataException("Invalid native dice texture geometry.");
}

public readonly record struct NativeDiceDraw(
    int Counter, CardPoint Center, int Bounce, int ScaleFixed8,
    NativeDicePass Shadow, NativeDicePass Die, uint? SoundRequest);

/// <summary>004568C0 / 00456B70 / 00456B90. One visual object; no game
/// result generation, wall clock, audio dispatch or scheduler. The native
/// +14 value is supplied by the command (word B). For this supported
/// range 1..6 the explicit selector tables are initialized by assembly.
/// Update only advances time; a real Draw also changes the center.
/// </summary>
public sealed class NativeDiceTimeline
{
    public const int StartCounter = -256, EndCounter = 768, UpdateIncrement = 8;
    public const int Updates = (EndCounter - StartCounter) / UpdateIncrement;
    public const int Layer = 0x110;
    public const uint BounceSound = 0x1000001c;
    public int Counter { get; private set; } = StartCounter;
    public bool Active => Counter < EndCounter;
    public bool BlocksRelease => Active; // vtable +4, 00403080
    public CardPoint Center { get; private set; }
    public int NativeIndex { get; }

    public NativeDiceTimeline(int nativeIndex, CardPoint origin)
    {
        if (nativeIndex is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(nativeIndex),
                "Only native indices 1..6 have supported complete selector inputs.");
        NativeIndex = nativeIndex;
        Center = origin;
    }

    public void AdvanceNativeUpdate()
    {
        if (Active) Counter += UpdateIncrement;
    }

    // Sample is side-effect free. The side test is the current Y at the
    // entry to Draw, not a persistent field or the gameplay player's side.
    public NativeDiceDraw? Sample(ReadOnlySpan<int> cosine,
        Func<int, NativeDiceTexture> resource)
    {
        if (!Active) return null;
        NativeFusionMath.Validate(cosine);
        ArgumentNullException.ThrowIfNull(resource);
        int t = Counter;
        bool upper = Center.Y < 300;
        int bounce = t < 0 ? 0 :
            t < 256 ? NativeFusionMath.C(cosine, (t & 255) - 128) >> 7 :
            t < 384 ? NativeFusionMath.C(cosine, ((2*t) & 255) - 128) >> 9 : 0;
        int scale = t < -192 ? t*4+1024 : bounce/2+256;
        CardPoint center = Center;
        if (t > 0 && t <= 384)
        {
            int n = (384-t)/8;
            int y = upper ? 236 : 364;
            center = new(525 + (Center.X-525)*n/(n+1), y+(Center.Y-y)*n/(n+1));
        }
        else if (t > 384 && t <= 448)
            center = new(525, upper ? t-148 : 748-t);
        uint? sound = t is 256 or 384 ? BounceSound : null;

        if (t >= 448)
        {
            int key = NativeIndex+6;
            var die = resource(key).Whole;
            var shadow = resource(6).Whole;
            int intensity = t <= 704 ? 256 : (768-t)*4;
            // 004570FE..0045717F: both passes use HALF THE FACE WIDTH
            // for X and Y. Do not independently center the shadow.
            int x = center.X-die.Width/2, y = center.Y-die.Width/2;
            return new(t,center,bounce,scale,
                new(6,shadow,new(x,y,shadow.Width,shadow.Height),intensity),
                new(key,die,new(x,y,die.Width,die.Height),intensity),sound);
        }

        // 00456CCC..00456D25: local selector tables overlap at the
        // index-6 boundary; initialized values at ESP+54 are 4.
        int bank = t <= 256 ? 0 : NativeIndex is 3 or 4 or 5 ? 2 : 4;
        int phaseOffset = NativeIndex switch { 1 or 3 => 8, 2 or 5 => 4, _ => 0 };
        int phase = upper ? t/8+256+phaseOffset : 255-t/8+phaseOffset;
        var shadowTexture = resource(bank+1);
        var dieTexture = resource(bank);
        _ = shadowTexture.PatternWidth;
        _ = dieTexture.PatternWidth;
        var shadowSource = shadowTexture.Pattern(phase % shadowTexture.Patterns);
        var dieSource = dieTexture.Pattern(phase % dieTexture.Patterns);
        int s = Math.Min(scale,256);
        int sw = shadowSource.Width*s/256, sh = shadowSource.Height*s/256;
        int dw = dieSource.Width*scale/256, dh = dieSource.Height*scale/256;
        int shift = bounce*25/256;
        // Separate truncations (00456DDB..00456E24).
        int sx = shift+scale*7/256, sy = shift+scale*9/256;
        return new(t,center,bounce,scale,
            new(bank+1,shadowSource,new(center.X-sw/2+sx,center.Y-sh/2+sy,sw,sh),256),
            new(bank,dieSource,new(center.X-dw/2,center.Y-dw/2-bounce*50/256,dw,dh),256),sound);
    }

    /// <summary>Call only for an actual native Draw. A second Draw at
    /// the same counter again updates center and may request audio again.
    /// Queries by a presentation gate must use Sample, never Draw.</summary>
    public NativeDiceDraw? Draw(ReadOnlySpan<int> cosine, Func<int,NativeDiceTexture> resource)
    {
        var sample = Sample(cosine,resource);
        if (sample is { } draw) Center = draw.Center;
        return sample;
    }
}
