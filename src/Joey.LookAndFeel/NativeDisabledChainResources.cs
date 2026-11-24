namespace Joey.LookAndFeel;

/// <summary>
/// Conservative reader for the six whole images referenced by 004531C0.
/// Uses the already-supported native text-list convention (filename followed
/// by optional integer fields). Rejects unknown syntax rather than skipping
/// records and shifting texture indices. Real disable.txt remains untested.
/// </summary>
public static class NativeDisabledChainResources
{
    public const string Prefix = @"j\duel\chain\";
    public const string Manifest = Prefix + "disable.txt";

    public static string[] Parse(string manifest, IEnumerable<string> archiveNames)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        List<string> names = [];
        foreach (string raw in manifest.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string name = fields[0].Replace('/', '\\');
            if ((!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) &&
                 !name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase)) ||
                fields.Skip(1).Any(field => !int.TryParse(field,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out _)))
                throw new InvalidDataException("Unsupported disable.txt resource record: " + line);

            // Do not resolve arbitrary basenames or silently select a similar
            // resource from another archive directory.
            string path = name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? name :
                name.StartsWith(@"duel\chain\", StringComparison.OrdinalIgnoreCase) ? @"j\" + name :
                name.StartsWith(@"chain\", StringComparison.OrdinalIgnoreCase) ? @"j\duel\" + name :
                !name.Contains('\\') ? Prefix + name : throw new InvalidDataException("Unexpected chain resource path: " + name);
            if (path.Split('\\').Any(part => part is "." or ".." or "") || !available.Contains(path))
                throw new InvalidDataException("Missing or invalid disable.txt resource: " + path);
            names.Add(path);
        }
        if (names.Count != NativeDisabledChainTimeline.FrameCount)
            throw new InvalidDataException("Expected six ordered whole images in disable.txt.");
        return names.ToArray();
    }
}
