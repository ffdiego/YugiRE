namespace Joey.LookAndFeel;

public readonly record struct NativePlacementTravelSample(CardBounds Portrait,int AngleIndex,
    int FaceProgress,int FaceWidthFixed16,bool FaceDown);

/// <summary>00418B60: center uses cosine, size interpolation is linear,
/// orientation moves by 16 units per update for a quarter turn; descriptor
/// bit 15 (face-up) progresses from 0 to 256 or vice versa. Width factor
/// from candidate 00418EF0: cos(min(face,256-face)). GPU projection and
/// destination sizing are client adapters, not native pixel parity.</summary>
public static class NativePlacementTravel
{
    public static NativePlacementTravelSample Sample(CardBounds origin,CardBounds destination,
        bool nearSide,bool destinationDefense,bool destinationFaceDown,int update,
        ReadOnlySpan<int> cosine)
    {
        if(update<0||update>CardTravel.Updates)throw new ArgumentOutOfRangeException(nameof(update));
        NativeFusionMath.Validate(cosine);
        int t=update*32;
        int w=origin.Width+(destination.Width-origin.Width)*t/256;
        int h=origin.Height+(destination.Height-origin.Height)*t/256;
        int weight=NativeFusionMath.C(cosine,t/2-128);
        var from=DuelCardLayout.FrameCenter(origin);
        var to=DuelCardLayout.FrameCenter(destination);
        int x=from.X+(int)((long)(to.X-from.X)*weight>>16);
        int y=from.Y+(int)((long)(to.Y-from.Y)*weight>>16);
        int fromFace=nearSide?256:0;
        int toFace=destinationFaceDown?0:256;
        int face=fromFace+(toFace-fromFace)*t/256;
        int scale=NativeFusionMath.C(cosine,Math.Min(face,256-face));
        return new(new(x-w/2,y-h/2,w,h),(nearSide?0:256)+(destinationDefense?t/2:0),
            face,Math.Max(0,scale),face<128);
    }
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
