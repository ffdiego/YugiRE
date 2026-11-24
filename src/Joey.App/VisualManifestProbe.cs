using Joey.Core;
using Joey.LookAndFeel;
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

    private sealed record ManifestRecord(string Name, int Bytes, string Latin1, string Hex);
    private sealed record ImageRecord(string Name, uint StoredBytes, uint DecodedBytes,
        int? Width, int? Height, string? Sha256, string? Error);

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
                if (bitmap)
                {
                    var info = BitmapInfo.Read(bytes);
                    width = info.Width;
                    height = info.Height;
                }
                else
                {
                    var info = NativeYgaSprite.Decode(bytes,
                        static (data, count) => Archive.Decode(data, count));
                    width = info.Width;
                    height = info.Height;
                }

                images.Add(new(entry.Name, entry.Stored, entry.Size, width, height,
                    Convert.ToHexString(SHA256.HashData(bytes)), null));
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
            images
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
