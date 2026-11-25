namespace Joey.LookAndFeel;

public readonly record struct NativeCursorManifestEntry(string Path, string? RawParameter);

/// <summary>English cursor manifest metadata recovered from the private DAT
/// inventory. Manifest positions are NOT native action codes or hotspots.</summary>
public static class NativeCursorManifest
{
    private static readonly (string Name, string? Parameter, int Size)[] Evidence =
    [
        ("cursor_pow.yga",null,63),
        ("cursor00.yga","007014",64), ("cursor01.yga","007014",64),
        ("cursor_e_summon.yga","007014",64), ("cursor_e_flip.yga","007014",64),
        ("cursor_e_atk.yga","007014",64), ("cursor_e_act.yga","007014",64),
        ("cursor_e_set.yga","007014",64), ("cursor_e_setdef.yga","007014",64),
        ("cursor_e_setatk.yga","007014",64), ("cursor_e_draw.yga","007014",64),
        ("cursor_e_surrender.yga","043015",64), ("cursor_e_fusion.yga","007014",64),
        ("cursor_ssummon_a.yga","007030",80), ("cursor_ssummon_d.yga","007030",80),
        ("cursor_e_over.yga","007014",64)
    ];

    public static NativeCursorManifestEntry[] ParseEnglish(string text, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<NativeCursorManifestEntry> entries = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//",2)[0].Trim();
            if (line.Length == 0) continue;
            string[] words = line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if (entries.Count >= Evidence.Length || words.Length > 2)
                throw new InvalidDataException("Unexpected cursor manifest record: " + line);
            var expected = Evidence[entries.Count];
            string path = @"cursor\" + words[0].Replace('?', 'e');
            string? parameter = words.Length == 2 ? words[1] : null;
            if (!path.Equals(@"cursor\" + expected.Name,StringComparison.OrdinalIgnoreCase) ||
                parameter != expected.Parameter || !available.Contains(path))
                throw new InvalidDataException("Unexpected original cursor entry: " + line);
            entries.Add(new(path,parameter));
        }
        if (entries.Count != Evidence.Length)
            throw new InvalidDataException("Incomplete English cursor manifest.");
        return [.. entries];
    }

    public static void ValidateDimensions(int index,int width,int height)
    {
        if (index < 0 || index >= Evidence.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (width != Evidence[index].Size || height != Evidence[index].Size)
            throw new InvalidDataException("Unexpected cursor dimensions: " + Evidence[index].Name);
    }
}
