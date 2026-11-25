using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>
/// Ordered bank shared by 004520E0 and 004874C0. Do not infer image
/// names or reorder by filename. The private fusion.txt still needs
/// to be exercised with the original archive.
/// </summary>
public static class NativeFusionResources
{
    public const string Prefix = @"j\duel\effect\";
    public const string Manifest = Prefix + "fusion.txt";
    private static readonly string[] ObservedNames=["fusion_mark.yga","fusion_uzu.yga","fusion_cir.yga",
        "fu_kira_a1.bmp","fu_kira_a2.bmp","fu_kira_a3.bmp","fu_kira_b1.bmp","fu_kira_b2.bmp","fu_kira_b3.bmp"];
    private static readonly int[] Widths=[76,274,194,10,14,18,10,16,22];
    private static readonly int[] Heights=[86,274,194,10,14,18,10,16,22];

    // Strict diagnostic adapter for the original DAT observed in v3;
    // native bank remapping is still separate from manifest order.
    public static void ValidateObservedBank(IReadOnlyList<string> paths)
    {
        if(paths.Count!=ObservedNames.Length||
            !paths.SequenceEqual(ObservedNames.Select(n=>Prefix+n),StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected fusion bank order.");
    }

    public static CardBounds ValidateObservedSize(int key,int width,int height)
    {
        if(key<0||key>=Widths.Length||width!=Widths[key]||height!=Heights[key])
            throw new InvalidDataException("Unexpected fusion resource dimensions.");
        return new(0,0,width,height);
    }

    public static bool IsOrbitalBitmap(string path)=>
        ObservedNames.Skip(3).Any(n=>path.Equals(Prefix+n,StringComparison.OrdinalIgnoreCase));


    public static string[] Parse(string text, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        List<string> paths = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string name = fields[0].Replace('/', '\\');
            if ((!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) &&
                 !name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase)) ||
                fields.Skip(1).Any(field => !int.TryParse(field, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out _)))
                throw new InvalidDataException("Unsupported fusion.txt resource record: " + line);
            string path = name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix + name :
                throw new InvalidDataException("Unexpected fusion resource path: " + name);
            if (path.Split('\\').Any(p => p is "." or ".." or "") || !available.Contains(path))
                throw new InvalidDataException("Missing fusion resource: " + path);
            paths.Add(path);
        }
        // 004877F0 references index 0, 004524F0 indices 1 and 2.
        // Strict observed-bank validation is opt-in for the effects review.
        if (paths.Count < 3) throw new InvalidDataException("Fusion bank must contain at least indices 0..2.");
        return paths.ToArray();
    }
}
