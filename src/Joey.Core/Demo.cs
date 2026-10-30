namespace Joey.Core;
// DEMONSTRATION ONLY: fixed hands/LP, unrestricted summon count, no tribute/effects.
public sealed class DemoDuel
{
    public List<Card> Hand { get; }
    public Card?[,] Monsters { get; } = new Card?[2, 5];
    public int[] Life { get; } = [8000, 8000];
    public int Turn { get; private set; } = 1;
    public int ActivePlayer => (Turn - 1) % 2;
    public DemoDuel(IEnumerable<Card> cards)
    {
        var all = cards.Where(c => c.Name.Length > 0).ToList();
        Hand = all.Where(c => c.IsMonster).Take(4).Concat(all.Where(c => !c.IsMonster).Take(2)).ToList();
        Monsters[1, 2] = all.FirstOrDefault(c => c.IsMonster);
    }
    public bool Summon(int handIndex, int player, int zone, bool monsterZone, out string message)
    {
        message = "Choose an empty monster zone on your side.";
        if (ActivePlayer != 0) { message = "Advance the turn to return to the player."; return false; }
        if (handIndex < 0 || handIndex >= Hand.Count || player != 0 || zone < 0 || zone >= 5 || !monsterZone || Monsters[player, zone] != null) return false;
        if (!Hand[handIndex].IsMonster) { message = "Only monsters can be summoned here."; return false; }
        Monsters[player, zone] = Hand[handIndex]; Hand.RemoveAt(handIndex);
        message = "Demo summon completed (no tribute or effect rules)."; return true;
    }
    public void NextTurn() => Turn++;
}
