using System.Text.Json;

namespace Joey.LookAndFeel;

/// <summary>Private V01 runtime capture, not a generated trigonometric approximation.</summary>
public static class NativeCosineCapture
{
    public const string ExecutableSha256 =
        "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    public const int SampleCount = 512;

    public static int[] Read(string path) => Parse(File.ReadAllText(path));

    public static int[] Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                !string.Equals(root.GetProperty("exeSha256").GetString(), ExecutableSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                root.GetProperty("tableVa").GetString() != "00A52EE8" ||
                root.GetProperty("initializedVa").GetString() != "00A536E8" ||
                root.GetProperty("initialized").GetInt32() != 1)
                throw new InvalidDataException("Unexpected native cosine capture provenance.");

            var values = root.GetProperty("values");
            if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != SampleCount)
                throw new InvalidDataException("Expected 512 signed native cosine samples.");
            var samples = values.EnumerateArray().Select(value => value.GetInt32()).ToArray();
            // An all-zero buffer is not the initialized FCOS table. Do not
            // constrain amplitude or rounding before observing the original.
            if (samples.All(value => value == 0))
                throw new InvalidDataException("Native cosine table is empty.");
            return samples;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or
                                   InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Invalid native cosine capture.", ex);
        }
    }
}
