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
var zero = new Card(20, 20, "Zero ATK test monster", "", 4u << 25);
var zeroBattle = new DemoDuel([zero, zero, zero]);
zeroBattle.Summon(0, 0, 0, true, out _); zeroBattle.NextTurn(); zeroBattle.Summon(0, 1, 0, true, out _); zeroBattle.BeginBattle(out _);
Check(zeroBattle.Attack(0, 0, out _) && zeroBattle.Monsters[0, 0] != null && zeroBattle.Monsters[1, 0] != null && zeroBattle.Graveyard(0).Count == 0 && zeroBattle.Graveyard(1).Count == 0 && zeroBattle.Life[0] == 8000 && zeroBattle.Life[1] == 8000, "Native zero-ATK tie preserves both monsters and LP");
Check(!zeroBattle.Attack(0, 0, out _), "Zero-damage attack still spends the attack allowance");
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
foreach (int defense in new[] { 500, 1000, 2000 })
{
    var wall = new Card(7, 7, "Wall", "", (4u << 25) | (uint)(defense / 10));
    var defenseDuel = new DemoDuel([weak, wall, weak]);
    defenseDuel.Summon(0, 0, 0, true, out _); defenseDuel.NextTurn(); defenseDuel.Summon(1, 1, 0, true, out _);
    Check(defenseDuel.ChangePosition(0, out _) && !defenseDuel.ChangePosition(0, out _), $"Defense {defense}: position changes once per turn");
    defenseDuel.BeginBattle(out _);
    Check(!defenseDuel.Attack(0, 0, out _) && !defenseDuel.ChangePosition(0, out _), $"Defense {defense}: cannot attack or change position during battle");
    defenseDuel.NextTurn(); defenseDuel.BeginBattle(out _);
    Check(defenseDuel.Attack(0, 0, out _) && defenseDuel.Monsters[0, 0] != null && (defenseDuel.Monsters[1, 0] == null) == (defense < 1000) && defenseDuel.Life[0] == (defense > 1000 ? 7000 : 8000) && defenseDuel.Life[1] == 8000, $"ATK 1000 against DEF {defense}: correct casualties and damage");
}
var warrior = new Card(9, 9, "Warrior", "", (15u << 20) | (100u << 9) | 100u);
var sword = new Card(10, 300, "Legendary Sword", "", 21u << 20);
var equipped = new DemoDuel([warrior, strong, weak, sword]);
Check(!equipped.Equip(3, 0, 0, out _) && equipped.Hand.Count == 4, "Cannot equip empty zone; preserve hand");
equipped.Summon(0, 0, 0, true, out _);
Check(equipped.Equip(2, 0, 0, out _) && equipped.AttackValue(0, 0) == 1300 && equipped.DefenseValue(0, 0) == 1300 && equipped.EquipmentCards.Count == 1, "Sword grants Warrior +300/+300 and occupies a spell zone");
equipped.NextTurn(); equipped.Summon(1, 1, 0, true, out _);
Check(!equipped.Equip(2, 1, 0, out _) && equipped.Hand.Count == 3, "Sword rejects non-Warrior");
Check(equipped.Equip(2, 0, 0, out _) && equipped.AttackValue(0, 0) == 1600, "Sword can target enemy Warrior and bonuses stack");
equipped.BeginBattle(out _); equipped.Attack(0, 0, out _);
Check(equipped.Monsters[0, 0] == null && equipped.EquipmentCards.Count == 0 && equipped.Graveyard(0).Count == 2 && equipped.Graveyard(1).Count == 1, "Destroyed target cleans both owners' equipment to their graveyards");
equipped.NextTurn(); equipped.Summon(0, 0, 0, true, out _);
Check(equipped.AttackValue(0, 0) == 2000, "Reused monster slot does not inherit equipment bonus");
var empty = new DemoDuel([]);
Check(empty.Draw(out _) && empty.Winner == 1, "Drawing from empty deck ends demo");
Check(!empty.Draw(out _) && !empty.Play(0, out _), "No actions after game over");
GraphicsTests.Run(Check, Reject);
DeckTests.Run(Check, Reject);
if (args.Length > 0)
{
    using var archive = new Archive(args[0]);
    Check(BitmapInfo.Read(archive.Read(MenuResources.Background)) == new BitmapInfo(800, 600), "Original menu background dimensions");
    Check(BitmapInfo.Read(archive.Read(MenuResources.Normal)) == new BitmapInfo(400, 250), "Original menu button atlas dimensions");
    Check(MenuResources.Load(archive).Buttons == new SceneSprite("menu_?_ani0.bmp", 200, 320), "Original menu scene position");
    Check(archive.Entries.Count == 3568, "Original archive index count");
    Check(Convert.ToHexString(SHA256.HashData(archive.Read(@"bin#\card_id.bin"))).Equals("A36CDE2192FA85D9CCBCDB0DDB8BA8494A5199EA31D11A50A70B5FD32BF5879C"), "Original ID resource hash");
    Check(Convert.ToHexString(SHA256.HashData(archive.Read(@"bin#\card_nameeng.bin"))).Equals("74848D0B2CD7D5FAC7A767075BD5B01E0366183CE934AFA6070CE24394A24032"), "Original names resource hash");
    var cards = Catalog.Load(archive);
    if (args.Length > 1) DeckTests.Original(args[1], cards, Check);
    var listResources = CardListResources.Load(archive);
    var packs = archive.Read(@"bin#\card_pack.bin");
    var gameCards = CardListResources.ForPacks(cards, packs);
    Check(gameCards.Count == 771 && CardListResources.ForPacks(cards, packs, 1).Count == 155 &&
        CardListResources.ForPacks(cards, packs, 2).Count == 315 && CardListResources.ForPacks(cards, packs, 4).Count == 350,
        "Original per-game counts include overlap: union is 771, not 820");
    var allArtwork = CardArtwork.Load(archive, cards);
    Check(gameCards.All(c => !allArtwork.For(c)!.StartsWith(@"card\token_", StringComparison.OrdinalIgnoreCase)),
        "Original pack membership excludes shared token placeholders");
    Check(listResources.Grid == new CardGrid(225, 110) && listResources.Previous.X == 408 && listResources.Previous.Y == 565 &&
        listResources.Next.X == 549 && listResources.Next.Y == 565, "Original grid and pagination sprite positions");
    Check(BitmapInfo.Read(archive.Read(CardListResources.Frame)) == new BitmapInfo(58, 80) &&
        BitmapInfo.Read(archive.Read(CardListResources.Left)) == new BitmapInfo(49, 23) &&
        BitmapInfo.Read(archive.Read(CardListResources.Digits)) == new BitmapInfo(120, 21), "Original frame, arrow and digit atlas dimensions");
    var alphabetical = CardListResources.Sort(cards, archive.Read(@"bin#\card_sorteng.bin"));
    Check(alphabetical.Take(6).Select(c => c.Name).SequenceEqual(new[] { "30,000-Year White Turtle", "4-Starred Ladybug of Doom",
        "7 Colored Fish", "7 Completed", "Abyss Flower", "Acid Crawler" }), "Original English ranks yield alphabetical card names");
    Check(alphabetical.Count == cards.Count && alphabetical.Select(c => c.Index).ToHashSet().SetEquals(cards.Select(c => c.Index)) &&
        alphabetical[^1].Name.Length == 0, "Alphabetical view retains variants and unnamed records");
    var artwork = CardArtwork.Load(archive, cards);
    Check(artwork.Count == cards.Count, "Original artwork manifest covers every non-sentinel record with matching ID");
    Check(artwork.For(cards.Single(c => c.Id == 0)) == @"card\LOB001.bmp", "Blue-Eyes artwork uses explicit manifest mapping");
    Check(artwork.For(cards.Single(c => c.Id == 762)) == @"card\PSV003.bmp", "Parasite artwork uses explicit manifest mapping");
    Check(archive.Entries.Where(e => e.Name.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        .All(e => BitmapInfo.Read(archive.Read(e.Name)) == new BitmapInfo(200, 290)), "Original card BMP dimensions including shared placeholders");
    Check(cards.Count == 1114, "Non-sentinel records retained including variants");
    Check(cards.Single(c => c.Id == 0).Name == "Blue-Eyes White Dragon", "ID zero is valid");
    Check(cards.Single(c => c.Id == 762).Name == "Parasite Paracide", "Known card identity");
    Check(cards.Single(c => c.Id == 0).Attack == 3000 && cards.Single(c => c.Id == 0).Defense == 2500, "Known monster base stats");
}
Console.WriteLine($"{passed} checks passed.");
