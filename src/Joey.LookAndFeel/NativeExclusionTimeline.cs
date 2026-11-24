namespace Joey.LookAndFeel;

public readonly record struct NativeExclusionPass(int? ResourceKey,CardBounds Bounds,
    int Intensity,int RotationDegrees);
// null ResourceKey denotes mini/list_card.txt, not exclusion.txt.
public readonly record struct NativeExclusionDraw(int Counter,NativeExclusionPass[] Passes,
    bool ReleasesCardBank);

/// <summary>0043DBE0. Counter and release mutate only on actual Draw.
/// SampleNextDraw is a pure query. Never advance in CanComplete.
/// Geometry callback resolves the descriptor for the current Draw;
/// no implicit interpretation of graveyard/banishment commands.</summary>
public sealed class NativeExclusionTimeline
{
    public const int Increment=8,EndCounter=256,Draws=24;
    public const uint ConstructorSound=0x10000024;
    public NativeExclusionStart Start { get; }
    public int Counter { get; private set; }=NativeExclusionStart.InitialCounter;
    public bool Active => Counter<EndCounter;

    public NativeExclusionTimeline(NativeExclusionStart start)=>Start=start;

    public NativeExclusionDraw? SampleNextDraw(CardPoint center,int miniWidth,int miniHeight,
        int nativePerspectiveBit,ReadOnlySpan<int> cosine,Func<int,CardBounds> resourceSize)
    {
        if(!Active)return null;
        if(miniWidth<=0||miniHeight<=0)throw new ArgumentOutOfRangeException(nameof(miniWidth));
        if(nativePerspectiveBit is not(0 or 1))throw new ArgumentOutOfRangeException(nameof(nativePerspectiveBit));
        ArgumentNullException.ThrowIfNull(resourceSize);
        NativeFusionMath.Validate(cosine);
        int t=Counter+Increment;
        List<NativeExclusionPass> passes=[];
        CardBounds Centered(int w,int h,int x)=>new(x-w/2,center.Y-h/2,w,h);
        CardBounds Size(int key)
        {
            var size=resourceSize(key);
            if(size.Width<=0||size.Height<=0)throw new InvalidDataException("Invalid exclusion resource geometry.");
            return size;
        }
        if(t<128)
            passes.Add(new(null,Centered(miniWidth,miniHeight,center.X),256,
                ((Start.Descriptor&1)^nativePerspectiveBit)*180));
        if(t<=128)
        {
            var s=Size(1);
            int intensity=NativeFusionMath.C(cosine,2*t-256)>>8;
            int w=unchecked(intensity*s.Width)>>8;
            for(int i=0;i<3;i++)
                passes.Add(new(1,Centered(w,s.Height,center.X+(i-1)*(s.Width+1)),intensity,0));
        }
        else
        {
            var s=Size(2); // measure key 2, NOT the selected animation frame
            int c=NativeFusionMath.C(cosine,t-128);
            int scale=(c>>9)+128;
            passes.Add(new(t/8%6+2,Centered(unchecked(scale*s.Width)>>8,
                unchecked(scale*s.Height)>>8,center.X),c>>7,0));
        }
        if(t>=96)
        {
            var s=Size(0);
            int c=NativeFusionMath.C(cosine,t<=128?4*t:t-128);
            int scale=t<=128?256:(c>>9)+128;
            passes.Add(new(0,Centered(unchecked(scale*s.Width)>>8,
                unchecked(scale*s.Height)>>8,center.X),c>>8,0));
        }
        // Terminal Draw still executes its passes before releasing the
        // masked card-bank key and clearing active at 0043E04E..0043E05D.
        return new(t,passes.ToArray(),t>=EndCounter);
    }

    public NativeExclusionDraw? Draw(CardPoint center,int miniWidth,int miniHeight,
        int nativePerspectiveBit,ReadOnlySpan<int> cosine,Func<int,CardBounds> resourceSize)
    {
        var frame=SampleNextDraw(center,miniWidth,miniHeight,nativePerspectiveBit,cosine,resourceSize);
        if(frame is { } draw)Counter=draw.Counter;
        return frame;
    }
}

/// <summary>Strict diagnostic mapping. Preserve local manifest order;
/// the original lookup remap and blit are not assumed equivalent to Raylib.
/// Reject if the private manifest differs, rather than inventing an order.</summary>
public static class NativeExclusionResources
{
    public const string Prefix=@"j\duel\effect\";
    private static readonly string[] Names=["jyoga_hikacard.yga","jyoga_hikatate.yga",
        "jyoga_ryusi1.bmp","jyoga_ryusi2.bmp","jyoga_ryusi3.bmp","jyoga_ryusi4.bmp",
        "jyoga_ryusi5.bmp","jyoga_ryusi6.bmp"];
    public static bool IsParticleBitmap(string path)=>
        Names.Skip(2).Any(n=>path.Equals(Prefix+n,StringComparison.OrdinalIgnoreCase));
    public static string[] Parse(string manifest,IEnumerable<string> archiveNames)
    {
        var available=new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<string> paths=[];
        foreach(string raw in manifest.Split('\n'))
        {
            string line=raw.Split("//",2)[0].Trim();
            if(line.Length==0)continue;
            string[] fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            string name=fields[0].Replace('/','\\');
            string path=name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)?name:Prefix+name;
            if(fields.Length>2 || fields.Length==2&&!int.TryParse(fields[1],out _) ||
                paths.Count>=Names.Length ||
                !path.Equals(Prefix+Names[paths.Count],StringComparison.OrdinalIgnoreCase) ||
                !available.Contains(path))
                throw new InvalidDataException("Unsupported exclusion manifest record: "+line);
            paths.Add(path);
        }
        if(paths.Count!=Names.Length)throw new InvalidDataException("Incomplete exclusion manifest.");
        return paths.ToArray();
    }
}
