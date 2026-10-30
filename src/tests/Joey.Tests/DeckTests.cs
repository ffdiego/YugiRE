using Joey.Core;

static class DeckTests
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] bytes = Convert.FromHexString("0100000091B6B15402000000F50701002C030000");
        var deck = DeckFile.Read(bytes);
        check(deck.HeaderHex == "0100000091B6B154" && deck.Groups.Count == 3 && deck.Groups[0].SequenceEqual(new ushort[] { 0, 2037 }) &&
            deck.Groups[1].SequenceEqual(new ushort[] { 812 }) && deck.Groups[2].Count == 0, "YDC fixture: header, zero ID, extended ID and three lists");
        var blue = new Card(816, 0, "Blue", "", 1u << 20);
        var gaia = new Card(32, 2037, "Gaia variant", "", 1u << 20);
        check(deck.Resolve(0, [gaia, blue]).SequenceEqual([blue, gaia]) && !deck.HasDuelSize,
            "YDC resolves resource IDs rather than table indices; short decks remain inspectable");
        reject(() => deck.Resolve(1, [blue]), "YDC unknown card ID is reported");
        reject(() => DeckFile.Read(bytes[..^1]), "Reject truncated YDC list");
        reject(() => DeckFile.Read([.. bytes, 0]), "Reject YDC trailing bytes");
        reject(() => DeckFile.Read(new byte[DeckFile.MaximumBytes + 1]), "Reject oversized YDC payload");
        var bad = (byte[])bytes.Clone(); bad[8] = 81;
        reject(() => DeckFile.Read(bad), "Reject YDC main list beyond 80");
        bad = (byte[])bytes.Clone(); bad[14] = 16;
        reject(() => DeckFile.Read(bad), "Reject YDC secondary list beyond 15");
        // Both endpoints accepted by the native 0x0044AA00 size check.
        foreach (int count in new[] { 40, 80 })
        {
            byte[] full = new byte[14 + count * 2]; full[8] = (byte)count;
            var parsed = DeckFile.Read(full);
            check(parsed.HasDuelSize && parsed.Resolve(0, [blue]).Count == count,
                $"YDC main size {count} preserves repeated copies");
        }
        var first = Enumerable.Repeat(blue, 40).ToArray();
        var second = Enumerable.Repeat(gaia, 80).ToArray();
        var duel = DemoDuel.FromDecks(first, second);
        check(duel.Hand.Count == 5 && duel.DeckCount(0) == 35 && duel.Hand.All(c => c == blue) && first.Length == 40,
            "Imported demo deals five cards and conserves player one's main deck");
        duel.NextTurn();
        check(duel.Hand.Count == 5 && duel.DeckCount(1) == 75 && duel.Hand.All(c => c == gaia),
            "Imported demo keeps player decks independent");
        check(duel.Draw(out _) && duel.Hand.Count == 6 && duel.DeckCount(1) == 74,
            "Imported demo draw conserves hand and deck cards");
        bool rejected = false;
        try { DemoDuel.FromDecks([], second); } catch (ArgumentException) { rejected = true; }
        check(rejected, "Imported demo rejects undersized main decks");
    }
    public static void Original(string path, IReadOnlyList<Card> cards, Action<bool, string> check)
    {
        var deck = DeckFile.Load(path);
        check(deck.Groups.Select(g => g.Count).SequenceEqual(new[] { 42, 1, 0 }), "Reference YDC counts are 42 / 1 / 0");
        var main = deck.Resolve(0, cards);
        check(main.Take(3).Select(c => c.Id).SequenceEqual(new ushort[] { 528, 722, 531 }) && main[0].Name == "Flame Cerebrus",
            "Reference YDC order and first card match existing RE evidence");
        check(main.Count(c => c.Id == 531) == 2, "Reference YDC preserves both Gemini Elf copies");
        check(deck.Resolve(1, cards).Single().Name == "Alligator's Sword Dragon" && deck.HasDuelSize,
            "Reference YDC secondary list resolves its original card");
        var duel = DemoDuel.FromDecks(main, main);
        check(duel.Hand.Count == 5 && duel.DeckCount(0) == 37 && duel.DeckCount(1) == 37 &&
            duel.Hand.All(card => main.Contains(card)), "Reference YDC starts a local demo using its real main deck");
        check(duel.Draw(out _) && duel.Hand.Count == 6 && duel.DeckCount(0) == 36,
            "Reference YDC draw uses imported cards");
    }
}
