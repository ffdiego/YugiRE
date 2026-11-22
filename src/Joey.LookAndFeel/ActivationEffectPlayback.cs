using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>
/// Some native effect sprites treat RGB black as a color-key; cursor YGAs
/// instead contain opaque black glyphs inside the action bubble, so they
/// must use their source BGRA alpha without forcing black transparent.
/// </summary>
public enum YgaTransparencyMode { BlackColorKey, SourceAlpha }

/// <summary>
/// Reads a single original YGA 32-bit BGRA sprite from data.dat. It owns no
/// archive or renderer. The caller supplies the DAT-compatible LZSS decoder,
/// keeping Joey.LookAndFeel independent of Joey.Core.
/// </summary>
public sealed record NativeYgaSprite(int Width, int Height, byte[] Rgba)
{
    public static NativeYgaSprite Decode(ReadOnlySpan<byte> bytes,
                                          Func<byte[], int, byte[]> decompress,
                                          YgaTransparencyMode transparency = YgaTransparencyMode.BlackColorKey)
    {
        ArgumentNullException.ThrowIfNull(decompress);
        if (bytes.Length < 24 || !bytes[..4].SequenceEqual("yga\0"u8))
            throw new InvalidDataException("Invalid YGA signature/size.");
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
        int height = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
        int mode = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(12, 4));
        int decodedSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(16, 4));
        int storedSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(20, 4));
        if (width is < 1 or > 4096 || height is < 1 or > 4096 ||
            (long)width * height * 4 != decodedSize || decodedSize > 16 * 1024 * 1024)
            throw new InvalidDataException("Invalid YGA pixel dimensions.");
        byte[] pixels;
        if (mode == 1 && storedSize == bytes.Length - 24)
        {
            pixels = decompress(bytes[24..].ToArray(), decodedSize);
        }
        else if (mode == 0 && (storedSize == 0 || storedSize == decodedSize) &&
                 bytes.Length - 24 == decodedSize)
        {
            pixels = bytes[24..].ToArray();
        }
        else throw new InvalidDataException("Unsupported YGA compression/length.");
        if (pixels is null || pixels.Length != decodedSize)
            throw new InvalidDataException("YGA decompressor returned incorrect byte count.");

        // BGRA -> RGBA for raylib. Black-keying is an asset-family policy,
        // NOT an intrinsic part of YGA decoding: cursor action labels use
        // opaque black letters. Never erase them from the source alpha.
        for (int i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            if (transparency == YgaTransparencyMode.BlackColorKey &&
                pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0)
                pixels[i + 3] = 0;
        }
        return new NativeYgaSprite(width, height, pixels);
    }
}

/// <summary>
/// Playback at the profile's nominal update rate, independent of rendering and rules.
/// Samples the already-ported counter and native three-pass descriptors.
/// </summary>
public sealed class ActivationEffectPlayback
{
    public const int MaxUpdates = 17;
    private readonly NativeActivationTimeline timeline = new();
    private readonly int[] cosine;
    private readonly PresentationTickClock clock = new();
    public CardPoint Center { get; private set; }
    public CardBounds ScratchBounds { get; private set; }
    public int Category { get; private set; }
    public bool IsPlaying { get; private set; }
    public int Counter => timeline.Counter;
    public NativeActivationFrame Frame => timeline.Sample(Category, cosine);

    public ActivationEffectPlayback(int[]? cosineFixed16 = null)
    {
        // The actual x87-generated DAT_00A52EE8 table must be captured for
        // bit-exact rendering. This approximation is NOT such a capture.
        cosine = cosineFixed16 is null ? ApproximateCosineTable() : (int[])cosineFixed16.Clone();
        if (cosine.Length != 512) throw new ArgumentException("Expected 512 cosine values.", nameof(cosineFixed16));
    }

    public static int[] ApproximateCosineTable() =>
        Enumerable.Range(0, 512)
            .Select(i => (int)(Math.Cos(i * Math.PI / 256.0) * 65536.0))
            .ToArray();

    public void Start(CardPoint center, CardBounds scratchBounds, int category = 0,
                      DuelLookAndFeel? style = null)
    {
        if (category is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(category));
        if (scratchBounds.Width is < 1 or > 512 || scratchBounds.Height is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(scratchBounds));
        Center = center;
        ScratchBounds = scratchBounds;
        Category = category;
        clock.Reset();
        timeline.Reset();
        IsPlaying = style?.PlayAnimations != false;
    }

    public void Advance(TimeSpan elapsed, DuelLookAndFeel style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsPlaying) return;
        if (!style.PlayAnimations) { IsPlaying = false; return; }
        int n = Math.Min(clock.Advance(elapsed, style), MaxUpdates);
        for (int i = 0; i < n && timeline.Active; i++)
            timeline.AdvanceNativeUpdate();
        IsPlaying = timeline.Active;
    }

    public void Stop()
    {
        IsPlaying = false;
        clock.Reset();
    }
}
