using System.Text.Json;
using Joey.Core;

/// <summary>
/// Functional sandbox coverage for explicitly classified Normal Monsters.
/// This tests the common summon/set/tribute/battle contract, not all card
/// interactions or parity with Joey's native engine.
/// </summary>
internal static class NormalMonsterPlayabilityChecks
{
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "docs", "cards", "monster-subtypes.json")))
                return dir.FullName;
        throw new FileNotFoundException("Cannot locate YugiRE source inventory.");
    }

    private static ushort[] NormalIds()
    {
        string root = RepositoryRoot();
        using var subtypes = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "cards", "monster-subtypes.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "cards", "playability-manifest.json")));
        var normal = subtypes.RootElement.GetProperty("subtypes").EnumerateObject()
            .Where(property => property.Value.GetString() == "Normal")
            .Select(property => ushort.Parse(property.Name))
            .OrderBy(id => id).ToArray();
        var declared = manifest.RootElement.GetProperty("groups").EnumerateArray()
            .Where(group => group.GetProperty("id").GetString() == "monster.normal")
            .SelectMany(group => group.GetProperty("resourceIds").EnumerateArray())
            .Select(element => element.GetUInt16()).OrderBy(id => id).ToArray();
        if (declared.Length != normal.Length || !declared.SequenceEqual(normal))
            throw new InvalidDataException("Normal monsters in manifest differ from subtype inventory.");
        return normal;
    }

    private static Card SyntheticNormal(ushort id, int index, int level)
    {
        // The original card property word is NOT being fabricated here. This
        // fixture explores generic engine rules across levels/types/attributes.
        int type = 1 + (index % 20);
        int attribute = 1 + (index % 6);
        int atk = 1000 + (index % 18) * 100;
        int def = 500 + (index % 10) * 100;
        uint properties = ((uint)type << 20) | ((uint)level << 25) |
                          ((uint)attribute << 29) |
                          ((uint)(atk / 10) << 9) | (uint)(def / 10);
        return new Card(index, id, $"Normal #{id}", "", properties);
    }

    private static Card?[] Tributes(int number) 
    {
        var slots = new Card?[5];
        for (int i = 0; i < number; i++)
            slots[i] = new Card(3000 + i, 5000, "Tribute", "",
                (1u << 20) | (4u << 25) | (100u << 9) | 100u);
        return slots;
    }

    private static void RunCard(Card card, Action<bool, string> check, string label)
    {
        int required = DemoDuel.NormalTributes(card);
        int[] paid = Enumerable.Range(0, required).ToArray();
        var noTributes = DemoDuel.FromPartialObservation(0, [card], new Card?[5],
            new Card?[5], 8000);
        if (required > 0)
            check(!noTributes.CanSummonFromHand(0) &&
                  !noTributes.Summon(0, 0, 0, true, out _) &&
                  noTributes.Hand.Single() == card && noTributes.Graveyard(0).Count == 0,
                  $"{label}: high-level monster never bypasses tributes");
        var duel = DemoDuel.FromPartialObservation(0, [card], Tributes(required),
            new Card?[5], 8000);
        Card physical = duel.Hand[0];
        int zone = required == 2 ? 1 : 0;
        check(duel.CanSummonFromHand(0) &&
              duel.Summon(0, 0, zone, true, out _, paid) &&
              ReferenceEquals(duel.Monsters[0, zone], physical) && duel.Hand.Count == 0 &&
              duel.Graveyard(0).Count == required &&
              duel.AttackValue(0, zone) == card.Attack &&
              duel.DefenseValue(0, zone) == card.Defense,
              $"{label}: normal summon, tributes, and original base attributes");
        // DemoDuel intentionally disallows Battle Phase on turn 1.
        duel.NextTurn();
        duel.NextTurn();
        check(duel.BeginBattle(out _) && duel.Attack(zone, -1, out _) &&
              duel.Life[1] == 8000 - card.Attack && duel.Monsters[0, zone] == card,
              $"{label}: normal monster can declare a direct attack");
        var set = DemoDuel.FromPartialObservation(0, [card], Tributes(required),
            new Card?[5], 8000);
        check(set.CanSetFromHand(0) && set.SetFromHand(0, out _, paid) &&
              set.Monsters[0, 0] == card && set.DefensePosition[0, 0] &&
              set.MonsterFaceDown[0, 0] && set.Hand.Count == 0 &&
              set.Graveyard(0).Count == required &&
              set.DefenseValue(0, 0) == card.Defense,
              $"{label}: normal monster set face-down with appropriate tribute count");
    }

    private static void RunQueuedCombat(Card card, Action<bool, string> check, string label)
    {
        Card opponent = SyntheticNormal(0, 29, level: 4);
        foreach (int side in new[] { 0, 1 })
        foreach (var (attacker, defender, faceDown) in new (Card, Card?, bool)[]
        {
            (card, opponent, false), (opponent, card, false), (opponent, card, true), (opponent, null, false)
        })
        {
            bool direct = defender is null;
            int defenderSide = 1 - side;
            DemoDuel State()
            {
                Card?[] attackingField = [attacker, direct ? card : null, null, null, null];
                Card?[] defendingField = [defender, null, null, null, null];
                var duel = DemoDuel.FromPartialObservation(side, [],
                    side == 0 ? attackingField : defendingField,
                    side == 1 ? attackingField : defendingField, 8000);
                if (side == 0)
                {
                    duel.NextTurn();
                    duel.NextTurn();
                }
                duel.MonsterFaceDown[defenderSide, 0] = duel.DefensePosition[defenderSide, 0] = faceDown;
                return duel;
            }

            var legacy = State();
            int targetSlot = direct ? -1 : 0;
            check(legacy.BeginBattle(out _) && legacy.Attack(0, targetSlot, out _),
                $"{label}: legacy comparison attack is legal");

            var queued = State();
            var queue = queued.CreatePrimitiveActionQueue();
            check(queued.BeginBattle(out _) && queued.TryQueueAttack(0, targetSlot, out _) &&
                queued.Life.SequenceEqual([8000, 8000]), $"{label}: attack admitted by the queue without damage");
            for (int tick = 0; queue.IsBusy && tick < 40; tick++) queue.Tick();
            check(!queue.IsBusy && queue.Fault is null && queued.Life.SequenceEqual(legacy.Life) &&
                queued.Monsters.Cast<Card?>().SequenceEqual(legacy.Monsters.Cast<Card?>()) &&
                queued.MonsterFaceDown.Cast<bool>().SequenceEqual(legacy.MonsterFaceDown.Cast<bool>()) &&
                queued.Graveyard(0).SequenceEqual(legacy.Graveyard(0)) &&
                queued.Graveyard(1).SequenceEqual(legacy.Graveyard(1)),
                $"{label}: queued combat agrees with legacy LP, reveal and casualties");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        var normalIds = NormalIds();
        check(normalIds.Length == 264 && normalIds.Distinct().Count() == normalIds.Length,
              "Exactly 264 distinct Normal Monster resource IDs are classified and tracked");
        for (int i = 0; i < normalIds.Length; i++)
        {
            int level = 1 + i % 8;
            var normal = SyntheticNormal(normalIds[i], i + 30, level);
            check(normal.IsMonster && NormalSummonRules.Category(normal) == 0 &&
                  !NormalSummonRules.RequiresSpecialSummon(normal),
                  $"Normal #{normal.Id}: simple monster category has no special-only gate");
            RunCard(normal, check, $"Normal #{normal.Id} synthetic level {level}");
            RunQueuedCombat(normal, check, $"Normal #{normal.Id} synthetic level {level}");
        }
    }

    /// <summary>
    /// Optional stronger check against each real record in the user's DAT.
    /// No native process and no proprietary source is committed by this test.
    /// </summary>
    public static void RunWithCatalog(IReadOnlyList<Card> cards, Action<bool, string> check)
    {
        foreach (ushort id in NormalIds())
        {
            Card? real = cards.SingleOrDefault(c => c.Id == id);
            bool match = real is not null && real.IsMonster &&
                         NormalSummonRules.Category(real) == 0 &&
                         real.Level is >= 1 and <= 12;
            check(match, $"Original DAT: Normal #{id} has ordinary category and playable level");
            if (match)
            {
                RunCard(real!, check, $"Original DAT Normal #{id}");
                RunQueuedCombat(real!, check, $"Original DAT Normal #{id}");
            }
        }
    }
}
