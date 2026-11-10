using System.Security.Cryptography;
using System.Text.Json;
using Joey.Core;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/tools/Joey.DeckTrace -- capture.jsonl path/to/data.dat");
    return 2;
}
try
{
    using (var input = File.OpenRead(args[1]))
        if (!Convert.ToHexString(SHA256.HashData(input)).Equals(CpuDeckTraceVerifier.DatSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsupported DAT SHA256");
    using var archive = new Archive(args[1]);
    var catalog = Catalog.Load(archive);
    var records = File.ReadLines(args[0]).Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => { using var document = JsonDocument.Parse(line); return document.RootElement.Clone(); }).ToArray();
    if (records.Length == 0) throw new InvalidDataException("Empty trace");
    var report = CpuDeckTraceVerifier.Validate(records[0],records[1..],path => {
        string key = path.Replace('/', '\\');
        if (!key.StartsWith("data\\",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsupported resource path");
        var deck = DeckFile.Read(archive.Read(key[5..]));
        return new[] {0,2,1}.Select(group => (IReadOnlyList<int>)deck.Resolve(group,catalog).Select(card=>card.Index).ToArray()).ToArray();
    });
    Console.WriteLine(JsonSerializer.Serialize(new {report.ExitCode,report.NormalSelections,report.Issues},new JsonSerializerOptions {WriteIndented=true}));
    return report.ExitCode;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or KeyNotFoundException)
{
    Console.Error.WriteLine("Incomplete/unsupported evidence: " + ex.Message);
    return 2;
}
