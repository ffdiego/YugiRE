namespace Joey.LookAndFeel;

/// <summary>The sole record in the original break.txt (v3 DAT inventory).
/// Reject other banks and missing resources rather than selecting a fallback.</summary>
public static class NativeBreakResources
{
    public const string Manifest=@"j\duel\effect\break.txt";
    public const string Shine=@"j\duel\effect\shine.bmp";
    public const int ShineWidth=74, ShineHeight=73;
    public static string Parse(string text,IEnumerable<string> archiveNames)
    {
        string[] lines=text.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length!=1 || !lines[0].Equals("shine.bmp",StringComparison.OrdinalIgnoreCase) ||
            !archiveNames.Contains(Shine,StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected the original break.txt shine.bmp record.");
        return Shine;
    }
}

/// <summary>0045C280 normalizes region+slot when their sum is below 11.
/// Unlike the disabled-chain producer, Break may encode a direct field
/// region 0..10 with slot zero. Combat snapshot 00578030 emits B=region:FF;
/// for monster regions 0..4, 0045C280 ignores slot FF and uses region.</summary>
public readonly record struct NativeBreakFieldTarget
{
    public ushort Descriptor { get; }
    public int Side => Descriptor & 1;
    public int Region => (Descriptor >> 1) & 31;
    public int Slot => (Descriptor >> 6) & 255;
    public int CombinedZone => Region+Slot;
    public bool IsCombatSnapshot => Slot==0xff && Region<5;
    public int FieldZone => IsCombatSnapshot?Region:CombinedZone;
    public NativeBreakFieldTarget(ushort descriptor)
    {
        Descriptor=descriptor;
        if(Region>10 || FieldZone>10)
            throw new ArgumentException("Not a field-only break descriptor.",nameof(descriptor));
    }
    public CardPoint Center(int perspective)
    {
        if(perspective is <0 or >1)throw new ArgumentOutOfRangeException(nameof(perspective));
        int zone=FieldZone;
        return DuelCardLayout.Center(zone<5?DuelCardZone.Monster:zone<10?DuelCardZone.Spell:DuelCardZone.Field,
            Side==perspective,zone%5);
    }
}

public readonly record struct NativeBreakFragment(CardBounds Source,CardBounds Destination);
public sealed record NativeBreakFieldFrame(int Counter,bool BlocksRelease,int RotationDegrees,
    CardBounds OrientedCardBounds,int DistortionParameter,NativeBreakScatterKind Kind,
    NativeBreakParticle[] Particles,NativeBreakFragment[] Fragments);

/// <summary>Diagnostic field-only pass plan for 00450FD0. The native
/// card surface is oriented BEFORE the 00558FB0 row-major partition.
/// The source rectangles below address an unrotated texture; the host
/// rotates each cropped tile, without changing its destination extent.
/// Neither color-key behavior nor virtual +AC/+C compositing is modeled.
/// Hand (0041F130), other non-field zones and live movement are excluded.
/// Slot FF in monster regions uses the native combat-snapshot center.</summary>
public static class NativeBreakFieldReview
{
    public static NativeBreakFieldFrame? Sample(NativeBreakTimeline timeline,CardPoint center,
        ushort descriptor,int perspectiveBit,int width,int height,ReadOnlySpan<int> cosine)
    {
        if(width is <1 or >4096 || height is <1 or >4096)
            throw new ArgumentOutOfRangeException(nameof(width));
        // Only these descriptors have established field centers in the review.
        _=new NativeBreakFieldTarget(descriptor);
        int rotation=NativeBreakTimeline.CardRotationDegrees(descriptor,perspectiveBit);
        if(!timeline.Active)return null;
        bool swap=rotation is 90 or 270;
        int w=swap?height:width,h=swap?width:height;
        CardPoint origin=new(center.X-w/2,center.Y-h/2);
        CardBounds whole=new(origin.X,origin.Y,w,h);
        int parameter=timeline.DistortionIntensity(cosine);
        NativeBreakScatterKind kind=NativeBreakScatter.Classify(parameter);
        List<NativeBreakFragment> fragments=[];
        if(kind==NativeBreakScatterKind.Direct)
            fragments.Add(new(new(0,0,width,height),whole));
        else if(kind==NativeBreakScatterKind.Fragmented)
            foreach(var block in NativeBreakScatter.Blocks(w,h))
            {
                var destination = NativeBreakScatter.Project(block, origin, w, h, parameter);
                fragments.Add(new(Unrotate(block.Source,width,height,rotation),destination));
            }
        return new(timeline.Counter,timeline.BlocksRelease,rotation,whole,parameter,kind,
            timeline.Particles(center,NativeBreakResources.ShineWidth,NativeBreakResources.ShineHeight,cosine),
            fragments.ToArray());
    }

    // Inverse of clockwise quarter-turn in half-open pixel rectangles.
    public static CardBounds Unrotate(CardBounds tile,int width,int height,int rotation) => rotation switch
    {
        0 => tile,
        90 => new(tile.Y,height-tile.X-tile.Width,tile.Height,tile.Width),
        180 => new(width-tile.X-tile.Width,height-tile.Y-tile.Height,tile.Width,tile.Height),
        270 => new(width-tile.Y-tile.Height,tile.X,tile.Height,tile.Width),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation))
    };
}
