using Joey.Core;
using Joey.LookAndFeel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Headless metadata-only inspection of the user's own data.dat, including
/// both j/duel effects and j/bust_up character/reaction cut-ins. The report
/// contains small .txt manifests and image dimensions/hashes, never image
/// pixels, card assets, executable code, or reconstructed artwork.
/// </summary>
static class VisualManifestProbe
{
    private const int MaxManifestBytes = 8192;
    private const string HelpPrefix = @"j\duel\help\";
    private const string BustUpPrefix = @"j\bust_up\";

    private sealed record ManifestRecord(string Name, int Bytes, string Latin1, string Hex);
    private sealed record ImageRecord(string Name, uint StoredBytes, uint DecodedBytes,
        int? Width, int? Height, string? Sha256, string? Error, YgaAlphaSummary? YgaAlpha = null);
    private sealed record YgaAlphaSummary(int TransparentPixels, int PartialAlphaPixels,
        int OpaquePixels, int NonTransparentBlackPixels, string TopLeftRgb, int NonTransparentTopLeftMatches);
    private sealed record HelpRequest(int Key, string Pattern, string[] MissingLanguages,
        ImageRecord[] Variants);
    private sealed record ReactionGeometry(string Manifest, string BaseResource, ImageRecord? BaseImage,
        CardPoint Origin, CardPoint[] SourceQuad, CardPoint[] DestinationQuad);

