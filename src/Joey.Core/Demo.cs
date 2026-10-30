namespace Joey.Core;

public enum DemoPhase { Main, Battle }

// Temporary local two-player sandbox, NOT a reconstruction of the original turn engine.
// Deck order, summon restrictions and battle rules below are demonstration policy.
public sealed class DemoDuel
{
    private readonly List<Card>[] hands = [[], []];
    private readonly Queue<Card>[] decks = [new(), new()];
    private readonly List<Card>[] graves = [[], []];
    private readonly bool[,] attacked = new bool[2, 5];
    private readonly bool[,] changedPosition = new bool[2, 5];
    public bool[,] DefensePosition { get; } = new bool[2, 5];
    private bool summoned, drawn;
    public IReadOnlyList<Card> Hand => hands[ActivePlayer];
    public IReadOnlyList<Card> Graveyard(int player) => graves[player].AsReadOnly();
    public int DeckCount(int player) => decks[player].Count;
    public Card?[,] Monsters { get; } = new Card?[2, 5];
    public int[] Life { get; } = [8000, 8000];
    public int Turn { get; private set; } = 1;
    public int ActivePlayer => (Turn - 1) % 2;
    public DemoPhase Phase { get; private set; }
    public int? Winner { get; private set; }
    public DemoDuel(IEnumerable<Card> cards)
    {
        var all = cards.Where(c => c.Name.Length > 0).ToList();
        var monsters = all.Where(c => c.IsMonster).Take(12).ToList();
        var spells = all.Where(c => c.Id is 342 or 345).ToList();
        // Fixed reproducible fixture; both players have an opening hand and a small deck.
        for (int p = 0; p < 2; p++)
        {
            hands[p].AddRange(monsters.Take(3)); hands[p].AddRange(spells);
            foreach (var c in monsters.Skip(3).Concat(spells)) decks[p].Enqueue(c);
        }
    }
    public bool Summon(int index, int player, int zone, bool monsterZone, out string message)
    {
        message = "Choose an empty monster zone on the active player's side.";
        if (Winner != null || Phase != DemoPhase.Main || summoned) { message = "Summon unavailable: main phase, once per turn."; return false; }
        if (index < 0 || index >= Hand.Count || player != ActivePlayer || zone < 0 || zone >= 5 || !monsterZone || Monsters[player, zone] != null) return false;
        if (!Hand[index].IsMonster) { message = "Only monsters can be summoned here."; return false; }
        Monsters[player, zone] = Hand[index]; hands[player].RemoveAt(index); summoned = true;
        message = "Monster summoned in attack position (demo: no tribute costs)."; return true;
    }
    public bool Draw(out string message)
    {
        message = "Draw available once per turn in main phase.";
        if (Winner != null || Phase != DemoPhase.Main || drawn) return false;
        if (decks[ActivePlayer].Count == 0) { Winner = 1 - ActivePlayer; message = "Empty deck: active player loses (demo policy)."; return true; }
        hands[ActivePlayer].Add(decks[ActivePlayer].Dequeue()); drawn = true;
        message = "Drew one card."; return true;
    }
    public bool Play(int index, out string message)
    {
        message = "Only Sparks and Ookazi are supported; other effects remain unimplemented.";
        if (Winner != null || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count) return false;
        // Proven RE effect amount and opponent targeting: docs/cards/sparks-ookazi.md.
        // Immediate activation and graveyard movement here are demo orchestration, no chains.
        int damage = Hand[index].Id switch { 342 => 200, 345 => 800, _ => 0 };
        if (damage == 0) return false;
        var card = Hand[index]; hands[ActivePlayer].RemoveAt(index); graves[ActivePlayer].Add(card);
        Damage(1 - ActivePlayer, damage); message = $"{card.Name}: {damage} damage to opponent."; return true;
    }
    public bool ChangePosition(int zone, out string message)
    {
        message = "Position changes require an occupied own zone in main phase, once per turn.";
        if (Winner != null || Phase != DemoPhase.Main || zone < 0 || zone >= 5 || Monsters[ActivePlayer, zone] == null || changedPosition[ActivePlayer, zone]) return false;
        DefensePosition[ActivePlayer, zone] = !DefensePosition[ActivePlayer, zone];
        changedPosition[ActivePlayer, zone] = true;
        message = DefensePosition[ActivePlayer, zone] ? "Monster in face-up defense (demo)." : "Monster in attack position (demo).";
        return true;
    }
    public bool BeginBattle(out string message)
    {
        message = "Battle unavailable on the opening turn or after game over.";
        if (Winner != null || Turn == 1 || Phase != DemoPhase.Main) return false;
        Phase = DemoPhase.Battle; message = "Select your monster, then an enemy monster or Direct attack."; return true;
    }
    public bool Attack(int source, int target, out string message)
    {
        message = "Select an unused attacker and a valid enemy target in battle phase.";
        int p = ActivePlayer, enemy = 1 - p;
        if (Winner != null || Phase != DemoPhase.Battle || source < 0 || source >= 5 || Monsters[p, source] is not Card attacker || attacked[p, source] || DefensePosition[p, source]) return false;
        if (target == -1)
        {
            if (Enumerable.Range(0, 5).Any(z => Monsters[enemy, z] != null)) { message = "Enemy monsters block direct attacks."; return false; }
            attacked[p, source] = true; Damage(enemy, attacker.Attack); message = $"Direct attack: {attacker.Attack} damage."; return true;
        }
        if (target < 0 || target >= 5 || Monsters[enemy, target] is not Card defender) return false;
        attacked[p, source] = true;
        if (DefensePosition[enemy, target])
        {
            int difference = attacker.Attack - defender.Defense;
            if (difference > 0) Destroy(enemy, target);
            else if (difference < 0) Damage(p, -difference);
            message = difference > 0 ? "Defense overcome: defender destroyed, no LP damage." : difference == 0 ? "Equal ATK/DEF: no destruction or damage." : $"Defense holds: attacker takes {-difference} damage, neither destroyed.";
            return true;
        }
        int delta = attacker.Attack - defender.Attack;
        if (delta >= 0) Destroy(enemy, target);
        if (delta <= 0) Destroy(p, source);
        if (delta > 0) Damage(enemy, delta);
        if (delta < 0) Damage(p, -delta);
        message = delta == 0 ? "Equal ATK: both monsters destroyed, no damage." : $"Battle: {Math.Abs(delta)} damage; weaker monster destroyed.";
        return true;
    }
    private void Destroy(int p, int z) { graves[p].Add(Monsters[p, z]!); Monsters[p, z] = null; DefensePosition[p, z] = false; }
    private void Damage(int p, int value) { Life[p] = Math.Max(0, Life[p] - value); if (Life[p] == 0) Winner = 1 - p; }
    public void NextTurn()
    {
        if (Winner != null) return;
        Turn++; Phase = DemoPhase.Main; summoned = drawn = false; Array.Clear(attacked); Array.Clear(changedPosition);
    }
}
