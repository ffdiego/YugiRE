namespace Joey.LookAndFeel;

public readonly record struct NativeDiceResource(string Path,int? Parameter);

/// <summary>Diagnostic manifest-order adapter, not a claim that the
/// native virtual remapping always equals file order. Reject unsupported
/// syntax or keys rather than silently substituting another resource.</summary>
public static class NativeDiceResources
{
    public const string Prefix=@"j\duel\effect\";
    public const string Manifest=Prefix+"dice.txt";

    // Original DAT dimensions, including duplicated shadow2 at keys 3 and 5.
    public static NativeDiceTexture ValidateTexture(int key,NativeDiceResource record,int width,int height)
    {
        var expected = key switch
        {
            0 => new NativeDiceTexture(270,46,6),
            1 => new NativeDiceTexture(288,48,6),
            2 or 4 => new NativeDiceTexture(512,39,16),
            3 or 5 => new NativeDiceTexture(152,43,4),
            6 => new NativeDiceTexture(41,44,1),
            >=7 and <=12 => new NativeDiceTexture(32,35,1),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        if (width!=expected.Width || height!=expected.Height ||
            (key<6 ? record.Parameter!=expected.Patterns : record.Parameter is not null))
            throw new InvalidDataException("Unexpected dice texture or divisor: "+record.Path);
        return expected;
    }

    public static bool IsDiceBitmap(string path) =>
        path.StartsWith(Prefix+"dice_",StringComparison.OrdinalIgnoreCase) &&
        path.EndsWith(".bmp",StringComparison.OrdinalIgnoreCase);

    public static NativeDiceResource[] Parse(string text,IEnumerable<string> archiveNames)
    {
        var available=new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<NativeDiceResource> records=[];
        foreach (string raw in text.Split('\n'))
        {
            string line=raw.Split("//",2)[0].Trim();
            if (line.Length==0) continue;
            string[] fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            string name=fields[0].Replace('/','\\');
            string path=name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix+name : throw new InvalidDataException("Unexpected dice path.");
            if (!path.StartsWith(Prefix+"dice_",StringComparison.OrdinalIgnoreCase) ||
                path.Split('\\').Any(p=>p is "." or ".." or "") ||
                !(path.EndsWith(".bmp",StringComparison.OrdinalIgnoreCase) ||
                  path.EndsWith(".yga",StringComparison.OrdinalIgnoreCase)) ||
                !available.Contains(path) || fields.Length>2)
                throw new InvalidDataException("Unsupported dice record: "+line);
            int? parameter=null;
            if(fields.Length==2)
            {
                if (!int.TryParse(fields[1],System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,out int value))
                    throw new InvalidDataException("Unsupported dice parameter: "+line);
                parameter=value;
            }
            records.Add(new(path,parameter));
        }
        if(records.Count<13) throw new InvalidDataException("Dice manifest does not expose keys 0..12 in order.");
        for(int k=0;k<6;k++)
            if(records[k].Parameter is not >0)
                throw new InvalidDataException("Missing strip divisor at dice key "+k);
        // These names are known from the original DAT inventory. They
        // provide an independent sanity check of the assumed key mapping.
        if(!records[6].Path.Equals(Prefix+"dice_dci_sha.yga",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Dice decision shadow not at expected key 6.");
        for(int i=1;i<=6;i++)
            if(!records[i+6].Path.Equals(Prefix+$"dice_decide{i}.bmp",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Dice decision resource not at expected key "+(i+6));
        return records.ToArray();
    }
}
