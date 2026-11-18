using System.Buffers.Binary;
using Joey.LookAndFeel;

internal static class ActivationEffectChecks
{
    private static byte[] Yga(int width, int height, int mode, int stored, byte[] payload)
    {
        var bytes = new byte[24 + payload.Length];
        "yga\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), height);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), mode);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), width * height * 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), stored);
        payload.CopyTo(bytes, 24);
        return bytes;
    }

    public static void Run(Action<bool, string> check)
    {
        // Use synthetic pixels; the user's original YGA resources remain
        // inside the local DAT and never enter the repository.
        byte[] pixels = [10, 20, 30, 200, 0, 0, 0, 255];
        var raw = NativeYgaSprite.Decode(Yga(2, 1, 0, 0, pixels),
            static (_, _) => throw new Exception("Raw YGA must not invoke LZSS"));
        check(raw.Width == 2 && raw.Height == 1 &&
              raw.Rgba.SequenceEqual(new byte[] { 30, 20, 10, 200, 0, 0, 0, 0 }),
              "Activation YGA: raw BGRA is converted to RGBA and black color-key is transparent");

        var compressed = NativeYgaSprite.Decode(Yga(2, 1, 1, 3, [1, 2, 3]),
            (_, length) => length == pixels.Length ? (byte[])pixels.Clone() : []);
        check(compressed.Rgba.SequenceEqual(raw.Rgba),
              "Activation YGA: compressed-mode payload is delegated to archive LZSS decoder");
        bool badSignature = false, badSize = false, badDecode = false;
        try { NativeYgaSprite.Decode(new byte[24], (_, _) => []); }
        catch (InvalidDataException) { badSignature = true; }
        try { NativeYgaSprite.Decode(Yga(3, 1, 0, 0, pixels), (_, _) => []); }
        catch (InvalidDataException) { badSize = true; }
        try { NativeYgaSprite.Decode(Yga(2, 1, 1, 3, [1, 2, 3]), (_, _) => [1]); }
        catch (InvalidDataException) { badDecode = true; }
        check(badSignature && badSize && badDecode,
              "Activation YGA: invalid headers, bounds and decoder output fail closed");

        check(ActivationSpriteNames.All.Count == 4 &&
              ActivationSpriteNames.At(0) == ActivationSpriteNames.UpperBlue &&
              ActivationSpriteNames.At(1) == ActivationSpriteNames.LowerBlue &&
              ActivationSpriteNames.At(2) == ActivationSpriteNames.UpperYellow &&
              ActivationSpriteNames.At(3) == ActivationSpriteNames.LowerYellow,
              "Activation YGA: all original blue/yellow sprite names and native resource indices");

        var playback = new ActivationEffectPlayback();
        playback.Start(new CardPoint(525, 352), new CardBounds(492, 308, 66, 88),
                       style: new DuelLookAndFeel());
        check(playback.IsPlaying && playback.Counter == 0 &&
              playback.Center == new CardPoint(525, 352) &&
              playback.Frame.Lower.ResourceIndex == 1,
              "Activation playback: starts at counter zero, own local screen position");

        playback.Advance(TimeSpan.FromMilliseconds(100), new DuelLookAndFeel());
        check(playback.IsPlaying && playback.Counter == 96,
              "Activation playback: prototype 60Hz policy gives six +16 updates in 100ms");
        playback.Advance(TimeSpan.FromSeconds(1), new DuelLookAndFeel());
        check(!playback.IsPlaying && playback.Counter == 272,
              "Activation playback: 17 native updates terminate the animation without blocking");

        playback.Start(new CardPoint(525, 352), new CardBounds(492, 308, 66, 88),
                       category: 1, style: new FastLookAndFeel());
        playback.Advance(TimeSpan.FromMilliseconds(100), new FastLookAndFeel());
        check(playback.IsPlaying && playback.Counter == 192 &&
              playback.Frame.Lower.ResourceIndex == 3 &&
              playback.Frame.Upper.ResourceIndex == 2,
              "Activation playback: Fast doubles update cadence; category 1 uses yellow resources");

        playback.Start(new CardPoint(525, 352), new CardBounds(492, 308, 66, 88),
                       style: new InstantLookAndFeel());
        check(!playback.IsPlaying && playback.Counter == 0,
              "Activation playback: Instant skips presentation but does not alter game logic");

        playback.Start(new CardPoint(525, 352), new CardBounds(492, 308, 66, 88));
        playback.Advance(TimeSpan.FromMilliseconds(200), new DuelLookAndFeel());
        playback.Stop();
        check(!playback.IsPlaying, "Activation playback: reset/scene change cancels pending effects");

        bool invalidGeometry = false, invalidCategory = false;
        try { playback.Start(new CardPoint(0, 0), new CardBounds(0, 0, 0, 88)); }
        catch (ArgumentOutOfRangeException) { invalidGeometry = true; }
        try { playback.Start(new CardPoint(0, 0), new CardBounds(0, 0, 66, 88), category: 2); }
        catch (ArgumentOutOfRangeException) { invalidCategory = true; }
        check(invalidGeometry && invalidCategory,
              "Activation playback: invalid scratch and unsupported category are rejected");
    }
}
