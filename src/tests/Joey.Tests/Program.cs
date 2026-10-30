using Joey.Core;
using System.Security.Cryptography;
using System.Text;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name)
{
    try { action(); } catch (InvalidDataException) { Check(true, name); return; }
    throw new Exception("Expected rejection: " + name);
}
byte[] compressed = Convert.FromHexString("DF6C696E652FEEF15F78BF2E626D700D0AEEF77900F9F3");
Check(Encoding.ASCII.GetString(Archive.Decode(compressed, 34)) == "line/line_x.bmp\r\nline/line_y.bmp\r\n", "Known LZSS literal and overlapping-reference fixture");
Reject(() => Archive.Decode(compressed[..^1], 34), "Reject truncated compressed input");
Reject(() => Archive.Decode([..compressed, 0], 34), "Reject compressed trailing data");
Reject(() => Archive.Decode([], -1), "Reject negative output allocation");
Reject(() => Archive.Decode([], 17 * 1024 * 1024), "Reject oversized output allocation");
var monster = new Card(1, 1, "Synthetic monster", "", 4u << 25);
var spell = new Card(2, 2, "Synthetic spell", "", 21u << 20);
var demo = new DemoDuel([monster, monster, monster, monster, spell]);
Check(!demo.Summon(0, 0, 0, false, out _) && demo.Hand.Count == 5, "Reject monster in spell zone without consuming hand");
Check(!demo.Summon(0, 1, 0, true, out _), "Reject opponent zone");
Check(!demo.Summon(0, 0, 5, true, out _), "Reject invalid zone");
Check(demo.Summon(0, 0, 0, true, out _) && demo.Hand.Count == 4 && demo.Monsters[0, 0] == monster, "Move hand monster to field");
Check(!demo.Summon(0, 0, 0, true, out _) && demo.Hand.Count == 4, "Reject occupied zone");
Check(!demo.Summon(3, 0, 1, true, out _), "Reject spell summon");
demo.NextTurn();
Check(!demo.Summon(0, 0, 1, true, out _), "Reject summon during opponent turn");
demo.NextTurn();
Check(demo.Summon(0, 0, 1, true, out _), "Allow action on next player turn");
if (args.Length > 0)
{
    using var archive = new Archive(args[0]);
    Check(archive.Entries.Count == 3568, "Original archive index count");
    Check(Convert.ToHexString(SHA256.HashData(archive.Read(@"bin#\card_id.bin"))).Equals("A36CDE2192FA85D9CCBCDB0DDB8BA8494A5199EA31D11A50A70B5FD32BF5879C"), "Original ID resource hash");
    Check(Convert.ToHexString(SHA256.HashData(archive.Read(@"bin#\card_nameeng.bin"))).Equals("74848D0B2CD7D5FAC7A767075BD5B01E0366183CE934AFA6070CE24394A24032"), "Original names resource hash");
    var cards = Catalog.Load(archive);
    Check(cards.Count == 1114, "Non-sentinel records retained including variants");
    Check(cards.Single(c => c.Id == 0).Name == "Blue-Eyes White Dragon", "ID zero is valid");
    Check(cards.Single(c => c.Id == 762).Name == "Parasite Paracide", "Known card identity");
    Check(cards.Single(c => c.Id == 0).Attack == 3000 && cards.Single(c => c.Id == 0).Defense == 2500, "Known monster base stats");
}
Console.WriteLine($"{passed} checks passed.");
