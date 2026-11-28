namespace Joey.LookAndFeel;

public readonly record struct NativePlacementTravelSample(CardBounds Portrait,int AngleIndex,
    int FaceProgress,int FaceWidthFixed16,bool FaceDown);

/// <summary>Portrait dimensions, unwrapped angle and face at one end of a card movement.</summary>
public readonly record struct CardTravelPose(CardBounds Portrait, int AngleIndex, bool FaceDown);

/// <summary>00418B60: center uses cosine, size interpolation is linear,
/// orientation moves by 16 units per update for a quarter turn; descriptor
/// bit 15 (face-up) progresses from 0 to 256 or vice versa. Width factor
/// from candidate 00418EF0: cos(min(face,256-face)). GPU projection and
/// destination sizing are client adapters, not native pixel parity.</summary>
public static class NativePlacementTravel
{
    // Windows capture 2026-11-28: eight motion samples, then a ninth
    // Update commits the destination descriptor. The two ordinary
    // summons started their effect two ticks later than the Sets.
    // This is the observed presentation interval, not a decoded
    // universal gate for every native placement opcode.
    public const int DescriptorCommitUpdates = CardTravel.Updates + 1;

    public static int ObservedEffectStartOffset(bool set) => DescriptorCommitUpdates + (set ? 0 : 2);

    public static int MotionSampleUpdate(int elapsedUpdates) =>
        Math.Clamp(elapsedUpdates, 0, CardTravel.Updates);

    // 91 (Windows trace): position, size and angle remain fixed; only
    // face changes 0 -> 256 by 32, followed by descriptor commit.
    public static NativePlacementTravelSample SampleFieldReveal(CardBounds portrait,bool nearSide,
        int update,ReadOnlySpan<int> cosine)
    {
        int angle = nearSide ? 0 : 256;
        return Sample(new(portrait, angle, true), new(portrait, angle, false), update, cosine);
    }

    public static NativePlacementTravelSample Sample(CardBounds origin,CardBounds destination,
        bool nearSide,bool destinationDefense,bool destinationFaceDown,int update,
        ReadOnlySpan<int> cosine)
    {
        int angle = nearSide ? 0 : 256;
        return Sample(new(origin, angle, !nearSide),
            new(destination, angle + (destinationDefense ? 128 : 0), destinationFaceDown), update, cosine);
    }

    public static NativePlacementTravelSample Sample(CardTravelPose source, CardTravelPose target,
        int update, ReadOnlySpan<int> cosine)
    {
        if(update<0||update>CardTravel.Updates)throw new ArgumentOutOfRangeException(nameof(update));
        NativeFusionMath.Validate(cosine);
        int t = update * 32;
        int width = Interpolate(source.Portrait.Width, target.Portrait.Width, t);
        int height = Interpolate(source.Portrait.Height, target.Portrait.Height, t);
        int weight = NativeFusionMath.C(cosine, t / 2 - 128);
        var from = DuelCardLayout.FrameCenter(source.Portrait);
        var to = DuelCardLayout.FrameCenter(target.Portrait);
        int x = from.X + (int)(((long)to.X - from.X) * weight >> 16);
        int y = from.Y + (int)(((long)to.Y - from.Y) * weight >> 16);
        int angle = Interpolate(source.AngleIndex, target.AngleIndex, t);
        int face = Interpolate(source.FaceDown ? 0 : 256, target.FaceDown ? 0 : 256, t);
        int scale = NativeFusionMath.C(cosine, Math.Min(face, 256 - face));
        return new(new(x - width / 2, y - height / 2, width, height), angle,
            face, Math.Max(0, scale), face < 128);
    }

    private static int Interpolate(int start, int end, int t) =>
        (int)(start + ((long)end - start) * t / 256);
}

/// <summary>Resource keys from observed set.txt, summon.txt and sacrifice.txt.
/// The constructor's monster flag, not controller side, selects the Set bank.</summary>
public static class NativePlacementAssets
{
    public const string Prefix=@"j\duel\effect\";
    public static string[] SetBank()=>new[]{"eff_set_y.bmp","eff_set_b.bmp",
        "eff_star0_y.bmp","eff_star1_y.bmp","eff_star0_b.bmp","eff_star1_b.bmp"}
        .Select(n=>Prefix+n).ToArray();
    public static string Aura(bool spell)=>SetBank()[spell?1:0];
    public static string Star(bool spell,int variant)
    {
        if(variant is not (0 or 1))throw new ArgumentOutOfRangeException(nameof(variant));
        return SetBank()[2+(spell?2:0)+variant];
    }
    public static string TributeStar(int variant)=>Star(false,variant);
    public const string Rainbow=Prefix+"eff_rainbow.bmp";

    public static void Validate(string text,IReadOnlyList<string> expected)
    {
        var actual=text.Split('\n').Select(l=>l.Split("//",2)[0].Trim())
            .Where(l=>l.Length>0).Select(l=>Prefix+l.Replace('/','\\')).ToArray();
        if(!actual.SequenceEqual(expected,StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected placement effect manifest.");
    }
}
