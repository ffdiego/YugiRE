using Joey.Core;

static class InfiniteCardsTests
{
    public static void Run(Action<bool, string> check)
    {
        var infinite = new Card(951, 0x593, "Infinite Cards", "", 21u << 20);
        var monster = new Card(1, 1, "Monster", "", (1u << 20) | (4u << 25));
        var sword = new Card(2, 300, "Legendary Sword", "", 21u << 20);
        var duel = DemoDuel.FromDecks(Enumerable.Repeat(infinite, 40).ToArray(), Enumerable.Repeat(monster, 40).ToArray());
        check(duel.Play(0, out _) && duel.Spells[0, 0] == infinite && duel.Hand.Count == 4 &&
            duel.Graveyard(0).Count == 0 && duel.HandLimitDisabled, "Infinite Cards remains in a spell zone instead of entering the graveyard");
        duel.EndTurn(out _); duel.Draw(out _); duel.EndTurn(out _); duel.EndTurn(out _); duel.Draw(out _);
        check(duel.Hand.Count == 7 && duel.EndTurn(out _) && duel.Turn == 5 && duel.PendingEndDiscards == 0,
            "An opponent's Infinite Cards permits ending with seven cards");
        duel.EndTurn(out _);
        duel.Spells[0, 0] = null; // Fixture removal; spell destruction itself is not part of this demo.
        check(!duel.HandLimitDisabled && duel.EndTurn(out _) && duel.PendingEndDiscards == 1 && duel.Turn == 6,
            "Hand limit follows the current field rather than a sticky activation flag");
        var full = DemoDuel.FromDecks(Enumerable.Repeat(infinite, 40).ToArray(), Enumerable.Repeat(monster, 40).ToArray());
        for (int zone = 0; zone < 5; zone++) full.Spells[0, zone] = sword;
        check(!full.Play(0, out _) && full.Hand.Count == 5 && full.Graveyard(0).Count == 0 && !full.HandLimitDisabled,
            "No free spell zone rejects Infinite Cards without hand loss");
        var equipped = DemoDuel.FromDecks(Enumerable.Repeat(sword, 40).ToArray(), Enumerable.Repeat(monster, 40).ToArray());
        equipped.Spells[0, 0] = infinite;
        equipped.Monsters[0, 0] = new Card(3, 3, "Warrior", "", (15u << 20) | (4u << 25));
        check(equipped.Equip(0, 0, 0, out _) && equipped.EquipmentCards.Single().SourceZone == 1 &&
            equipped.Spells[0, 0] == infinite && equipped.Spells[0, 1] == sword,
            "Continuous spells and equipment share the same five source zones");
    }
}
