namespace Joey.LookAndFeel;

public readonly record struct NativeCoinResource(string Path,int Patterns,int Width,int Height);

/// <summary>DAT manifest order adapter for coin review. The actual manifest
/// and image dimensions were inspected in the private v3 SQLite. Native
/// virtual bank remapping and compositor are not verified by this parser.</summary>
public static class NativeCoinResources
{
    public const string Prefix=@"j\duel\effect\";
    public const string Manifest=Prefix+"coin.txt";
    private static readonly NativeCoinResource[] Expected=
    [
        new(Prefix+"coin_rotate.bmp",14,560,40),
        new(Prefix+"coin_sha.yga",4,168,42),
        new(Prefix+"coin_ripple.yga",1,64,64),
        new(Prefix+"coin_mask.bmp",1,40,40),
        new(Prefix+"coin_ref.bmp",1,69,40),
        new(Prefix+"coin_kirari.yga",4,56,14)
    ];

    public static NativeCoinResource[] Parse(string text,IEnumerable<string> archiveNames)
    {
        ArgumentNullException.ThrowIfNull(text);
        var available=new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<NativeCoinResource> records=[];
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Split("//",2)[0].Trim();
            if(line.Length==0)continue;
            string[] fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(records.Count>=Expected.Length)
                throw new InvalidDataException("Unexpected additional coin manifest record.");
            var expected=Expected[records.Count];
            string name=fields[0].Replace('/','\\');
            string path=name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)?name:Prefix+name;
            if(!path.Equals(expected.Path,StringComparison.OrdinalIgnoreCase)||!available.Contains(path))
                throw new InvalidDataException("Unexpected or missing coin resource: "+name);
            if(expected.Patterns==1 ? fields.Length!=1 :
                fields.Length!=2 || !int.TryParse(fields[1],System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,out int n) || n!=expected.Patterns)
                throw new InvalidDataException("Unexpected coin strip divisor: "+line);
            records.Add(expected);
        }
        if(records.Count!=Expected.Length)throw new InvalidDataException("Incomplete coin manifest.");
        return records.ToArray();
    }

    public static NativeDiceTexture ValidateTexture(NativeCoinResource record,int width,int height)
    {
        if(width!=record.Width||height!=record.Height)
            throw new InvalidDataException("Unexpected coin texture dimensions: "+record.Path);
        var texture=new NativeDiceTexture(width,height,record.Patterns);
        _=texture.PatternWidth;
        return texture;
    }

    // Diagnostic color-key policy; never apply to reflection/mask resources.
    public static bool IsCoinFaceBitmap(string name) =>
        name.Equals(Prefix+"coin_rotate.bmp",StringComparison.OrdinalIgnoreCase);
}
