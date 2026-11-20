using Joey.Core;

internal static class RitualSearcherChecks
{
    private static Card Monster(ushort id, int index, int atk = 800, int def = 900,
        int level = 4) =>
        new(index, id, $"Monster {id}", "", (3u << 20) | ((uint)level << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));

    private static Card RitualMonster(ushort id, int index, int cls = 18) =>
        new(index, id, $"Ritual Monster {id}", "", ((uint)cls << 20) | (1u << 25));

    private static Card RitualSpell(ushort id, int index, int cls = 22) =>
        new(index, id, $"Ritual Spell {id}", "", (uint)cls << 20);

    private static Card?[] Slots(Card? a = null) => [a, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var sonic = Monster(1122, 497, 1400, 1000);
        var senju = Monster(1109, 485, 1400, 1000);
        var blackIllusion = RitualSpell(958, 200);
        var blackLuster = RitualSpell(669, 201);
        var genericSpell = RitualSpell(342, 202);
        var fakeRitualSpell = RitualSpell(958, 203, cls: 21);
        var relinquished = RitualMonster(730, 204);
        var ritualCopy = RitualMonster(730, 205);
        var blackLusterSoldier = RitualMonster(363, 206, cls: 15);
        var genericMonster = Monster(909, 207, 1000, 900);
        var fakeRitualMonster = RitualMonster(730, 208, cls: 22);
        var attackMonster = Monster(904, 209, 2000, 1500);

        check(RitualSearchRules.SonicBird == 1122 &&
              RitualSearchRules.SenjuOfTheThousandHands == 1109 &&
              RitualSearchRules.OnNormalOrFlipSummon(1122) == CardSelectionFilter.RitualSpell &&
              RitualSearchRules.OnNormalOrFlipSummon(1109) == CardSelectionFilter.RitualMonster &&
              RitualSearchRules.OnNormalOrFlipSummon(497) is null &&
              RitualSearchRules.OnNormalOrFlipSummon(485) is null &&
              RitualSearchRules.OnNormalOrFlipSummon(601) is null &&
              CardSelectionRules.IsEligible(blackIllusion, CardSelectionFilter.RitualSpell) &&
              CardSelectionRules.IsEligible(blackLuster, CardSelectionFilter.RitualSpell) &&
              !CardSelectionRules.IsEligible(genericSpell, CardSelectionFilter.RitualSpell) &&
              !CardSelectionRules.IsEligible(fakeRitualSpell, CardSelectionFilter.RitualSpell) &&
              CardSelectionRules.IsEligible(relinquished, CardSelectionFilter.RitualMonster) &&
              CardSelectionRules.IsEligible(blackLusterSoldier, CardSelectionFilter.RitualMonster) &&
              !CardSelectionRules.IsEligible(genericMonster, CardSelectionFilter.RitualMonster) &&
              !CardSelectionRules.IsEligible(fakeRitualMonster, CardSelectionFilter.RitualMonster),
              "P165: original resource IDs and closed ritual spell/monster eligibility sets");

        var normal = DemoDuel.FromPartialObservation(0, [sonic], Slots(), Slots(), 8000,
            side0Deck: [genericSpell, blackIllusion, relinquished, blackLuster, fakeRitualSpell],
            effectShuffleSeed: 42);
        check(normal.SummonLeftmost(0, false, out _) &&
              normal.Monsters[0, 0] == sonic &&
              normal.PendingCardSelection is {
                  Owner: 0, EffectResourceId: 1122,
                  Source: CardSelectionSource.Deck,
                  Destination: CardSelectionDestination.Hand,
                  Filter: CardSelectionFilter.RitualSpell, MinSelections: 0,
                  MaxSelections: 1 } &&
              normal.PendingCardSelection.EligibleCards
                  .Select(x => x.SourceIndex).SequenceEqual([1, 3]) &&
              normal.HandFor(0).Count == 0 && normal.DeckCount(0) == 5 &&
              !normal.EndTurn(out _) && !normal.Draw(out _) &&
              !normal.CanSummonFromHand(0),
              "Sonic Bird: Normal Summon opens optional ritual SPELL search and blocks other actions");
        var unchanged = normal.DeckSnapshot(0).ToArray();
        check(!normal.ResolveCardSelection(0, out _) &&
              !normal.ResolveCardSelection(2, out _) &&
              !normal.ResolveCardSelection(4, out _) &&
              !normal.ResolveCardSelection(-1, out _) &&
              normal.DeckSnapshot(0).SequenceEqual(unchanged) &&
              normal.PendingCardSelection is not null,
              "Sonic Bird: non-Ritual Spell, Ritual Monster and class-mismatch rejected atomically");
        check(normal.ResolveCardSelection(3, out _) &&
              normal.PendingCardSelection is null &&
              normal.HandFor(0).Single() == blackLuster &&
              normal.DeckCount(0) == 4 && !normal.DeckSnapshot(0).Contains(blackLuster) &&
              !normal.ResolveCardSelection(1, out _) &&
              !normal.SummonLeftmost(0, false, out _),
              "Sonic Bird: chosen spell enters owner hand, remaining deck shuffles; summon spent");

        var skip = DemoDuel.FromPartialObservation(0, [sonic], Slots(), Slots(), 8000,
            side0Deck: [blackIllusion, blackLuster, genericMonster]);
        check(skip.SetFromHand(0, out _) &&
              skip.PendingCardSelection is null && skip.HandFor(0).Count == 0 &&
              skip.MonsterFaceDown[0, 0],
              "Sonic Bird: setting face-down does not search Ritual Spell");
        skip.NextTurn(); skip.NextTurn();
        check(skip.ChangePosition(0, out _) &&
              skip.PendingCardSelection is { Owner: 0, EffectResourceId: 1122,
                  Filter: CardSelectionFilter.RitualSpell, MinSelections: 0 } &&
              skip.PendingCardSelection.EligibleCards.Count == 2 &&
              !skip.ChangePosition(0, out _) &&
              skip.SkipOptionalCardSelection(out _) &&
              skip.PendingCardSelection is null &&
              skip.DeckSnapshot(0).SequenceEqual([blackIllusion, blackLuster, genericMonster]) &&
              skip.Monsters[0, 0] == sonic && skip.HandFor(0).Count == 0 &&
              !skip.SkipOptionalCardSelection(out _),
              "Sonic Bird: Flip Summon activates optional search; Skip keeps deck order and field");

        var senjuSummon = DemoDuel.FromPartialObservation(1, [senju], Slots(), Slots(), 8000,
            side1Deck: [genericMonster, blackIllusion, relinquished, blackLusterSoldier,
                        fakeRitualMonster, ritualCopy], effectShuffleSeed: 24);
        check(senjuSummon.SummonLeftmost(0, false, out _) &&
              senjuSummon.PendingCardSelection is {
                  Owner: 1, EffectResourceId: 1109,
                  Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Hand,
                  Filter: CardSelectionFilter.RitualMonster, MinSelections: 0, MaxSelections: 1 } &&
              senjuSummon.PendingCardSelection.EligibleCards
                  .Select(c => c.SourceIndex).SequenceEqual([2, 3, 5]) &&
              !senjuSummon.ResolveCardSelection(1, out _) &&
              !senjuSummon.ResolveCardSelection(4, out _) &&
              senjuSummon.ResolveCardSelection(3, out _) &&
              senjuSummon.HandFor(1).Single() == blackLusterSoldier &&
              senjuSummon.DeckCount(1) == 5 &&
              senjuSummon.HandFor(0).Count == 0 &&
              senjuSummon.PendingCardSelection is null,
              "Senju: Normal Summon searches Ritual MONSTER, not its Ritual Spell; side 1 owns result");

        var noMatch = DemoDuel.FromPartialObservation(0, [senju], Slots(), Slots(), 8000,
            side0Deck: [blackIllusion, genericMonster, fakeRitualMonster]);
        check(noMatch.SummonLeftmost(0, false, out _) &&
              noMatch.PendingCardSelection is null &&
              noMatch.DeckSnapshot(0).SequenceEqual([blackIllusion, genericMonster, fakeRitualMonster]) &&
              !noMatch.SkipOptionalCardSelection(out _) &&
              noMatch.EndTurn(out _),
              "Senju: no ritual monster means no empty modal or shuffle");

        var effectReveal = DemoDuel.FromPartialObservation(0, [], Slots(senju), Slots(), 8000,
            side0Deck: [relinquished]);
        effectReveal.MonsterFaceDown[0, 0] = true;
        check(effectReveal.RevealMonsterByEffect(0, 0, out _) &&
              effectReveal.PendingCardSelection is null &&
              effectReveal.HandFor(0).Count == 0 &&
              effectReveal.DeckSnapshot(0).Single() == relinquished,
              "Senju: external face-up reveal is not a Flip Summon and must NOT search");

        var attacked = DemoDuel.FromPartialObservation(1, [], Slots(sonic),
            Slots(attackMonster), 8000, side0Deck: [blackIllusion]);
        attacked.MonsterFaceDown[0, 0] = attacked.DefensePosition[0, 0] = true;
        check(attacked.BeginBattle(out _) && attacked.Attack(0, 0, out _) &&
              attacked.Monsters[0, 0] is null && attacked.Graveyard(0).Single() == sonic &&
              attacked.PendingCardSelection is null &&
              attacked.DeckSnapshot(0).Single() == blackIllusion,
              "Sonic Bird: revealing during opponent's battle does not trigger Normal/Flip Summon search");

        // Original DAT class and IDs also guard the filter from generic ritual-like
        // card names, and the eligibility test covers the entire packed catalog.
        ushort[] spellIds = [669, 670, 672, 673, 675, 676, 678, 691,
                             693, 695, 696, 698, 699, 720, 958, 1416];
        ushort[] monsterIds = [355, 359, 363, 364, 700, 701, 703, 708,
                               709, 715, 716, 717, 718, 721, 730, 1411];
        check(spellIds.All(id => RitualSearchRules.IsRitualSpell(RitualSpell(id, id))) &&
              monsterIds.All(id => RitualSearchRules.IsRitualMonster(RitualMonster(id, id))) &&
              !RitualSearchRules.IsRitualSpell(genericSpell) &&
              !RitualSearchRules.IsRitualMonster(genericMonster),
              "P165: all 16 native packed Ritual Spell/Monster bindings accepted by the filter");
    }
}
