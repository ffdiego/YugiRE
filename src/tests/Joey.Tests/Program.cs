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
string malformed = Path.Combine(Path.GetTempPath(), "joey-dat-" + Guid.NewGuid() + ".dat");
try
{
    File.WriteAllBytes(malformed, Encoding.ASCII.GetBytes("BADMAGIC0000"));
    Reject(() => { using var a = new Archive(malformed); }, "Reject invalid DAT signature");
    using (var writer = new BinaryWriter(File.Create(malformed)))
    {
        writer.Write(Encoding.ASCII.GetBytes("KCEJYUGI")); writer.Write(1u);
    }
    Reject(() => { using var a = new Archive(malformed); }, "Reject truncated DAT index");
    using (var writer = new BinaryWriter(File.Create(malformed)))
    {
        writer.Write(Encoding.ASCII.GetBytes("KCEJYUGI")); writer.Write(1u);
        writer.Write(new byte[256]); writer.Write(280u); writer.Write(1u); writer.Write(1u);
    }
    Reject(() => { using var a = new Archive(malformed); }, "Reject payload beyond EOF");
}
finally { File.Delete(malformed); }
var weak = new Card(1, 1, "Weak", "", (4u << 25) | (100u << 9));
var strong = new Card(2, 2, "Strong", "", (4u << 25) | (200u << 9));
var sparks = new Card(3, 342, "Sparks", "", 21u << 20);
var ookazi = new Card(4, 345, "Ookazi", "", 21u << 20);
var demo = new DemoDuel([weak, strong, weak, strong, weak, sparks, ookazi]);
Check(!demo.Summon(0, 0, 0, false, out _) && demo.Hand.Count == 5, "Reject incompatible zone without hand mutation");
Check(!demo.Summon(0, 1, 0, true, out _), "Reject inactive side");
Check(!demo.Summon(0, 0, 5, true, out _), "Reject invalid zone");
Check(demo.Summon(1, 0, 0, true, out _) && demo.Hand.Count == 4, "Summon from hand");
Check(!demo.Summon(0, 0, 1, true, out _), "Only one demo normal summon per turn");
Check(!demo.BeginBattle(out _), "No first-turn battle");
Check(demo.Draw(out _) && demo.Hand.Count == 5 && demo.DeckCount(0) == 3, "Draw conserves deck and hand cards");
Check(!demo.Draw(out _), "No second draw same turn");
Check(demo.Play(2, out _) && demo.Life[1] == 7800 && demo.Graveyard(0).Count == 1, "Sparks damages opponent and enters graveyard");
Check(demo.Play(2, out _) && demo.Life[1] == 7000, "Ookazi damages opponent by 800");
Check(!demo.Play(0, out _), "Unsupported effects leave cards in hand");
demo.NextTurn();
Check(demo.ActivePlayer == 1 && demo.Hand.Count == 5, "Turns expose the active local player's hand");
Check(demo.Summon(0, 1, 0, true, out _) && demo.BeginBattle(out _), "Other player can summon and enter battle");
Check(!demo.Attack(0, -1, out _), "Monsters block direct attack");
Check(demo.Attack(0, 0, out _) && demo.Life[1] == 6000 && demo.Monsters[1, 0] == null && demo.Monsters[0, 0] == strong, "Weaker attacker destroyed and owner takes difference");
demo.NextTurn();
Check(demo.BeginBattle(out _) && demo.Attack(0, -1, out _) && demo.Life[1] == 4000, "Direct attack damages LP");
Check(!demo.Attack(0, -1, out _), "Attacker cannot attack twice");
var equal = new DemoDuel([weak, weak, weak]);
equal.Summon(0, 0, 0, true, out _); equal.NextTurn(); equal.Summon(0, 1, 0, true, out _); equal.BeginBattle(out _);
Check(equal.Attack(0, 0, out _) && equal.Monsters[0, 0] == null && equal.Monsters[1, 0] == null && equal.Life[0] == 8000, "Equal attack destroys both without damage");
var winning = new DemoDuel([strong, weak, weak, sparks, ookazi]);
winning.Summon(0, 0, 0, true, out _); winning.NextTurn(); winning.Summon(0, 1, 0, true, out _); winning.NextTurn(); winning.BeginBattle(out _);
// Change the defender fixture to exercise the winning-attacker branch explicitly.
winning.Monsters[1, 0] = weak;
Check(winning.Attack(0, 0, out _) && winning.Life[1] == 7000 && winning.Graveyard(1).Count == 1 && winning.Monsters[0, 0] == strong, "Stronger attacker destroys defender and damages opponent");
var lethal = new DemoDuel([weak, strong, weak, ookazi]);
lethal.Life[1] = 500;
Check(lethal.Play(3, out _) && lethal.Life[1] == 0 && lethal.Winner == 0, "Spell damage saturates at zero and declares winner");
int finalTurn = lethal.Turn; lethal.NextTurn();
Check(lethal.Turn == finalTurn && !lethal.BeginBattle(out _), "Game over locks turn and battle progression");
var empty = new DemoDuel([]);
Check(empty.Draw(out _) && empty.Winner == 1, "Drawing from empty deck ends demo");
Check(!empty.Draw(out _) && !empty.Play(0, out _), "No actions after game over");
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
