namespace Joey.Core;

public enum DemoPhase { Main, Battle, End }

// Temporary local two-player turn engine. Deck order and progression are demo policy.
// Selected RE-backed battle/spell rules are marked individually; see src/README.md.
public sealed class DemoDuel
{
    private readonly List<Card>[] hands = [[], []];
    private readonly Queue<Card>[] decks = [new(), new()];
    private readonly List<Card>[] graves = [[], []];
    public record Equipment(Card Card, int Owner, int SourceZone, int TargetPlayer, int TargetZone);
    private readonly List<Equipment> equipment = [];
    public IReadOnlyList<Equipment> EquipmentCards => equipment.AsReadOnly();
    private readonly bool[,] attacked = new bool[2, 5];
    private readonly bool[,] changedPosition = new bool[2, 5];
    public bool[,] DefensePosition { get; } = new bool[2, 5];
    private bool summoned, drawn;
    public IReadOnlyList<Card> Hand => hands[ActivePlayer];
    public IReadOnlyList<Card> Graveyard(int player) => graves[player].AsReadOnly();
    public int DeckCount(int player) => decks[player].Count;
    public Card?[,] Monsters { get; } = new Card?[2, 5];
    public Card?[,] Spells { get; } = new Card?[2, 5];
    public int[] Life { get; } = [8000, 8000];
    public int Turn { get; private set; } = 1;
    public int ActivePlayer => (Turn - 1) % 2;
    public DemoPhase Phase { get; private set; }
    public int? Winner { get; private set; }
    public const int EndTurnHandLimit = 6;
    // All placed demo spells are face-up and active; native suppression/negation is not modeled.
    public bool HandLimitDisabled => Spells.Cast<Card?>().Any(card => card?.Id == 0x593);
    public int PendingEndDiscards => Phase == DemoPhase.End && !HandLimitDisabled ? Math.Max(0, Hand.Count - EndTurnHandLimit) : 0;
    private DemoDuel() { }
    // Demo orchestration: seeded shuffle and identical-deck local matches are not native RE claims.
    public static DemoDuel FromDecks(IReadOnlyList<Card> first, IReadOnlyList<Card> second, int seed = 1979)
    {
        if (first.Count is < 40 or > 80 || second.Count is < 40 or > 80)
            throw new ArgumentException("Imported main decks must contain 40 to 80 cards");
        var duel = new DemoDuel();
        var random = new Random(seed);
        for (int player = 0; player < 2; player++)
        {
            Card[] shuffled = (player == 0 ? first : second).ToArray();
            random.Shuffle(shuffled);
            duel.hands[player].AddRange(shuffled.Take(5));
            foreach (var card in shuffled.Skip(5)) duel.decks[player].Enqueue(card);
        }
        return duel;
    }
    public DemoDuel(IEnumerable<Card> cards)
    {
        var all = cards.Where(c => c.Name.Length > 0).ToList();
        var monsters = all.Where(c => c.IsMonster).Take(12).ToList();
        var warrior = all.FirstOrDefault(c => c.ClassCode == 15);
        if (warrior != null && !monsters.Take(3).Contains(warrior)) monsters.Insert(0, warrior);
        var spells = all.Where(c => c.Id is 300 or 342 or 345).OrderBy(c => c.Id == 300 ? 1 : 0).ToList();
        // Fixed reproducible fixture; both players have an opening hand and a small deck.
        for (int p = 0; p < 2; p++)
        {
            hands[p].AddRange(monsters.Take(3)); hands[p].AddRange(spells);
            foreach (var c in monsters.Skip(3).Concat(spells)) decks[p].Enqueue(c);
        }
    }
    public static int NormalTributes(Card card) => card.Level <= 4 ? 0 : card.Level <= 6 ? 1 : 2;
    public bool Summon(int index, int player, int zone, bool monsterZone, out string message, IReadOnlyList<int>? tributeZones = null)
    {
        message = "Choose an empty monster zone on the active player's side.";
        if (Winner != null || Phase != DemoPhase.Main || summoned) { message = "Summon unavailable: main phase, once per turn."; return false; }
        if (index < 0 || index >= Hand.Count || player != ActivePlayer || zone < 0 || zone >= 5 || !monsterZone) return false;
        if (!Hand[index].IsMonster) { message = "Only monsters can be summoned here."; return false; }
        if (NormalSummonRules.RequiresSpecialSummon(Hand[index]))
        { message = "This monster requires a special summon, which is not implemented in this demo."; return false; }
        int required = NormalTributes(Hand[index]);
        int[] tributes = tributeZones?.ToArray() ?? [];
        if (tributes.Length != required)
        { message = $"This monster needs {required} tribute(s). Select own monsters, then a destination."; return false; }
        if (tributes.Distinct().Count() != tributes.Length || tributes.Any(z => z < 0 || z >= 5 || Monsters[player, z] == null))
        { message = "Tributes must be distinct occupied monster zones on your side."; return false; }
        if (Monsters[player, zone] != null && !tributes.Contains(zone))
        { message = "Choose an empty zone or one of the selected tribute zones."; return false; }
        // RE base count: 0x005AAB90. All validation precedes payment in this compact demo transaction.
        // Special summon restrictions, Soul Exchange and token-specific restrictions remain unsupported.
        foreach (int tribute in tributes) RemoveMonster(player, tribute);
        Monsters[player, zone] = Hand[index]; hands[player].RemoveAt(index); summoned = true;
        DefensePosition[player, zone] = false;
        message = $"Monster summoned in attack position; {required} tribute(s)."; return true;
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
        message = "Use Sparks, Ookazi or Infinite Cards; select a Warrior on the field for Legendary Sword.";
        if (Winner != null || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count) return false;
        if (Hand[index].Id == 0x593)
        {
            int zone = Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
            if (zone < 0) { message = "Infinite Cards needs a free spell zone."; return false; }
            Spells[ActivePlayer, zone] = Hand[index]; hands[ActivePlayer].RemoveAt(index);
            message = "Infinite Cards active: neither player has an end-turn hand limit.";
            return true;
        }
        // Proven RE effect amount and opponent targeting: docs/cards/sparks-ookazi.md.
        // Immediate activation and graveyard movement here are demo orchestration, no chains.
        int damage = Hand[index].Id switch { 342 => 200, 345 => 800, _ => 0 };
        if (damage == 0) return false;
        var card = Hand[index]; hands[ActivePlayer].RemoveAt(index); graves[ActivePlayer].Add(card);
        Damage(1 - ActivePlayer, damage); message = $"{card.Name}: {damage} damage to opponent."; return true;
    }
    public int AttackValue(int p, int z) => (Monsters[p, z]?.Attack ?? 0) + Bonus(p, z);
    public int DefenseValue(int p, int z) => (Monsters[p, z]?.Defense ?? 0) + Bonus(p, z);
    private int Bonus(int p, int z) => Monsters[p, z]?.ClassCode == 15 ? equipment.Count(e => e.TargetPlayer == p && e.TargetZone == z) * 300 : 0;
    public bool Equip(int index, int targetPlayer, int targetZone, out string message)
    {
        message = "Legendary Sword needs a face-up Warrior and a free own spell zone.";
        if (Winner != null || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count || Hand[index].Id != 300 || targetPlayer < 0 || targetPlayer > 1 || targetZone < 0 || targetZone >= 5 || Monsters[targetPlayer, targetZone]?.ClassCode != 15) return false;
        int sourceZone = Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
        if (sourceZone < 0) return false;
        // RE: docs/cards/legendary-sword.md. Target may be on either side, +300/+300.
        // Demo substitutes static class for dynamic type; no type-changing effects exist here.
        equipment.Add(new(Hand[index], ActivePlayer, sourceZone, targetPlayer, targetZone));
        Spells[ActivePlayer, sourceZone] = Hand[index]; hands[ActivePlayer].RemoveAt(index);
        message = "Legendary Sword equipped: +300 ATK / +300 DEF."; return true;
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
            attacked[p, source] = true; Damage(enemy, AttackValue(p, source)); message = $"Direct attack: {AttackValue(p, source)} damage."; return true;
        }
        if (target < 0 || target >= 5 || Monsters[enemy, target] is not Card defender) return false;
        attacked[p, source] = true;
        if (DefensePosition[enemy, target])
        {
            int difference = AttackValue(p, source) - DefenseValue(enemy, target);
            if (difference > 0) RemoveMonster(enemy, target);
            else if (difference < 0) Damage(p, -difference);
            message = difference > 0 ? "Defense overcome: defender destroyed, no LP damage." : difference == 0 ? "Equal ATK/DEF: no destruction or damage." : $"Defense holds: attacker takes {-difference} damage, neither destroyed.";
            return true;
        }
        int delta = AttackValue(p, source) - AttackValue(enemy, target);
        // RE: docs/22-battle-core.md, 0x005B82EC..0x005B82EE: equal ATK destroys only above zero.
        if (delta == 0 && AttackValue(p, source) == 0)
        {
            message = "Zero ATK tie: neither monster destroyed, no damage.";
            return true;
        }
        if (delta >= 0) RemoveMonster(enemy, target);
        if (delta <= 0) RemoveMonster(p, source);
        if (delta > 0) Damage(enemy, delta);
        if (delta < 0) Damage(p, -delta);
        message = delta == 0 ? "Equal ATK: both monsters destroyed, no damage." : $"Battle: {Math.Abs(delta)} damage; weaker monster destroyed.";
        return true;
    }
    private void RemoveMonster(int p, int z)
    {
        graves[p].Add(Monsters[p, z]!); Monsters[p, z] = null; DefensePosition[p, z] = false;
        // Common cleanup only: no Banisher/token/trigger exceptions in this sandbox.
        foreach (var e in equipment.Where(e => e.TargetPlayer == p && e.TargetZone == z).ToArray())
        { graves[e.Owner].Add(e.Card); Spells[e.Owner, e.SourceZone] = null; equipment.Remove(e); }
    }
    private void Damage(int p, int value) { Life[p] = Math.Max(0, Life[p] - value); if (Life[p] == 0) Winner = 1 - p; }
    public void NextTurn() => EndTurn(out _);
    public bool EndTurn(out string message)
    {
        message = "The duel has ended.";
        if (Winner != null) return false;
        // RE: 0x005A58BC..0x005A58F7. Other end-phase effects and native suppression remain unsupported.
        if (!HandLimitDisabled && Hand.Count > EndTurnHandLimit)
        {
            Phase = DemoPhase.End;
            message = $"End phase: select and discard {PendingEndDiscards} card(s) to keep six.";
            return true;
        }
        AdvanceTurn(); message = "Turn changed. Draw once; summon once; Battle to attack.";
        return true;
    }
    public bool DiscardForEndTurn(int index, out string message)
    {
        message = "Select a hand card while resolving the end-phase hand limit.";
        if (Winner != null || PendingEndDiscards == 0 || index < 0 || index >= Hand.Count) return false;
        var card = hands[ActivePlayer][index];
        hands[ActivePlayer].RemoveAt(index); graves[ActivePlayer].Add(card);
        if (PendingEndDiscards > 0) message = $"Discarded {card.Name}. Discard {PendingEndDiscards} more card(s).";
        else { AdvanceTurn(); message = $"Discarded {card.Name}. Turn changed."; }
        return true;
    }
    private void AdvanceTurn()
    {
        Turn++; Phase = DemoPhase.Main; summoned = drawn = false; Array.Clear(attacked); Array.Clear(changedPosition);
    }
}
