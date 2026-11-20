using System.Text.Json;
using Joey.Core;

/// <summary>
/// Native-observed P168 effect transitions from mass96 (not whole-duel replay).
/// Each fixture includes the exact native command seq and state deltas; replay
/// here begins from a deliberately bounded C# scenario with two eligible
/// same-resource copies. Compare only effect LP delta, two summons and zone
/// posture, not opponent action choice or native deck shuffle order.
/// </summary>
internal static class NativeP168EvidenceChecks
{
    private static Card Monster(ushort resourceId, int index, int atk = 1000, int def = 100) =>
        new(index, resourceId, $"Native {resourceId}", "", (3u << 20) |
            (2u << 25) | ((uint)(atk / 10) << 9) | (uint)(def / 10));

    private static Card?[] Slots(Card? a = null) => [a, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "p168-mass96-native-episodes.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        check(root.GetProperty("schema").GetString() ==
              "joey-p168-native-effect-transitions-v1" &&
              root.GetProperty("sourceSha256").GetString()!.Length == 64 &&
              root.GetProperty("episodes").GetArrayLength() == 4,
              "Native P168: four trace-localized episodes with a pinned ZIP hash");

        foreach (var episode in root.GetProperty("episodes").EnumerateArray())
        {
            int cohort = episode.GetProperty("cohort").GetInt32();
            int seq = episode.GetProperty("effectSeq").GetInt32();
            int owner = episode.GetProperty("sourceOwner").GetInt32();
            int affected = episode.GetProperty("affectedPlayer").GetInt32();
            ushort id = checked((ushort)episode.GetProperty("resourceId").GetInt32());
            int index = episode.GetProperty("internalIndex").GetInt32();
            int before = episode.GetProperty("lifeBefore").GetInt32();
            int after = episode.GetProperty("lifeAfter").GetInt32();
            int delta = episode.GetProperty("lifeDelta").GetInt32();
            int deckBefore = episode.GetProperty("deckBefore").GetInt32();
            int deckAfter = episode.GetProperty("deckAfter").GetInt32();
            var nativeSummons = episode.GetProperty("summons").EnumerateArray().ToArray();
            bool momonga = id == CardSelectionRules.NimbleMomonga;
            int expectedPositionWord = momonga ? 513 : 514;
            string tag = $"Native P168 c{cohort:00} seq {seq} {episode.GetProperty("name").GetString()}";

            check((id == 1114 && !momonga || id == 1115 && momonga) &&
                  owner is 0 or 1 && affected == (momonga ? owner : 1 - owner) &&
                  delta == after - before &&
                  delta == (momonga ? 1000 : -500) &&
                  deckBefore - deckAfter == 2 &&
                  nativeSummons.Length == 2 &&
                  episode.GetProperty("sourceLeftFieldSeq").GetInt32() < seq &&
                  episode.GetProperty("lifeDeltaSeq").GetInt32() > seq &&
                  nativeSummons.All(x =>
                      x.GetProperty("seq").GetInt32() >
                      episode.GetProperty("lifeDeltaSeq").GetInt32() &&
                      x.GetProperty("cardIndex").GetInt32() == index &&
                      x.GetProperty("positionWord").GetInt32() == expectedPositionWord),
                  tag + ": native seq validates LP delta, two deck movements and raw posture words");

            int attackerSide = 1 - owner;
            Card original = Monster(id, index);
            Card sameName1 = Monster(id, index);
            Card sameName2 = Monster(id, index);
            Card attacker = Monster(900, 901, atk: 2200, def: 1800);
            var deck = new[] { sameName1, sameName2 };
            var duel = DemoDuel.FromPartialObservation(
                attackerSide, [],
                Slots(owner == 0 ? original : attacker),
                Slots(owner == 1 ? original : attacker),
                activeLifePoints: 8000,
                side0Deck: owner == 0 ? deck : [],
                side1Deck: owner == 1 ? deck : [],
                effectShuffleSeed: seq);
            duel.Life[affected] = before;
            duel.DefensePosition[owner, 0] = true;
            if (momonga) duel.MonsterFaceDown[owner, 0] = true;
            // FromPartialObservation(activeSide:0) starts at turn 1, where no
            // Battle Phase is allowed. Advance two turns without drawing.
            if (attackerSide == 0) { duel.NextTurn(); duel.NextTurn(); }

            check(duel.BeginBattle(out _) && duel.Attack(0, 0, out _) &&
                  duel.Life[affected] == after &&
                  duel.PendingCardSelection is {
                      Source: CardSelectionSource.Deck,
                      Destination: CardSelectionDestination.Field,
                      Filter: CardSelectionFilter.SameNameMonster,
                      MinSelections: 0, MaxSelections: 2
                  } &&
                  duel.PendingCardSelection.Owner == owner &&
                  duel.PendingCardSelection.RequiredResourceId == id &&
                  duel.PendingCardSelection.SummonPosition ==
                      (momonga ? CardSelectionPosition.FaceDownDefense
                               : CardSelectionPosition.FaceUpAttack),
                  tag + ": bounded C# battle effect matches observed LP delta");

            check(duel.SelectBatchCard(0, out _) &&
                  duel.SelectBatchCard(1, out _) &&
                  duel.PendingCardSelection is null &&
                  duel.DeckCount(owner) == 0 &&
                  Enumerable.Range(0, 5).Count(z => duel.Monsters[owner, z]?.Id == id) == 2 &&
                  Enumerable.Range(0, 5).Where(z => duel.Monsters[owner, z]?.Id == id)
                      .All(z => duel.MonsterFaceDown[owner, z] == momonga &&
                                duel.DefensePosition[owner, z] == momonga),
                  tag + ": bounded C# double recruitment and posture match native hypothesis");
        }
    }
}
