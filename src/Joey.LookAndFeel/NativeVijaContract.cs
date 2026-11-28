using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>
/// 004580F0, 00458480, 004584A0. Constructor and gate contract.
/// NativeVijaTimeline supplies the recovered Draw geometry; the native
/// scratch compositor and live duel integration remain separate.
/// </summary>
public readonly record struct NativeVijaContract
{
    public int LetterIndex { get; }
    public bool GroupMode { get; }
    public ushort Descriptor { get; }
    public int Side => Descriptor & 1;
    public int Region => (Descriptor >> 1) & 31;
    public int Slot => (Descriptor >> 6) & 255;
    public int InitialCounter => GroupMode ? -4 - 72 * LetterIndex : -4;
    public int PhaseLimit => GroupMode ? 640 - 72 * LetterIndex : 320;
    public int BlockingThrough => PhaseLimit + 64;
    public int Layer => Region == 11 ? 0x201 + Slot * 16 : 0x120;
    public uint SoundRequest => GroupMode ? 0x10000023u : 0x10000022u;

    public NativeVijaContract(ushort nativeIndex, bool groupMode, ushort descriptor)
    {
        LetterIndex = nativeIndex % 5;
        GroupMode = groupMode;
        Descriptor = descriptor;
    }

    // Query of virtual +4, not a claim about visibility, completion of
    // another object or number of real-time frames.
    public bool BlocksCompletion(int nativeCounter) => nativeCounter <= BlockingThrough;
}

/// <summary>vija.txt uses patternCount*1000 + yOffset for strip entries.
/// Unlike generic manifests, this encoding is supported by the original
/// constructor's /1000 and the historical Draw investigation's %1000.
/// Do not assign a strip to an animation phase by name.</summary>
public readonly record struct NativeVijaAtlas(int PatternCount, int YOffset, int FrameWidth, int Height)
{
    public static NativeVijaAtlas FromParameter(int parameter, int width, int height)
    {
        int patterns = parameter / 1000;
        if (patterns <= 0 || width <= 0 || height <= 0 || width % patterns != 0)
            throw new InvalidDataException("Invalid vija strip geometry or packed parameter.");
        return new(patterns, parameter % 1000, width / patterns, height);
    }
}

public readonly record struct NativeVijaResource(string Path, int? Parameter);

public static class NativeVijaResources
{
    public const string Prefix = @"j\duel\effect\";
    public const string Manifest = Prefix + "vija.txt";
    private static readonly string[] Names = [
        "vija_skel.bmp", "vija_smoke.yga", "vija_moji_iro.bmp",
        "vija_moji_mask.bmp", "vija_moji_smo.yga"
    ];

    // DAT manifest inventory in the private cumulative v7 SQLite.
    // Manifest order is not proof of the virtual bank remap.
    private static readonly (string Name, int? Parameter, int Width, int Height)[] ObservedBank =
    [
        ("vija_smoke.yga",5004,400,80),
        ("vija_skel.bmp",5004,210,41),
        ("vija_moji_smo.yga",8031,256,37),
        ("vija_moji_mask.bmp",5031,160,37),
        ("vija_moji_iro.bmp",null,160,37)
    ];

    public static NativeVijaTexture ValidateTexture(int key,NativeVijaResource record,int width,int height)
    {
        if(key<0 || key>=ObservedBank.Length)
            throw new ArgumentOutOfRangeException(nameof(key));
        var expected=ObservedBank[key];
        if(!record.Path.Equals(Prefix+expected.Name,StringComparison.OrdinalIgnoreCase) ||
            record.Parameter!=expected.Parameter || width!=expected.Width || height!=expected.Height)
            throw new InvalidDataException("Unexpected Vija manifest order, parameter or dimensions: "+record.Path);
        return new(width,height,record.Parameter ?? 0);
    }

    // Preserve original order and absence of numeric parameters. The
    // exact numeric fields/ordering must come from the owner's DAT.
    public static NativeVijaResource[] Parse(string text, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        var expected = Names.Select(n => Prefix + n).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<NativeVijaResource> records = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string name = fields[0].Replace('/', '\\');
            string path = name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix + name : throw new InvalidDataException("Unexpected vija path.");
            if (!expected.Remove(path) || !available.Contains(path) || fields.Length > 2)
                throw new InvalidDataException("Missing, duplicate or unsupported vija resource: " + path);
            int? parameter = null;
            if (fields.Length == 2)
            {
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    throw new InvalidDataException("Unsupported vija parameter: " + line);
                parameter = value;
            }
            records.Add(new(path, parameter));
        }
        if (expected.Count != 0) throw new InvalidDataException("Incomplete vija.txt.");
        return records.ToArray();
    }
}