    private static YgaAlphaSummary InspectYgaAlpha(ReadOnlySpan<byte> rgba)
    {
        var origin = rgba[..3];
        int transparent = 0, partial = 0, opaque = 0, black = 0, matchesOrigin = 0;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] == 0)
            {
                transparent++;
                continue;
            }
            if (rgba[i + 3] == 255) opaque++;
            else partial++;
            if ((rgba[i] | rgba[i + 1] | rgba[i + 2]) == 0) black++;
            if (rgba.Slice(i, 3).SequenceEqual(origin)) matchesOrigin++;
        }
        return new(transparent, partial, opaque, black, Convert.ToHexString(origin), matchesOrigin);
    }

    private static HelpRequest[]? InspectHelpBank(IEnumerable<ManifestRecord> manifests,
        IEnumerable<ImageRecord> images)
    {
        var manifest = manifests.FirstOrDefault(record =>
            record.Name.Equals(HelpPrefix + "help.txt", StringComparison.OrdinalIgnoreCase));
        if (manifest is null) return null;
        var lookup = images.Where(image => image.Name.StartsWith(HelpPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(image => image.Name, StringComparer.OrdinalIgnoreCase);
        var requests = new List<HelpRequest>();
        foreach (var line in manifest.Latin1.Split('\n'))
        {
            // The original loader treats nonempty manifest rows as indexed
            // entries. Comments contain non-ASCII bytes and have no key.
            string pattern = line.Split("//", 2)[0].Trim();
            if (pattern.Length == 0) continue;
            if (!pattern.EndsWith("_?.yga", StringComparison.OrdinalIgnoreCase) ||
                pattern.Count(c => c == '?') != 1 || pattern.IndexOfAny(['/', '\\', ':']) >= 0 ||
                pattern.Any(char.IsWhiteSpace))
                throw new InvalidDataException("Invalid help manifest resource pattern: " + pattern);

            var variants = new List<ImageRecord>();
            var missingLanguages = new List<string>();
            foreach (char language in "efgijs")
            {
                var resource = HelpPrefix + pattern.Replace('?', language);
                if (lookup.TryGetValue(resource, out var image)) variants.Add(image);
                else missingLanguages.Add(language.ToString());
            }
            requests.Add(new(requests.Count, pattern, [..missingLanguages], [..variants]));
        }
        return [..requests];
    }

    private static ReactionGeometry[] InspectReactionGeometry(IEnumerable<ManifestRecord> manifests,
        IEnumerable<ImageRecord> images)
    {
        var lookup = images.Where(image => image.Name.StartsWith(BustUpPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(image => image.Name, StringComparer.OrdinalIgnoreCase);
        const string reactionPrefix = BustUpPrefix + "reaction";
        var result = new List<ReactionGeometry>();
        foreach (var manifest in manifests)
        {
            if (!manifest.Name.StartsWith(reactionPrefix, StringComparison.OrdinalIgnoreCase) ||
                !manifest.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                manifest.Name.Length != reactionPrefix.Length + 6 ||
                !char.IsAsciiDigit(manifest.Name[reactionPrefix.Length]) ||
                !char.IsAsciiDigit(manifest.Name[reactionPrefix.Length + 1]))
                continue;

            string[][] rows = manifest.Latin1.Split('\n')
                .Select(line => line.Split("//", 2)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Where(words => words.Length > 0).Take(5).ToArray();
            if (rows.Length != 5)
                throw new InvalidDataException("Incomplete reaction geometry: " + manifest.Name);

            var points = new CardPoint[5];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Length != 2 ||
                    !int.TryParse(rows[i][1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int packed))
                    throw new InvalidDataException($"Invalid reaction coordinate at key {i}: {manifest.Name}");
                points[i] = new(packed / 1000, packed % 1000);
            }

            // 00416C48..00416D47 reads keys 0, 1, 2, 4, 3 in this order.
            var origin = points[0];
            CardPoint[] source = [points[1], points[2], points[4], points[3]];
            var destination = source.Select(point => new CardPoint(point.X + origin.X, point.Y + origin.Y)).ToArray();
            string resource = BustUpPrefix + rows[0][0].Replace('/', '\\');
            result.Add(new(manifest.Name, resource, lookup.GetValueOrDefault(resource), origin, source, destination));
        }
        return [..result];
    }

    public static int Export(Archive archive, string outputPath = "scratch/visual-review/dat-manifests.json")
    {
        ArgumentNullException.ThrowIfNull(archive);
        var entries = archive.Entries
            .Where(entry => NativeVisualArchiveScope.Includes(entry.Name))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var manifests = new List<ManifestRecord>();
        var images = new List<ImageRecord>();
        foreach (var entry in entries)
        {
            bool manifest = entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            bool bitmap = entry.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);
            bool yga = entry.Name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase);
            if (!manifest && !bitmap && !yga) continue;

            // No manifest may silently be truncated: that's worse than
            // knowing that its resource order has not yet been recovered.
            if (manifest)
            {
                if (entry.Size > MaxManifestBytes)
                    throw new InvalidDataException($"Manifest exceeds {MaxManifestBytes} bytes: {entry.Name}");
                byte[] text = archive.Read(entry.Name);
                manifests.Add(new(entry.Name, text.Length,
                    Encoding.Latin1.GetString(text), Convert.ToHexString(text)));
                continue;
            }

            try
            {
                byte[] bytes = archive.Read(entry.Name);
                int width, height;
                YgaAlphaSummary? alpha = null;
                if (bitmap)
                {
                    var info = BitmapInfo.Read(bytes);
                    width = info.Width;
                    height = info.Height;
                }
                else
                {
                    var info = NativeYgaSprite.Decode(bytes,
                        static (data, count) => Archive.Decode(data, count),
                        YgaTransparencyMode.SourceAlpha);
                    width = info.Width;
                    height = info.Height;
                    alpha = InspectYgaAlpha(info.Rgba);
                }

                images.Add(new(entry.Name, entry.Stored, entry.Size, width, height,
                    Convert.ToHexString(SHA256.HashData(bytes)), null, alpha));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or
                                           OverflowException or ArgumentException)
            {
                images.Add(new(entry.Name, entry.Stored, entry.Size, null, null,
                    null, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        var report = new
        {
            schemaVersion = 1,
            source = "data.dat original — manifest text and graphics metadata only",
            archiveEntries = archive.Entries.Count,
            manifests,
            images,
            nativeAlphaAllocation = Enumerable.Range(2, 7).Select(deviceFormat => new
            {
                deviceFormat,
                softwareFormat = NativeSurfaceColorLayout.SelectSourceAlphaFormat(deviceFormat)
            }),
            helpRequests = InspectHelpBank(manifests, images),
            bustUpReactionGeometry = InspectReactionGeometry(manifests, images)
        };

        string? folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"[visual-dat] {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"[visual-dat] {manifests.Count} manifests, {images.Count} image metadata records");
        return images.Any(x => x.Error is not null) ? 2 : 0;
    }
}
