namespace Joey.LookAndFeel;

public readonly record struct NativeChainDigit(CardBounds Source, CardBounds Destination, int Intensity);

public static class NativeChainDraw
{
    public const string Manifest = NativeDisabledChainResources.Prefix + "chain.txt";

    // The lookup virtual in 005DC9F4 is an identity (0043A5D0).
    // Resource loading indexes the parsed list in 00549BA0.
    public static string[] Resources(string manifest, IEnumerable<string> archiveNames, char language)
    {
        var available = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        var resources = new List<string>();
        foreach (var raw in manifest.Split('\n'))
        {
            var name = raw.Split("//", 2)[0].Trim().Replace('?', language);
            if (name.Length == 0) continue;
            var path = NativeDisabledChainResources.Prefix + name;
            if (!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                name.Contains('\\') || name.Contains('/') || !available.Contains(path))
                throw new InvalidDataException($"Missing or invalid chain.txt resource: {path}");
            resources.Add(path);
        }
        if (resources.Count != 26)
            throw new InvalidDataException("Expected 26 ordered bitmap records in chain.txt.");
        return resources.ToArray();
    }

    // 0041F95D / 0041FBCD use the whole selected sprite at this origin.
    public static CardBounds Sprite(CardPoint center, int width, int height) =>
        new(center.X - width / 2, center.Y - height / 2 - 16, width, height);

    // 0041FAB1 / 0041FD1B take an atlas slice and unnormalized intensity.
    // Keep the raw argument, including 256 at the held frame.
    public static NativeChainDigit[] Digits(NativeChainTimeline timeline, CardPoint center,
        int atlasWidth, int atlasHeight)
    {
        if (!timeline.HasNumberPass) return [];

        int step = timeline.Counter / NativeChainTimeline.Step;
        int intensity = step <= 32 ? Math.Max(0, step - 16) : 48 - step;
        int factor = 32 - intensity;
        int digitWidth = atlasWidth / 10;
        int width = digitWidth * factor / 16;
        int height = atlasHeight * factor / 16;
        int x = center.X - width / 2;
        int y = center.Y + 22 - height / 2;

        List<NativeChainDigit> passes = [];
        for (int number = timeline.Number; number != 0; number /= 10)
        {
            int digit = number % 10;
            int left = digit * atlasWidth / 10;
            int right = (digit + 1) * atlasWidth / 10;
            passes.Add(new(new(left, 0, right - left, atlasHeight),
                new(x, y, width, height), intensity << 4));
            x -= digitWidth;
        }
        return passes.ToArray();
    }
}
