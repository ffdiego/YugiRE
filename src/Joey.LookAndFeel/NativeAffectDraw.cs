namespace Joey.LookAndFeel;

public readonly record struct NativeAffectCopy(CardBounds Source, CardPoint Destination);

public sealed record NativeAffectScratch(int IconIndex, NativeAffectCopy[] BackgroundCopies);

public readonly record struct NativeAffectDrawPass(
    CardPoint Center, int Intensity, NativeAffectScratch? Rebuild);

/// <summary>Draw order and scratch reuse for 0040E040/0040E1F0.
/// The caller resolves links and supplies the target center. The icon
/// remap and final surface compositor are independent of this plan.</summary>
public sealed class NativeAffectDraw
{
    private readonly int scratchWidth;
    private readonly int scratchHeight;
    private readonly int loopHeight;

    // The constructor at 0040DC4C creates the scratch from icon 1 dimensions.
    public NativeAffectDraw(int scratchWidth, int scratchHeight, int loopHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scratchWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scratchHeight);
        if (loopHeight <= scratchHeight) throw new ArgumentOutOfRangeException(nameof(loopHeight));
        this.scratchWidth = scratchWidth;
        this.scratchHeight = scratchHeight;
        this.loopHeight = loopHeight;
    }

    public const string Manifest = @"j\duel\effect\affect.txt";
    private int? cachedSelector;

    public static string[] Resources(string manifest, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        List<string> resources = [];
        const string prefix = @"j\duel\effect\";
        foreach (var raw in manifest.Split('\n'))
        {
            string name = raw.Split("//", 2)[0].Trim();
            if (name.Length == 0) continue;
            string path = prefix + name;
            if (!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                name.Contains('\\') || name.Contains('/') || !available.Contains(path))
                throw new InvalidDataException($"Missing or invalid affect.txt resource: {path}");
            resources.Add(path);
        }
        if (resources.Count != 6)
            throw new InvalidDataException("Expected loop and five icon records in affect.txt.");
        return resources.ToArray();
    }

    public NativeAffectDrawPass Sample(NativeAffectTimeline timeline, int selector,
        CardPoint center, ReadOnlySpan<int> nativeCosine)
    {
        if (selector is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(selector));
        int intensity = timeline.Intensity(nativeCosine);
        NativeAffectScratch? rebuild = null;
        if (cachedSelector != selector)
        {
            int start = (timeline.Counter / 8) % loopHeight;
            int end = (int)(((long)start + scratchHeight) % loopHeight);
            NativeAffectCopy main = new(new(0, start, scratchWidth,
                (end < start ? loopHeight : end) - start), new(0, 0));

            // The wrapped head is copied to the bottom first at 0040E145.
            // The tail or unwrapped slice is copied to y=0 at 0040E191.
            NativeAffectCopy[] copies = end < start
                ? [new(new(0, 0, scratchWidth, end), new(0, loopHeight - start)), main]
                : [main];
            rebuild = new(selector, copies);
            cachedSelector = selector;
        }
        return new(center, intensity, rebuild);
    }
}
