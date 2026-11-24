using System.Globalization;

namespace Joey.LookAndFeel;

public readonly record struct NativeStatusResource(string Path, int Columns);

/// <summary>
/// Runtime card.txt bank. The first four confirmed focus entries anchor
/// the indices; the remaining filenames and divisors come only from DAT.
/// The actual private card.txt has not been exercised in this environment.
/// </summary>
public static class NativeCardStatusResources
{
    public const string Prefix = @"j\duel\card\";
    public const string Manifest = Prefix + "card.txt";

    public static NativeStatusResource[] Parse(string text, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        List<NativeStatusResource> resources = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string name = words[0].Replace('/', '\\');
            if (!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unknown card.txt resource record: " + line);
            string path = name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix + name :
                throw new InvalidDataException("Unexpected card.txt path: " + name);
            if (path.Split('\\').Any(p => p is "." or ".." or "") || !available.Contains(path))
                throw new InvalidDataException("Missing card.txt resource: " + path);
            int columns = 0;
            // The auxiliary integer is a divisor for 4..9 only. Never
            // infer it from image dimensions; reject ambiguous records.
            if (resources.Count is >= 4 and <= 9 &&
                (words.Length != 2 || !int.TryParse(words[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out columns) || columns <= 0))
                throw new InvalidDataException("Missing or ambiguous status atlas divisor: " + line);
            resources.Add(new(path, columns));
        }
        string[] anchors = [NativeCardSelectionFrame.Small, NativeCardSelectionFrame.Large,
            NativeCardSelectionFrame.SmallAlternate, NativeCardSelectionFrame.LargeAlternate];
        if (resources.Count < 14 || !resources.Take(4).Select(x => x.Path)
            .SequenceEqual(anchors, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected card.txt index anchors or count.");
        int[] minimumColumns = [6, 20, 6, 8, 4, 8];
        for (int i = 4; i <= 9; i++)
            if (resources[i].Columns < minimumColumns[i - 4])
                throw new InvalidDataException("Status atlas cannot contain the native frame range: " + resources[i].Path);
        return resources.ToArray();
    }
}
