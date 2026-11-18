namespace Joey.Core;

public enum DemoPhase { Main, Battle, End }

// Temporary local two-player turn engine. Deck order and progression are demo policy.
// Selected RE-backed battle/spell rules are marked individually; see src/README.md.
public sealed class DemoDuel
{
    private readonly List<Card>[] hands = [[], []];
    private readonly Queue<Card>[] decks = [new(), new()];
    private readonly List<Card>[] graves = [[], []];
    public record Equipment(Card Card, int Owner, int SourceZone, int TargetPlayer, int TargetZone, Card TargetCard);
    private readonly List<Equipment> equipment = [];
    public IReadOnlyList<Equipment> EquipmentCards => equipment.AsReadOnly();
    private readonly bool[,] attacked = new bool[2, 5];
    private readonly bool[,] changedPosition = new bool[2, 5];
    public bool[,] DefensePosition { get; } = new bool[2, 5];
    public bool[,] MonsterFaceDown { get; } = new bool[2, 5];
    public bool[,] SpellFaceDown { get; } = new bool[2, 5];
    private bool summoned, drawn;
    private sealed record BattleContinuation(int AttackerSide, int AttackerZone, Card Attacker,
                                              int DefenderSide, int DefenderZone, Card Defender);
    private BattleContinuation? deferredBattle;
    public DemoFlipTarget? PendingFlipTarget { get; private set; }
    public bool IsDraw { get; private set; } // Sandbox draw when both decks cannot complete Morphing Jar.
    public bool GameOver => Winner is not null || IsDraw;
    private bool ActionsBlocked => GameOver || PendingFlipTarget is not null;
    private bool equipmentContextResolved = true; // Closed sandbox: static properties, no generic blockers.
    public IReadOnlyList<Card> Hand => hands[ActivePlayer];
    public IReadOnlyList<Card> HandFor(int player) => player is 0 or 1
        ? hands[player].AsReadOnly() : throw new ArgumentOutOfRangeException(nameof(player));
    public IReadOnlyList<Card> Graveyard(int player) => graves[player].AsReadOnly();
    public int DeckCount(int player) => decks[player].Count;
    public Card?[,] Monsters { get; } = new Card?[2, 5];
    public Card?[,] Spells { get; } = new Card?[2, 5];
    // One dedicated field-spell zone per player; at most one is face-up.
    // These arrays are also writable by bounded sandbox fixtures, like Spells.
    public Card?[] FieldSpells { get; } = new Card?[2];
    public bool[] FieldSpellFaceDown { get; } = new bool[2];
    public Card? ActiveFieldSpell => Enumerable.Range(0, 2)
        .Where(p => !FieldSpellFaceDown[p])
        .Select(p => FieldSpells[p]).FirstOrDefault(card => card is not null);
    public int[] Life { get; } = [8000, 8000];
    public int Turn { get; private set; } = 1;
    public int ActivePlayer => (Turn - 1) % 2;
    public DemoPhase Phase { get; private set; }
    public int? Winner { get; private set; }
    public const int EndTurnHandLimit = 6;
    // Native suppression/negation is not modeled; setting a spell does not activate it.
    public bool HandLimitDisabled => Enumerable.Range(0, 2).Any(p =>
        Enumerable.Range(0, 5).Any(z => Spells[p, z]?.Id == 0x593 && !SpellFaceDown[p, z]));
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
    /// <summary>
    /// Minimal state hydration for differential tests against partially observed
    /// Joey snapshots. This is NOT a native save/load implementation: unknown
    /// deck/graveyard/flags/chain state is deliberately left at demo defaults.
    /// Only use fixtures that explicitly bound which fields are compared. Equipment
    /// context stays unknown unless the caller explicitly opts into closed sandbox policy.
    /// </summary>
    public static DemoDuel FromPartialObservation(
        int activeSide,
        IReadOnlyList<Card> activeHand,
        IReadOnlyList<Card?> side0Monsters,
        IReadOnlyList<Card?> side1Monsters,
        int activeLifePoints,
        bool equipmentContextResolved = false)
    {
        if (activeSide is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(activeSide));
        ArgumentNullException.ThrowIfNull(activeHand);
        ArgumentNullException.ThrowIfNull(side0Monsters);
        ArgumentNullException.ThrowIfNull(side1Monsters);
        if (side0Monsters.Count != 5 || side1Monsters.Count != 5)
            throw new ArgumentException("A partial observation must specify five monster slots per side.");
        if (activeLifePoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(activeLifePoints),
                "This adapter only models an ongoing main phase.");
        if (activeHand.Any(card => card is null))
            throw new ArgumentException("Active hand must not contain null cards.", nameof(activeHand));

        var duel = new DemoDuel { Turn = activeSide + 1, equipmentContextResolved = equipmentContextResolved };
        duel.Life[activeSide] = activeLifePoints;
        duel.hands[activeSide].AddRange(activeHand);
        for (int slot = 0; slot < 5; slot++)
        {
            duel.Monsters[0, slot] = side0Monsters[slot];
            duel.Monsters[1, slot] = side1Monsters[slot];
        }
        return duel;
    }

