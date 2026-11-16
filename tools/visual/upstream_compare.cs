// Offline differential harness for derplayer's three LZSS classes, supplied separately.
// Never reference or execute the mod launcher's game/process code.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using YuGiOh_PoC_Patcher.YuGi;

using var inventory = JsonDocument.Parse(File.ReadAllText(args[1]));
using var dat = File.OpenRead(args[0]);
int count = 0, matches = 0, exactLengths = 0;
foreach (var row in inventory.RootElement.GetProperty("resources").EnumerateArray())
{
    if (row.GetProperty("extension").GetString() != ".yga") continue;
    if (row.GetProperty("size").GetInt32() != row.GetProperty("stored").GetInt32())
        throw new InvalidDataException("Harness expects uncompressed DAT members");
    dat.Position = row.GetProperty("offset").GetInt64();
    var data = new byte[row.GetProperty("size").GetInt32()];
    dat.ReadExactly(data);
    int size = BitConverter.ToInt32(data, 16);
    var decoded = YuGiLZSS.Decompress(data[24..], size);
    string expected = row.GetProperty("yga").GetProperty("pixel_sha256").GetString()!;
    string hash = Convert.ToHexString(SHA256.HashData(decoded.AsSpan(0, size))).ToLowerInvariant();
    count++;
    if (decoded.Length == size) exactLengths++;
    if (hash == expected) matches++;
    else throw new InvalidDataException("Codec mismatch: " + row.GetProperty("name").GetString());
}
Console.WriteLine(JsonSerializer.Serialize(new { count, matches, exactLengths, nativeExecution = false }));
if (count != 537 || matches != 537) throw new InvalidDataException("Incomplete corpus");
