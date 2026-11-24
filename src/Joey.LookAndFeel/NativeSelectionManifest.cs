using System.Globalization;

namespace Joey.LookAndFeel;

public readonly record struct NativeSelectionBackground(string Resource, int SlotYOffset);

/// <summary>
/// Parent list manifest: two background/offset records and a scroll manifest.
/// 0043A060 sets byte +1FC when total > 5, uses that resource's dimensions,
/// and 0043ABA0 reads its associated integer from the 8-byte resource entry.
/// Scroll and icon geometry are not assigned speculative meanings here.
/// </summary>
public sealed record NativeSelectionManifest(NativeSelectionBackground WithoutScroll,
    NativeSelectionBackground WithScroll, string ScrollManifest, int ScrollAuxiliary)
{
    private const string Prefix = @"j\duel\list\";

    public NativeSelectionBackground Background(int totalCards) =>
        totalCards > NativeSelectionWindow.VisibleSlots ? WithScroll : WithoutScroll;

    // Ordinary modes: 0043A060 appends '1' + (side == ((~perspective)&1)).
    // Mode 0x29 has an additional side flip and is outside this integration.
    public static int SideVariant(int side, int perspective)
    {
        if (side is not (0 or 1) || perspective is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(side));
        return side == perspective ? 1 : 2;
    }

    public static NativeSelectionManifest Parse(string text, IEnumerable<string> archiveNames)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        List<(string Path, int Value)> records = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 2 || words[0].Contains('\\') || words[0].Contains('/') ||
                !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                throw new InvalidDataException("Unsupported selection manifest record: " + line);
            string path = Prefix + words[0];
            if (!available.Contains(path))
                throw new InvalidDataException("Missing selection resource: " + path);
            records.Add((path, number));
        }
        if (records.Count != 3 ||
            !records[0].Path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
            !records[1].Path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
            !records[2].Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected two selection backgrounds and one scroll manifest.");
        return new(new(records[0].Path, records[0].Value),
            new(records[1].Path, records[1].Value), records[2].Path, records[2].Value);
    }

    // Stable branch 0043B3F9: X - width/2, Y - height/2.
    // Whole bitmap placement; compositing still follows the client policy.
    public static CardBounds BackgroundBounds(int width, int height) =>
        Centered(NativeSelectionWindow.TargetCenter, width, height);

    public static CardBounds SlotBounds(int slot, int backgroundHeight, int slotYOffset,
        int miniWidth, int miniHeight) =>
        Centered(NativeSelectionWindow.SlotAnchor(NativeSelectionWindow.TargetCenter.X,
            NativeSelectionWindow.TargetCenter.Y, slotYOffset, backgroundHeight, slot),
            miniWidth, miniHeight);

    private static CardBounds Centered(CardPoint center, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        return new(center.X - width / 2, center.Y - height / 2, width, height);
    }
}