    public DemoDuel(IEnumerable<Card> cards)
    {
        var all = cards.Where(c => c.Name.Length > 0).ToList();
        var monsters = all.Where(c => c.IsMonster).Take(12).ToList();
        var warrior = all.FirstOrDefault(c => c.ClassCode == 15);
        if (warrior != null && !monsters.Take(3).Contains(warrior)) monsters.Insert(0, warrior);
        // Put both featured FLIP monsters in the first hand of the local demo.
        foreach (ushort id in new ushort[] { FlipEffectRules.MorphingJar, FlipEffectRules.ManEaterBug })
        {
            var flip = all.FirstOrDefault(c => c.Id == id);
            if (flip is null) continue;
            monsters.Remove(flip);
            monsters.Insert(0, flip);
        }
        var spells = all.Where(c => EquipmentRules.Supports(c.Id) ||
            LifePointSpellRules.Supports(c.Id) || FieldSpellRules.Supports(c.Id))
            .OrderBy(c => c.Id == 300 ? 1 : 0).ToList();
        // Fixed reproducible fixture; both players have an opening hand and a small deck.
        for (int p = 0; p < 2; p++)
        {
            hands[p].AddRange(monsters.Take(3)); hands[p].AddRange(spells);
            foreach (var c in monsters.Skip(3).Concat(spells)) decks[p].Enqueue(c);
        }
    }
    public static int NormalTributes(Card card) => card.Level <= 4 ? 0 : card.Level <= 6 ? 1 : 2;
    public bool CanSummonFromHand(int index) => !ActionsBlocked && Phase == DemoPhase.Main && !summoned &&
        index >= 0 && index < Hand.Count && Hand[index].IsMonster &&
        !NormalSummonRules.RequiresSpecialSummon(Hand[index]) &&
        Enumerable.Range(0, 5).Count(z => Monsters[ActivePlayer, z] != null) >= NormalTributes(Hand[index]) &&
        (NormalTributes(Hand[index]) > 0 || FirstFreeMonsterZone() >= 0);
    public int FirstFreeMonsterZone(IReadOnlyList<int>? tributes = null) => Enumerable.Range(0, 5)
        .FirstOrDefault(z => Monsters[ActivePlayer, z] == null || tributes?.Contains(z) == true, -1);
    public int FirstFreeSpellZone() => Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
    public bool CanSetFromHand(int index) => index >= 0 && index < Hand.Count && (Hand[index].IsMonster
        ? CanSummonFromHand(index)
        : !ActionsBlocked && Phase == DemoPhase.Main && Hand[index].ClassCode is 21 or 22 or 23 &&
          (FieldSpellRules.Supports(Hand[index].Id) ? FieldSpells[ActivePlayer] is null : FirstFreeSpellZone() >= 0));
    public bool CanActivateFromHand(int index) => !ActionsBlocked && Phase == DemoPhase.Main &&
        index >= 0 && index < Hand.Count &&
        (FieldSpellRules.Supports(Hand[index].Id) ||
        (FirstFreeSpellZone() >= 0 && (EquipmentRules.Supports(Hand[index].Id) ||
            Hand[index].Id == 0x593 || LifePointSpellRules.Supports(Hand[index].Id))));
    // Leftmost placement follows the operator's observed UI. No native animation timing is implied.
    public bool SummonLeftmost(int index, bool faceDown, out string message, IReadOnlyList<int>? tributes = null) =>
        Summon(index, ActivePlayer, FirstFreeMonsterZone(tributes), true, out message, tributes, faceDown);
    public bool SetFromHand(int index, out string message, IReadOnlyList<int>? tributes = null)
    {
        message = "Set unavailable: choose a card in main phase with a free matching zone.";
        if (!CanSetFromHand(index)) return false;
        if (Hand[index].IsMonster) return SummonLeftmost(index, true, out message, tributes);
        if (FieldSpellRules.Supports(Hand[index].Id))
        {
            // Setting a new field spell does not replace the opponent's active one.
            FieldSpells[ActivePlayer] = Hand[index];
            FieldSpellFaceDown[ActivePlayer] = true;
            hands[ActivePlayer].RemoveAt(index);
            message = "Field Spell set face-down in its dedicated zone.";
            return true;
        }
        int zone = FirstFreeSpellZone();
        Spells[ActivePlayer, zone] = Hand[index]; SpellFaceDown[ActivePlayer, zone] = true;
        hands[ActivePlayer].RemoveAt(index);
        message = "Card set face-down in the leftmost free spell/trap zone.";
        return true;
    }
    public bool Summon(int index, int player, int zone, bool monsterZone, out string message, IReadOnlyList<int>? tributeZones = null, bool faceDown = false)
    {
        message = "Choose an empty monster zone on the active player's side.";
        if (ActionsBlocked || Phase != DemoPhase.Main || summoned) { message = "Summon unavailable: main phase, once per turn."; return false; }
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
        DefensePosition[player, zone] = faceDown;
        MonsterFaceDown[player, zone] = faceDown;
        // Native placement 0x00568610 sets the same turn lock read by 0x0056B5A0.
        changedPosition[player, zone] = true;
        message = $"Monster {(faceDown ? "set in face-down defense" : "summoned in attack position")}; {required} tribute(s)."; return true;
    }
    public bool Draw(out string message)
    {
        message = "Draw available once per turn in main phase.";
        if (ActionsBlocked || Phase != DemoPhase.Main || drawn) return false;
        if (decks[ActivePlayer].Count == 0) { Winner = 1 - ActivePlayer; message = "Empty deck: active player loses (demo policy)."; return true; }
        hands[ActivePlayer].Add(decks[ActivePlayer].Dequeue()); drawn = true;
        message = "Drew one card."; return true;
    }
    public bool Play(int index, out string message)
    {
        message = "Choose a supported LP, Field or continuous spell; equipment needs a compatible face-up monster.";
        if (ActionsBlocked || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count) return false;
        if (FieldSpellRules.Supports(Hand[index].Id))
        {
            Card field = Hand[index];
            // A face-up Field Spell on either side is replaced; a set card on
            // the opponent's side remains face-down until they activate it.
            ReplaceActiveFields();
            if (FieldSpells[ActivePlayer] is not null) SendFieldSpellToGraveyard(ActivePlayer);
            FieldSpells[ActivePlayer] = field;
            FieldSpellFaceDown[ActivePlayer] = false;
            hands[ActivePlayer].RemoveAt(index);
            message = $"{field.Name}: {FieldSpellRules.EffectSummary(field.Id)}";
            return true;
        }
        if (FirstFreeSpellZone() < 0) { message = "Activation needs a free spell/trap zone."; return false; }
        if (Hand[index].Id == 0x593)
        {
            int zone = Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
            if (zone < 0) { message = "Infinite Cards needs a free spell zone."; return false; }
            Spells[ActivePlayer, zone] = Hand[index]; SpellFaceDown[ActivePlayer, zone] = false; hands[ActivePlayer].RemoveAt(index);
            message = "Infinite Cards active: neither player has an end-turn hand limit.";
            return true;
        }
        // Demo policy: immediate LP effect and graveyard movement, without chains.
        // Resolve before spending the card, so unsupported spells are atomic.
        var card = Hand[index];
        var change = LifePointSpellRules.ResolveDemo(card.Id, ActivePlayer);
        if (change is null) return false;
        hands[ActivePlayer].RemoveAt(index); graves[ActivePlayer].Add(card);
        ApplyLifePointSpell(change.Value);
        message = $"{card.Name}: LP changed (self {change.Value.CasterDelta}, opponent {change.Value.OpponentDelta}).";
        return true;
    }
    public bool ActivateSetSpell(int zone, out string message)
    {
        message = "This set card's activation is not implemented in the demo.";
        int p = ActivePlayer;
        if (ActionsBlocked || Phase != DemoPhase.Main || zone < 0 || zone >= 5 ||
            !SpellFaceDown[p, zone] || Spells[p, zone] is not Card card) return false;
        if (card.Id == 0x593)
        {
            SpellFaceDown[p, zone] = false;
            message = "Infinite Cards activated."; return true;
        }
        // Same bounded plan as hand activation; native suppression is not modeled.
        var change = LifePointSpellRules.ResolveDemo(card.Id, p);
        if (change is null) return false;
        Spells[p, zone] = null; SpellFaceDown[p, zone] = false; graves[p].Add(card);
        ApplyLifePointSpell(change.Value);
        message = $"{card.Name}: LP changed (self {change.Value.CasterDelta}, opponent {change.Value.OpponentDelta}).";
        return true;
    }
    public bool ActivateSetFieldSpell(out string message)
    {
        message = "Only a set Field Spell in your own zone can activate during Main Phase.";
        int owner = ActivePlayer;
        if (ActionsBlocked || Phase != DemoPhase.Main ||
            FieldSpells[owner] is not Card field || !FieldSpellFaceDown[owner] ||
            !FieldSpellRules.Supports(field.Id)) return false;
        ReplaceActiveFields();
        FieldSpellFaceDown[owner] = false;
        message = $"{field.Name}: {FieldSpellRules.EffectSummary(field.Id)}";
        return true;
    }

