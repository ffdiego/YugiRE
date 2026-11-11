using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

public static class CaptureReader
{
    public static CaptureDocument Read(string path, string? expectedSha256 = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (expectedSha256 is not null && !digest.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Capture SHA256 mismatch");
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return Read(reader, Path.GetFullPath(path), digest);
    }

    public static CaptureDocument Read(TextReader reader, string source, string sha256)
    {
        CaptureMetadata? metadata = null;
        JsonElement? footer = null;
        var entries = new List<CaptureEntry>();
        int number = 0;
        while (reader.ReadLine() is { } line)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                var raw = document.RootElement.Clone();
                if (raw.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a JSON object");
                string kind = Fields.String(raw, "kind") ?? throw new InvalidDataException("Missing kind");
                if (metadata is null)
                {
                    if (kind != "metadata") throw new InvalidDataException("First record must be metadata");
                    metadata = new(Fields.String(raw, "mode"), Fields.Int(raw, "formatVersion"),
                        Fields.String(raw, "exeSha256"), Fields.Long(raw, "seen"),
                        Fields.Long(raw, "retained"), Fields.Long(raw, "overwritten"), raw);
                    continue;
                }
                if (kind == "metadata") throw new InvalidDataException("Duplicate metadata header");
                if (footer is not null) throw new InvalidDataException("Record after capture_summary");
                if (kind == "capture_summary") { footer = raw; continue; }
                string? function = Fields.String(raw, "function"), phase = Fields.String(raw, "phase");
                NativeAiStats? stats = null;
                NativeAiCombatEvent? combat = null;
                if (kind == "ai_probe" && phase == "leave" && !Fields.HasValue(raw, "readError"))
                {
                    if (function is "computed_stats" or "combat_event")
                    {
                        string hex = Fields.String(raw, "outputHex") ?? throw new InvalidDataException("Missing native output buffer");
                        byte[] bytes = Convert.FromHexString(hex);
                        if (function == "computed_stats") stats = NativeAiStats.Read(bytes);
                        else combat = NativeAiCombatEvent.Read(bytes);
                    }
                }
                entries.Add(new(number, kind, Fields.Long(raw, "seq"), Fields.Long(raw, "callId"),
                    Fields.Int(raw, "threadId"), Fields.Int(raw, "side"), function, phase,
                    Fields.UInts(raw, kind == "command" ? "args" : "rawArgs"), stats, combat, raw));
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new InvalidDataException($"{source}: line {number}: {error.Message}", error);
            }
        }
        if (metadata is null) throw new InvalidDataException("Empty capture / missing metadata");
        return new(source, sha256, metadata, entries.AsReadOnly(), footer);
    }
}

internal static class Fields
{
    public static bool HasValue(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind != JsonValueKind.Null;
    public static string? String(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).GetString() : null;
    public static int? Int(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).GetInt32() : null;
    public static long? Long(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).GetInt64() : null;
    public static bool? Bool(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).GetBoolean() : null;
    public static uint[]? UInts(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).EnumerateArray().Select(v => v.GetUInt32()).ToArray() : null;
    public static int[]? Ints(JsonElement value, string name) => HasValue(value, name) ? value.GetProperty(name).EnumerateArray().Select(v => v.GetInt32()).ToArray() : null;
}
