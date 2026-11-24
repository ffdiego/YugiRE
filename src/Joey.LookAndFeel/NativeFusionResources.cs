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
        // The number and order of the orbital resources remain unknown.
        if (paths.Count < 3) throw new InvalidDataException("Fusion bank must contain at least indices 0..2.");
        return paths.ToArray();
    }
}