    private void ReplaceActiveFields()
    {
        // One active Field Spell globally in this 2004-style demo.
        for (int p = 0; p < 2; p++)
            if (FieldSpells[p] is not null && !FieldSpellFaceDown[p])
                SendFieldSpellToGraveyard(p);
    }

    private void SendFieldSpellToGraveyard(int owner)
    {
        if (FieldSpells[owner] is not Card field) return;
        graves[owner].Add(field);
        FieldSpells[owner] = null;
        FieldSpellFaceDown[owner] = false;
    }

    /// <summary>Sandbox removal hook; not a claim about native destruction timing.</summary>
    public bool RemoveFieldSpell(int owner)
    {
        if (ActionsBlocked || owner is < 0 or > 1 || FieldSpells[owner] is null) return false;
        SendFieldSpellToGraveyard(owner);
        return true;
    }

    private void ApplyLifePointSpell(LifePointSpellChange change)
    {
        ChangeLife(ActivePlayer, change.CasterDelta);
        ChangeLife(1 - ActivePlayer, change.OpponentDelta);
    }
    private void ChangeLife(int player, int delta)
    {
        // Demo policy: LP cannot be negative; safe int ceiling for repeated healing.
        Life[player] = (int)Math.Clamp((long)Life[player] + delta, 0L, int.MaxValue);
        if (Life[player] == 0 && delta < 0) Winner = 1 - player;
    }
    public int AttackValue(int p, int z) => EquipmentStats(p, z).Attack;
    public int DefenseValue(int p, int z) => EquipmentStats(p, z).Defense;
    private NativeAiStats EquipmentStats(int p, int z)
    {
        if (Monsters[p, z] is not Card monster) return default;
        // Demo policy: no intrinsic/type-changing or suppression effects. Field
        // contributions are added on demand to equipment before the final clamp.
        var stats = new NativeAiStats((ushort)monster.Index, (byte)monster.ClassCode,
            (byte)(monster.Properties >> 29), monster.Attack, monster.Defense);
        if (MonsterFaceDown[p, z]) return stats; // Native calculation returns before equipment deltas.
        var sum = new NativeAiAttributeDelta();
        foreach (var e in equipment.Where(e => e.TargetPlayer == p && e.TargetZone == z))
        {
            // A sandbox identity check, not a reconstruction of the native selection key.
            if (!ReferenceEquals(e.TargetCard, monster) || !ReferenceEquals(Spells[e.Owner, e.SourceZone], e.Card) ||
                SpellFaceDown[e.Owner, e.SourceZone]) continue;
            var delta = NativeAiAttributes.EquipmentDelta(e.Card.Id, stats, monster.Id);
            if (!delta.IsSupported) throw new InvalidOperationException("Unsupported equipment in demo link list");
            sum = new(unchecked(sum.Attack + delta.Value.Attack), unchecked(sum.Defense + delta.Value.Defense));
        }
        // Field Spell is a separate global modifier, not an equipment link.
        // Do not cache: activation, replacement or removal changes stats immediately.
        if (ActiveFieldSpell is Card field)
        {
            var bonus = FieldSpellRules.Bonus(field.Id, monster.ClassCode, (byte)(monster.Properties >> 29), DefensePosition[p, z]);
            sum = new(sum.Attack + bonus.Attack, sum.Defense + bonus.Defense);
        }
        // Accumulate all contributions and clamp once. Other native stages are absent.
        return NativeAiAttributes.ApplyResolvedTail(stats, sum, 0, 0);
    }
    private EquipmentTargetContext DemoEquipmentTarget(int player, int zone)
    {
        if (player is < 0 or > 1 || zone is < 0 or >= 5)
            return new(Player: player, Zone: zone);
        var card = Monsters[player, zone];
        return new(player, zone, card?.Index ?? 0, card?.Id ?? 0, !MonsterFaceDown[player, zone],
            equipmentContextResolved ? (byte)(card?.ClassCode ?? 0) : null,
            equipmentContextResolved ? (byte)((card?.Properties ?? 0) >> 29) : null,
            equipmentContextResolved ? true : null);
    }
    public NativeAiResult<bool> EquipmentEligibility(int index, int targetPlayer, int targetZone)
    {
        if (ActionsBlocked || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count ||
            targetPlayer is < 0 or > 1 || targetZone is < 0 or >= 5 || FirstFreeSpellZone() < 0)
            return NativeAiResult<bool>.Supported(false);
        return EquipmentRules.Eligibility(Hand[index].Id, DemoEquipmentTarget(targetPlayer, targetZone));
    }
    public bool Equip(int index, int targetPlayer, int targetZone, out string message)
    {
        message = "Equipment needs a compatible face-up monster and a free own spell zone.";
        // Re-read the live target and hand at resolution, after UI selection. No mutation before validation.
        var eligibility = EquipmentEligibility(index, targetPlayer, targetZone);
        if (!eligibility.IsSupported) { message = string.Join("; ", eligibility.UnsupportedReasons); return false; }
        if (!eligibility.Value) return false;
        int sourceZone = FirstFreeSpellZone();
        var card = Hand[index];
        equipment.Add(new(card, ActivePlayer, sourceZone, targetPlayer, targetZone, Monsters[targetPlayer, targetZone]!));
        Spells[ActivePlayer, sourceZone] = card; SpellFaceDown[ActivePlayer, sourceZone] = false;
        hands[ActivePlayer].RemoveAt(index);
        message = $"{card.Name} equipped."; return true;
    }
    /// <summary>Explicit sandbox removal, not an activation, native movement rule or trigger.</summary>
    public bool RemoveEquipment(int owner, int sourceZone)
    {
        if (ActionsBlocked || owner is < 0 or > 1 || sourceZone is < 0 or >= 5) return false;
        var e = equipment.SingleOrDefault(e => e.Owner == owner && e.SourceZone == sourceZone);
        if (e is null) return false;
        equipment.Remove(e);
        // Preserve an unrelated occupant if a caller directly replaced a public fixture slot.
        if (ReferenceEquals(Spells[owner, sourceZone], e.Card))
        {
            Spells[owner, sourceZone] = null; SpellFaceDown[owner, sourceZone] = false;
        }
        graves[owner].Add(e.Card);
        return true;
    }
    public bool ChangePosition(int zone, out string message)
    {
        message = "Change position in main phase, once per turn, after the monster's summon turn.";
        int owner = ActivePlayer;
        if (ActionsBlocked || Phase != DemoPhase.Main || zone is < 0 or >= 5 ||
            Monsters[owner, zone] is not Card monster || changedPosition[owner, zone]) return false;
        bool flipped = MonsterFaceDown[owner, zone];
        DefensePosition[owner, zone] = !DefensePosition[owner, zone];
        MonsterFaceDown[owner, zone] = false;
        changedPosition[owner, zone] = true;
        if (flipped)
        {
            TriggerFlip(owner, zone, monster, out message);
            if (message.Length == 0) message = "Flip Summon: face-up attack position.";
        }
        else message = DefensePosition[owner, zone] ?
            "Monster in face-up defense (demo)." : "Monster in attack position (demo).";
        return true;
    }
    public bool BeginBattle(out string message)
    {
        message = "Battle unavailable on the opening turn or after game over.";
        if (ActionsBlocked || Turn == 1 || Phase != DemoPhase.Main) return false;
        Phase = DemoPhase.Battle; message = "Select your monster, then an enemy monster or Direct attack."; return true;
    }
    public bool Attack(int source, int target, out string message)
    {
        message = "Select an unused attacker and a valid enemy target in battle phase.";
        int p = ActivePlayer, enemy = 1 - p;
        if (ActionsBlocked || Phase != DemoPhase.Battle || source is < 0 or >= 5 ||
            Monsters[p, source] is not Card attacker || attacked[p, source] ||
            DefensePosition[p, source]) return false;
        if (target == -1)
        {
            if (Enumerable.Range(0, 5).Any(z => Monsters[enemy, z] is not null))
            { message = "Enemy monsters block direct attacks."; return false; }
            attacked[p, source] = true;
            int damage = AttackValue(p, source);
            Damage(enemy, damage);
            message = $"Direct attack: {damage} damage.";
            return true;
        }
        if (target is < 0 or >= 5 || Monsters[enemy, target] is not Card defender) return false;
        attacked[p, source] = true;
        bool wasFaceDown = MonsterFaceDown[enemy, target];
        MonsterFaceDown[enemy, target] = false;
        if (wasFaceDown)
        {
            TriggerFlip(enemy, target, defender, out string flipMessage);
            if (PendingFlipTarget is not null)
            {
                deferredBattle = new(p, source, attacker, enemy, target, defender);
                message = flipMessage;
                return true;
            }
            if (GameOver)
            {
                message = flipMessage;
                return true;
            }
            ResolveBattleOutcome(p, source, enemy, target, out string battleMessage);
            message = flipMessage.Length > 0 ? flipMessage + " " + battleMessage : battleMessage;
            return true;
        }
        return ResolveBattleOutcome(p, source, enemy, target, out message);
    }

