using Joey.Core;

internal static class BattleGraveyardEffectChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 100,
        int level = 4, byte attribute = 2, int type = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)type << 20) |
            ((uint)level << 25) | ((uint)attribute << 29) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null,
        Card? d = null, Card? e = null) => [a, b, c, d, e];

    public static void Run(Action<bool, string> check)
    {
        var momonga = Monster(1115, 453, 1000, 100, level: 2, attribute: 5, type: 11);
        var duplicate1 = Monster(1115, 1453, 1000, 100, level: 2, attribute: 5, type: 11);
        var duplicate2 = Monster(1115, 2453, 1000, 100, level: 2, attribute: 5, type: 11);
        var germ = Monster(1114, 383, 1000, 100, level: 2);
        var germ1 = Monster(1114, 1383, 1000, 100, level: 2);
        var germ2 = Monster(1114, 2383, 1000, 100, level: 2);
        var opponent = Monster(800, 1300, 2200, 1800);
        var irrelevant = Monster(901, 1301, 2000, 1500);
        var tribute = Monster(907, 1302, 2300, 1500, level: 6);
        var spear = Monster(1116, 516, 500, 500, level: 2);
        var anotherSpear = Monster(1116, 1516, 500, 500, level: 2);
        var ally = Monster(909, 1303, 1600, 1000);
        var enemyGrave = Monster(910, 1304, 1400, 1300);
        var bug = Monster(500, 1305, 450, 600, level: 2, type: 10);

        check(CardSelectionRules.BattleSameNameRecruiter(1115) &&
              CardSelectionRules.BattleSameNameRecruiter(1114) &&
              !CardSelectionRules.BattleSameNameRecruiter(1116) &&
              CardSelectionRules.IsEligible(duplicate1, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1115) &&
              !CardSelectionRules.IsEligible(germ, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1115) &&
              !CardSelectionRules.IsEligible(duplicate1, CardSelectionFilter.SameNameMonster) &&
              CardSelectionRules.IsEligible(ally, CardSelectionFilter.AnyMonster),
              "P168/P169: catalogued IDs, same resource ID duplicates, and generic Graveyard monster filter");

        var momongaDuel = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000,
            side0Deck: [duplicate1, irrelevant, duplicate2], effectShuffleSeed: 1984);
        momongaDuel.MonsterFaceDown[0, 0] = momongaDuel.DefensePosition[0, 0] = true;
        check(momongaDuel.BeginBattle(out _) && momongaDuel.Attack(0, 0, out _) &&
              momongaDuel.Life[0] == 9000 && momongaDuel.Life[1] == 8000 &&
              momongaDuel.Graveyard(0).Single() == momonga &&
              momongaDuel.PendingCardSelection is {
                  Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Field,
                  Filter: CardSelectionFilter.SameNameMonster, MinSelections: 0, MaxSelections: 2,
                  Owner: 0, RequiredResourceId: 1115, EffectResourceId: 1115,
                  SummonPosition: CardSelectionPosition.FaceDownDefense } &&
              momongaDuel.PendingCardSelection.EligibleCards
                  .Select(c => c.SourceIndex).SequenceEqual([0, 2]),
              "Nimble Momonga: battle sends to GY, heals 1000, then offers 0-2 same-name copies");

        check(momongaDuel.SelectBatchCard(0, out _) &&
              momongaDuel.SelectedBatchIndices.SequenceEqual([0]) &&
              momongaDuel.DeckCount(0) == 3 &&
              momongaDuel.Monsters[0, 0] is null &&
              !momongaDuel.SelectBatchCard(0, out _) &&
              !momongaDuel.SkipOptionalCardSelection(out _),
              "P168: one selection does not mutate deck/field and cannot be duplicated or skipped");
        check(momongaDuel.FinishOptionalCardSelection(out _) &&
              momongaDuel.PendingCardSelection is null &&
              momongaDuel.Monsters[0, 0] == duplicate1 &&
              momongaDuel.MonsterFaceDown[0, 0] && momongaDuel.DefensePosition[0, 0] &&
              momongaDuel.DeckSnapshot(0).Contains(duplicate2) &&
              momongaDuel.DeckCount(0) == 2 &&
              momongaDuel.SelectedBatchIndices.Count == 0,
              "Nimble Momonga: finish after one selected copy, summon set DEF and shuffle once");

        var bothMomonga = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000, side0Deck: [duplicate1, irrelevant, duplicate2]);
        bothMomonga.DefensePosition[0, 0] = true;
        check(bothMomonga.BeginBattle(out _) && bothMomonga.Attack(0, 0, out _) &&
              bothMomonga.SelectBatchCard(0, out _) &&
              bothMomonga.SelectBatchCard(2, out _) &&
              bothMomonga.PendingCardSelection is null &&
              bothMomonga.DeckSnapshot(0).SequenceEqual([irrelevant]) &&
              bothMomonga.Monsters[0, 0] == duplicate1 &&
              bothMomonga.Monsters[0, 1] == duplicate2 &&
              bothMomonga.MonsterFaceDown[0, 0] &&
              bothMomonga.MonsterFaceDown[0, 1],
              "Nimble Momonga: selecting two copies commits together with face-down DEF");

        var declined = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000, side0Deck: [duplicate1, duplicate2, irrelevant]);
        declined.DefensePosition[0, 0] = true;
        check(declined.BeginBattle(out _) && declined.Attack(0, 0, out _) &&
              declined.SkipOptionalCardSelection(out _) &&
              declined.PendingCardSelection is null &&
              declined.DeckSnapshot(0).SequenceEqual([duplicate1, duplicate2, irrelevant]) &&
              declined.Monsters[0, 0] is null && declined.Life[0] == 9000,
              "Nimble Momonga: skipping optional summons preserves mandatory heal and deck order");

        var germDuel = DemoDuel.FromPartialObservation(1, [], Slots(germ),
            Slots(opponent), 8000, side0Deck: [germ1, germ2]);
        germDuel.MonsterFaceDown[0, 0] = germDuel.DefensePosition[0, 0] = true;
        check(germDuel.BeginBattle(out _) && germDuel.Attack(0, 0, out _) &&
              germDuel.Life[1] == 7500 && germDuel.Life[0] == 8000 &&
              germDuel.PendingCardSelection is { Owner: 0, RequiredResourceId: 1114,
                  MaxSelections: 2, SummonPosition: CardSelectionPosition.FaceUpAttack } &&
              germDuel.SelectBatchCard(0, out _) && germDuel.SelectBatchCard(1, out _) &&
              germDuel.Monsters[0, 0] == germ1 && germDuel.Monsters[0, 1] == germ2 &&
              !germDuel.MonsterFaceDown[0, 0] && !germDuel.DefensePosition[0, 0] &&
              germDuel.DeckCount(0) == 0,
              "Giant Germ: opponent suffers mandatory 500 and 2 copies summon face-up attack");

        var empty = DemoDuel.FromPartialObservation(1, [], Slots(germ),
            Slots(opponent), 8000, side0Deck: [irrelevant]);
        empty.DefensePosition[0, 0] = true;
        check(empty.BeginBattle(out _) && empty.Attack(0, 0, out _) &&
              empty.PendingCardSelection is null && empty.Life[1] == 7500 &&
              empty.DeckSnapshot(0).Single() == irrelevant,
              "Giant Germ: mandatory damage applies even without extra copies");

        var oneSlot = DemoDuel.FromPartialObservation(1, [],
            Slots(momonga, irrelevant, irrelevant, irrelevant, irrelevant),
            Slots(opponent), 8000, side0Deck: [duplicate1, duplicate2]);
        oneSlot.DefensePosition[0, 0] = true;
        check(oneSlot.BeginBattle(out _) && oneSlot.Attack(0, 0, out _) &&
              oneSlot.PendingCardSelection is { MaxSelections: 1 } &&
              oneSlot.ResolveCardSelection(0, out _) &&
              oneSlot.PendingCardSelection is null &&
              oneSlot.Monsters[0, 0] == duplicate1 && oneSlot.MonsterFaceDown[0, 0] &&
              oneSlot.Monsters[0, 4] == irrelevant,
              "P168: available monster zones cap same-name summons to one");

        var tributeNoEffect = DemoDuel.FromPartialObservation(0, [tribute],
            Slots(germ), Slots(), 8000, side0Deck: [germ1]);
        check(tributeNoEffect.Summon(0, 0, 0, true, out _, [0]) &&
              tributeNoEffect.Graveyard(0).Single() == germ &&
              tributeNoEffect.PendingCardSelection is null &&
              tributeNoEffect.Life[1] == 8000 &&
              tributeNoEffect.DeckSnapshot(0).Single() == germ1,
              "Giant Germ: tribute is not battle destruction, no damage or summon");

        var flipThenRemoved = DemoDuel.FromPartialObservation(0, [],
            Slots(spear), Slots(bug), 8000,
            side0Graveyard: [ally, anotherSpear], side1Graveyard: [enemyGrave]);
        flipThenRemoved.MonsterFaceDown[0, 0] = flipThenRemoved.DefensePosition[0, 0] = true;
        flipThenRemoved.MonsterFaceDown[1, 0] = true;
        check(flipThenRemoved.RevealMonsterByEffect(0, 0, out _) &&
              flipThenRemoved.MonsterWasFlipped[0, 0] &&
              flipThenRemoved.PendingCardSelection is null &&
              flipThenRemoved.Graveyard(0).Count == 2,
              "Spear Cretin: FLIP marks history but never revives on reveal alone");
        check(flipThenRemoved.RevealMonsterByEffect(1, 0, out _) &&
              flipThenRemoved.ResolveFlipTarget(0, 0, out _) &&
              flipThenRemoved.Graveyard(0).Count == 3 &&
              flipThenRemoved.PendingCardSelection is { Owner: 0,
                  Source: CardSelectionSource.Graveyard,
                  Destination: CardSelectionDestination.Field,
                  Filter: CardSelectionFilter.AnyMonster,
                  MaySelectFaceDownDefense: true,
                  EffectResourceId: 1116, MinSelections: 1, MaxSelections: 1 } &&
              flipThenRemoved.PendingCardSelection.EligibleCards
                  .Select(x => x.SourceIndex).SequenceEqual([0, 1]) &&
              !flipThenRemoved.SkipOptionalCardSelection(out _) &&
              !flipThenRemoved.ResolveCardSelection(2, out _),
              "Spear Cretin: after later GY transition, owner selects one monster but not this source");
        check(flipThenRemoved.ResolveCardSelection(1, 4, true, out _) &&
              flipThenRemoved.Monsters[0, 4] == anotherSpear &&
              flipThenRemoved.MonsterFaceDown[0, 4] &&
              flipThenRemoved.DefensePosition[0, 4] &&
              !flipThenRemoved.MonsterWasFlipped[0, 4] &&
              flipThenRemoved.PendingCardSelection is { Owner: 1,
                  Destination: CardSelectionDestination.Field,
                  Source: CardSelectionSource.Graveyard } &&
              flipThenRemoved.ResolveCardSelection(0, null, false, out _) &&
              flipThenRemoved.Monsters[1, 1] == enemyGrave &&
              flipThenRemoved.PendingCardSelection is null &&
              flipThenRemoved.Graveyard(0).Single() == spear,
              "Spear Cretin: both owners choose separately; allow set DEF and attack; no self-revive");

        var unflipped = DemoDuel.FromPartialObservation(0, [],
            Slots(spear), Slots(bug), 8000,
            side0Graveyard: [ally], side1Graveyard: [enemyGrave]);
        unflipped.MonsterFaceDown[1, 0] = true;
        check(unflipped.RevealMonsterByEffect(1, 0, out _) &&
              unflipped.ResolveFlipTarget(0, 0, out _) &&
              unflipped.Graveyard(0).SequenceEqual([ally, spear]) &&
              unflipped.PendingCardSelection is null,
              "Spear Cretin: face-up without previous FLIP does not trigger on destruction");

        var battleSpear = DemoDuel.FromPartialObservation(1, [], Slots(spear),
            Slots(opponent), 8000, side0Graveyard: [ally], side1Graveyard: [enemyGrave]);
        battleSpear.MonsterFaceDown[0, 0] = battleSpear.DefensePosition[0, 0] = true;
        check(battleSpear.BeginBattle(out _) && battleSpear.Attack(0, 0, out _) &&
              battleSpear.PendingCardSelection is { Owner: 0, EffectResourceId: 1116 } &&
              battleSpear.Graveyard(0).SequenceEqual([ally, spear]) &&
              battleSpear.ResolveCardSelection(0, out _) &&
              battleSpear.PendingCardSelection is { Owner: 1, EffectResourceId: 1116 } &&
              battleSpear.ResolveCardSelection(0, out _) &&
              battleSpear.Monsters[0, 0] == ally && battleSpear.Monsters[1, 1] == enemyGrave,
              "Spear Cretin: revealing during battle triggers only after destruction and offers both sides");

        var nothingToRevive = DemoDuel.FromPartialObservation(1, [], Slots(spear),
            Slots(opponent), 8000);
        nothingToRevive.MonsterFaceDown[0, 0] = nothingToRevive.DefensePosition[0, 0] = true;
        check(nothingToRevive.BeginBattle(out _) && nothingToRevive.Attack(0, 0, out _) &&
              nothingToRevive.PendingCardSelection is null &&
              nothingToRevive.Graveyard(0).Single() == spear,
              "Spear Cretin: source alone in graveyard cannot self-special-summon");
    }
}
