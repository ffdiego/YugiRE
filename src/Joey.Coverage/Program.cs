using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Joey.Core;

// Read-only bridge: descriptions consumed by Catalog.Load never leave this process.
if (args.Length != 1) throw new ArgumentException("Usage: Joey.Coverage <data.dat>");
using var archive = new Archive(args[0]);
string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
var ids = archive.Read(@"bin#\card_id.bin");
var pack = archive.Read(@"bin#\card_pack.bin");
if (pack.Length != ids.Length) throw new InvalidDataException("Inconsistent card_pack length");
var loaded = Catalog.Load(archive);
var helperAliases = (from left in loaded from right in loaded
    where left.Index < right.Index && left.Id != right.Id && Math.Abs(left.Id - right.Id) != 2000
        && NativeAiAttributes.EquivalentResources(left.Id, right.Id)
    select new { leftIndex = left.Index, rightIndex = right.Index }).ToArray();
var cards = loaded.Select(c => new {
    internalIndex = c.Index, resourceId = c.Id, name = c.Name,
    properties = c.Properties, classCode = c.ClassCode, attack = c.Attack,
    defense = c.Defense, level = c.Level,
    pack = BinaryPrimitives.ReadUInt16LittleEndian(pack.AsSpan(c.Index * 2))
}).ToArray();
using var dat = File.OpenRead(args[0]);
Console.Write(JsonSerializer.Serialize(new {
    fingerprints = new {
        datSha256 = Convert.ToHexStringLower(SHA256.HashData(dat)),
        cardIdSha256 = Hash(ids), namesSha256 = Hash(archive.Read(@"bin#\card_nameeng.bin")),
        propertiesSha256 = Hash(archive.Read(@"bin#\card_prop.bin"))
    }, packSha256 = Hash(pack), cards, helperAliases
}));