    private bool ResolveBattleOutcome(int attackerSide, int source, int defenderSide, int target,
                                      out string message)
    {
        message = "Battle stopped: a combatant left the field.";
        if (GameOver || Monsters[attackerSide, source] is not Card ||
            Monsters[defenderSide, target] is not Card) return false;
        if (DefensePosition[defenderSide, target])
        {
            int difference = AttackValue(attackerSide, source) - DefenseValue(defenderSide, target);
            if (difference > 0) RemoveMonster(defenderSide, target);
            else if (difference < 0) Damage(attackerSide, -difference);
            message = difference > 0 ? "Defense overcome: defender destroyed, no LP damage." :
                difference == 0 ? "Equal ATK/DEF: no destruction or damage." :
                $"Defense holds: attacker takes {-difference} damage, neither destroyed.";
            return true;
        }
        int delta = AttackValue(attackerSide, source) - AttackValue(defenderSide, target);
        // Equal ATK above zero destroys both; zero/zero tie leaves both.
        if (delta == 0 && AttackValue(attackerSide, source) == 0)
        {
            message = "Zero ATK tie: neither monster destroyed, no damage.";
            return true;
        }
        if (delta >= 0) RemoveMonster(defenderSide, target);
        if (delta <= 0) RemoveMonster(attackerSide, source);
        if (delta > 0) Damage(defenderSide, delta);
        if (delta < 0) Damage(attackerSide, -delta);
        message = delta == 0 ? "Equal ATK: both monsters destroyed, no damage." :
            $"Battle: {Math.Abs(delta)} damage; weaker monster destroyed.";
        return true;
    }

