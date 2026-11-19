using Joey.Core;

internal static class DeckSearcherChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 1000, int level = 4) =>
        new(index, id, $"Monster {id}", "", (3u << 20) | ((uint)level << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Spell(ushort id, int index) => new(index, id, $"Spell {id}", "", 22u << 20);
    private static Card?[] Slots(Card? first = null, Card? second = null) =>
        [first, second, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var sangan = Monster(47, 248, 1000, 600);
        var witch = Monster(573, 261, 1100, 1200);
        var lowAtk = Monster(100, 1, 1500, 1800);
        var lowDef = Monster(1010, 2, 1800, 1500);
        var weak = Monster(1020, 3, 800, 500);
        var strong = Monster(1030, 4, 1700, 1700);
        var nonMonster = Spell(342, 5);
        var attacker = Monster(1100, 6, 2200, 2000);
        var big = Monster(777, 7, 2300, 1900, level: 7);
        var oneTribute = Monster(778, 8, 2300, 1900, level: 6);

        check(CardSelectionRules.OnFieldToGraveyardFilter(47) == CardSelectionFilter.MonsterAttackAtMost1500 &&
              CardSelectionRules.OnFieldToGraveyardFilter(573) == CardSelectionFilter.MonsterDefenseAtMost1500 &&
              CardSelectionRules.OnFieldToGraveyardFilter(590) is null &&
              CardSelectionRules.IsEligible(lowAtk, CardSelectionFilter.MonsterAttackAtMost1500) &&
              !CardSelectionRules.IsEligible(lowAtk, CardSelectionFilter.MonsterDefenseAtMost1500) &&
              CardSelectionRules.IsEligible(lowDef, CardSelectionFilter.MonsterDefenseAtMost1500) &&
              !CardSelectionRules.IsEligible(lowDef, CardSelectionFilter.MonsterAttackAtMost1500) &&
              !CardSelectionRules.IsEligible(nonMonster, CardSelectionFilter.MonsterAttackAtMost1500) &&
              !CardSelectionRules.IsEligible(strong, CardSelectionFilter.MonsterDefenseAtMost1500),
              "P006: exact card IDs, independent inclusive ATK/DEF 1500 filters, monsters only");

        var duel = DemoDuel.FromPartialObservation(1, [], Slots(sangan), Slots(attacker), 8000,
            side0Deck: [nonMonster, strong, lowAtk, lowDef, weak], effectShuffleSeed: 123);
        check(duel.BeginBattle(out _) && duel.Attack(0, 0, out _) &&
              duel.PendingFlipTarget is null &&
              duel.PendingCardSelection is { Owner: 0, Source: CardSelectionSource.Deck,
                  Destination: CardSelectionDestination.Hand,
                  Filter: CardSelectionFilter.MonsterAttackAtMost1500,
                  EffectResourceId: 47, MinSelections: 1, MaxSelections: 1 } &&
              duel.PendingCardSelection.EligibleCards.Select(c => c.SourceIndex).SequenceEqual([2, 4]) &&
              duel.Graveyard(0).Single() == sangan && duel.Monsters[0, 0] is null,
              "Sangan: being destroyed on opponent's turn opens own deck search (not FLIP)");
        var before = duel.DeckSnapshot(0).ToArray();
        check(!duel.ResolveCardSelection(0, out _) && !duel.ResolveCardSelection(3, out _) &&
              !duel.EndTurn(out _) && !duel.BeginBattle(out _) &&
              duel.HandFor(0).Count == 0 && duel.DeckSnapshot(0).SequenceEqual(before) &&
              duel.PendingCardSelection is not null,
              "P006: invalid choice, including nonmonster/high ATK, leaves deck untouched");
        check(duel.ResolveCardSelection(2, out _) &&
              duel.PendingCardSelection is null && duel.HandFor(0).Single() == lowAtk &&
              duel.DeckCount(0) == 4 && !duel.DeckSnapshot(0).Contains(lowAtk) &&
              !duel.ResolveCardSelection(4, out _) && duel.HandFor(1).Count == 0,
              "Sangan: selected occurrence moves to defender's hand and search resolves once");

        var witchDuel = DemoDuel.FromPartialObservation(0, [oneTribute], Slots(witch), Slots(), 8000,
            side0Deck: [strong, lowAtk, lowDef, nonMonster, weak]);
        check(witchDuel.Summon(0, 0, 0, true, out _, [0]) &&
              witchDuel.PendingCardSelection is { Owner: 0,
                  Filter: CardSelectionFilter.MonsterDefenseAtMost1500,
                  EffectResourceId: 573 } &&
              witchDuel.PendingCardSelection.EligibleCards.Select(c => c.SourceIndex).SequenceEqual([2, 4]) &&
              witchDuel.Graveyard(0).Single() == witch && witchDuel.Monsters[0, 0] == oneTribute &&
              witchDuel.Hand.Count == 0 && !witchDuel.EndTurn(out _),
              "Witch: normal tribute payment triggers even when replacement is summoned");
        check(witchDuel.ResolveCardSelection(2, out _) &&
              witchDuel.HandFor(0).Single() == lowDef &&
              witchDuel.DeckCount(0) == 4 && witchDuel.PendingCardSelection is null,
              "Witch: DEF<=1500 monster from deck to hand; high ATK is allowed");

        var noTarget = DemoDuel.FromPartialObservation(0, [oneTribute], Slots(sangan), Slots(), 8000,
            side0Deck: [strong, nonMonster]);
        check(noTarget.Summon(0, 0, 0, true, out _, [0]) &&
              noTarget.PendingCardSelection is null && noTarget.Graveyard(0).Single() == sangan &&
              noTarget.DeckCount(0) == 2 && noTarget.HandFor(0).Count == 0 &&
              noTarget.EndTurn(out _),
              "Sangan: no eligible cards does not open a selection or mutate deck");

        var badTribute = DemoDuel.FromPartialObservation(0, [oneTribute], Slots(sangan), Slots(), 8000,
            side0Deck: [weak]);
        check(!badTribute.Summon(0, 0, 0, true, out _, [0, 0]) &&
              badTribute.PendingCardSelection is null &&
              badTribute.DeckCount(0) == 1 && badTribute.Graveyard(0).Count == 0 &&
              badTribute.Monsters[0, 0] == sangan,
              "P006: rejected tribute never queues an effect or spends a card");

        var penguin = Monster(601, 10, 750, 500);
        var bounce = DemoDuel.FromPartialObservation(0, [], Slots(penguin), Slots(sangan), 8000,
            side1Deck: [weak]);
        bounce.MonsterFaceDown[0, 0] = true;
        check(bounce.RevealMonsterByEffect(0, 0, out _) &&
              bounce.ResolveFlipTarget(1, 0, out _) &&
              bounce.FinishFlipTargets(out _) &&
              bounce.Monsters[1, 0] is null && bounce.HandFor(1).Single() == sangan &&
              bounce.Graveyard(1).Count == 0 &&
              bounce.PendingCardSelection is null && bounce.DeckCount(1) == 1,
              "Sangan: returning from field to hand never triggers deck search");

        var bug = Monster(500, 11, 450, 600);
        var destroyedByEffect = DemoDuel.FromPartialObservation(0, [], Slots(bug), Slots(witch), 8000,
            side1Deck: [lowDef, strong]);
        destroyedByEffect.MonsterFaceDown[0, 0] = true;
        check(destroyedByEffect.RevealMonsterByEffect(0, 0, out _) &&
              destroyedByEffect.ResolveFlipTarget(1, 0, out _) &&
              destroyedByEffect.Graveyard(1).Single() == witch &&
              destroyedByEffect.PendingCardSelection is { Owner: 1,
                  Filter: CardSelectionFilter.MonsterDefenseAtMost1500 } &&
              destroyedByEffect.ResolveCardSelection(0, out _) &&
              destroyedByEffect.HandFor(1).Single() == lowDef,
              "Witch: field destruction by an effect triggers the owner's search");

        var two = DemoDuel.FromPartialObservation(0, [big], Slots(sangan, witch), Slots(), 8000,
            side0Deck: [lowAtk, lowDef, strong], effectShuffleSeed: 46);
        check(two.Summon(0, 0, 0, true, out _, [0, 1]) &&
              two.Graveyard(0).SequenceEqual([sangan, witch]) &&
              two.PendingCardSelection is { Owner: 0, EffectResourceId: 47 } &&
              two.PendingCardSelection.EligibleCards.Single().Card == lowAtk,
              "Two tribute searches: first effect is pending; second trigger is queued");
        int firstIndex = two.PendingCardSelection!.EligibleCards.Single().SourceIndex;
        check(two.ResolveCardSelection(firstIndex, out _) &&
              two.PendingCardSelection is { Owner: 0, EffectResourceId: 573 } &&
              two.PendingCardSelection.EligibleCards.Single().Card == lowDef &&
              two.HandFor(0).Single() == lowAtk,
              "Second tribute search sees newly shuffled deck, without losing the trigger");
        int secondIndex = two.PendingCardSelection!.EligibleCards.Single().SourceIndex;
        check(two.ResolveCardSelection(secondIndex, out _) &&
              two.PendingCardSelection is null &&
              two.HandFor(0).SequenceEqual([lowAtk, lowDef]) &&
              two.DeckSnapshot(0).SequenceEqual([strong]) &&
              two.Monsters[0, 0] == big,
              "Double tribute searches resolve serially with no duplicate or missing cards");

        var repeatA = DemoDuel.FromPartialObservation(0, [oneTribute], Slots(sangan), Slots(), 8000,
            side0Deck: [lowAtk, lowDef, weak, strong], effectShuffleSeed: 71);
        var repeatB = DemoDuel.FromPartialObservation(0, [oneTribute], Slots(sangan), Slots(), 8000,
            side0Deck: [lowAtk, lowDef, weak, strong], effectShuffleSeed: 71);
        repeatA.Summon(0, 0, 0, true, out _, [0]);
        repeatB.Summon(0, 0, 0, true, out _, [0]);
        check(repeatA.ResolveCardSelection(0, out _) &&
              repeatB.ResolveCardSelection(0, out _) &&
              repeatA.DeckSnapshot(0).Select(c => c.Index).SequenceEqual(
                  repeatB.DeckSnapshot(0).Select(c => c.Index)),
              "P006: seeded sandbox shuffle has deterministic order (not native RNG parity)");
    }
}