    private void TriggerFlip(int owner, int zone, Card card, out string message)
    {
        message = "";
        switch (FlipEffectRules.Effect(card.Id))
        {
            case DemoFlipEffect.DestroyMonster:
                PendingFlipTarget = new(owner, zone, card);
                message = $"{card.Name} FLIP (P{owner + 1}): select a monster on either side to destroy.";
                break;
            case DemoFlipEffect.ReplaceHands:
                for (int p = 0; p < 2; p++)
                {
                    graves[p].AddRange(hands[p]);
                    hands[p].Clear();
                }
                bool[] deckOut = [decks[0].Count < 5, decks[1].Count < 5];
                for (int p = 0; p < 2; p++)
                    for (int i = 0; i < 5 && decks[p].Count > 0; i++)
                        hands[p].Add(decks[p].Dequeue());
                if (deckOut[0] && deckOut[1]) IsDraw = true;
                else if (deckOut[0]) Winner = 1;
                else if (deckOut[1]) Winner = 0;
                message = deckOut[0] || deckOut[1]
                    ? $"{card.Name} FLIP: both discard and draw; insufficient deck cards end the demo duel."
                    : $"{card.Name} FLIP: both discard their hands and draw 5.";
                break;
        }
    }

    /// <summary>
    /// Resolve target selection and resume an interrupted battle only if
    /// the same attacker and defender still occupy their original zones.
    /// </summary>
    public bool ResolveFlipTarget(int targetPlayer, int targetZone, out string message)
    {
        message = "Select an occupied monster on either side to resolve the FLIP effect.";
        if (PendingFlipTarget is not DemoFlipTarget pending ||
            targetPlayer is < 0 or > 1 || targetZone is < 0 or >= 5 ||
            Monsters[targetPlayer, targetZone] is not Card target) return false;
        PendingFlipTarget = null;
        BattleContinuation? resume = deferredBattle;
        deferredBattle = null;
        RemoveMonster(targetPlayer, targetZone);
        message = $"{pending.SourceCard.Name} FLIP: destroyed {target.Name}.";
        if (resume is null) return true;
        if (!ReferenceEquals(Monsters[resume.AttackerSide, resume.AttackerZone], resume.Attacker) ||
            !ReferenceEquals(Monsters[resume.DefenderSide, resume.DefenderZone], resume.Defender))
        {
            message += " Battle stopped because a combatant left the field.";
            return true;
        }
        ResolveBattleOutcome(resume.AttackerSide, resume.AttackerZone,
                             resume.DefenderSide, resume.DefenderZone, out string battleMessage);
        message += " " + battleMessage;
        return true;
    }

    private void RemoveMonster(int p, int z)
    {
        graves[p].Add(Monsters[p, z]!); Monsters[p, z] = null; DefensePosition[p, z] = false; MonsterFaceDown[p, z] = false;
        // Common cleanup only: no Banisher/token/trigger exceptions in this sandbox.
        foreach (var e in equipment.Where(e => e.TargetPlayer == p && e.TargetZone == z).ToArray())
        RemoveEquipment(e.Owner, e.SourceZone);
    }
    private void Damage(int p, int value) => ChangeLife(p, -value);
    public void NextTurn() => EndTurn(out _);
    public bool EndTurn(out string message)
    {
        message = "The duel has ended.";
        if (ActionsBlocked) return false;
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
        if (ActionsBlocked || PendingEndDiscards == 0 || index < 0 || index >= Hand.Count) return false;
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
