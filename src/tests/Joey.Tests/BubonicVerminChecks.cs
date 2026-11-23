using Joey.Core;

internal static class BubonicVerminChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 100) =>
        new(index, id, id == 1307 ? "Bubonic Vermin" : $"Monster {id}",
            "", (11u << 20) | (2u << 25) | ((uint)(atk / 10) << 9) | (uint)(def / 10));

    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null,
        Card? d = null, Card? e = null) => [a, b, c, d, e];

    public static void Run(Action<bool, string> check)
    {
        var vermin = Monster(1307, 588);
        var copy1 = Monster(1307, 1588);
        var copy2 = Monster(1307, 2588);
        var unrelated = Monster(1310, 1600);
        var attacker = Monster(50, 1601, atk: 2100);

        check(FlipEffectRules.Effect(1307) == DemoFlipEffect.RecruitSameNameFaceDown &&
              FlipEffectRules.Supports(1307) &&
              CardSelectionRules.IsEligible(copy1, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1307) &&
              !CardSelectionRules.IsEligible(unrelated, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1307) &&
              !CardSelectionRules.BattleSameNameRecruiter(1307),
              "Bubonic Vermin: correct resource ID, FLIP trigger and shared same-name filter");

        var summon = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [copy1, unrelated, copy2], effectShuffleSeed: 71);
        summon.MonsterFaceDown[0, 0] = summon.DefensePosition[0, 0] = true;
        int shuffles = 0, specials = 0;
        DemoSpecialSummon? placement = null;
        summon.DeckShuffled += player => { if (player == 0) shuffles++; };
        summon.MonsterSpecialSummoned += e => { specials++; placement = e; };
        check(summon.ChangePosition(0, out _) &&
              summon.PendingCardSelection is {
                  Owner: 0, Source: CardSelectionSource.Deck,
                  Destination: CardSelectionDestination.Field,
                  Filter: CardSelectionFilter.SameNameMonster,
                  RequiredResourceId: 1307, EffectResourceId: 1307,
                  MinSelections: 0, MaxSelections: 1,
                  SummonPosition: CardSelectionPosition.FaceDownDefense,
                  MaySelectFaceDownDefense: false } &&
              summon.PendingCardSelection.EligibleCards.Select(x => x.SourceIndex)
                  .SequenceEqual([0, 2]) &&
              summon.Monsters[0, 0] == vermin && !summon.MonsterFaceDown[0, 0] &&
              summon.MonsterWasFlipped[0, 0] && shuffles == 0 && specials == 0,
              "Bubonic Vermin: Flip Summon opens optional same-ID selection without moving the Deck");

        check(!summon.ResolveCardSelection(1, out _) &&
              !summon.ResolveCardSelection(0, 0, out _) &&
              summon.PendingCardSelection is not null && summon.DeckCount(0) == 3 &&
              shuffles == 0 && specials == 0,
              "Bubonic Vermin: wrong ID and occupied destination reject atomically");
        check(summon.ResolveCardSelection(2, 4, false, out _) &&
              summon.PendingCardSelection is null &&
              summon.Monsters[0, 4] == copy2 &&
              summon.MonsterFaceDown[0, 4] && summon.DefensePosition[0, 4] &&
              !summon.MonsterWasFlipped[0, 4] &&
              summon.DeckCount(0) == 2 && summon.DeckSnapshot(0).Contains(copy1) &&
              summon.DeckSnapshot(0).Contains(unrelated) &&
              placement is { Player: 0, Zone: 4, Card.Id: 1307, FaceDownDefense: true } &&
              shuffles == 1 && specials == 1 &&
              !summon.ResolveCardSelection(0, out _),
              "Bubonic Vermin: one chosen copy summons face-down DEF, shuffles once, and resolves once");

        var declined = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [copy1, unrelated]);
        declined.MonsterFaceDown[0, 0] = declined.DefensePosition[0, 0] = true;
        int declineShuffles = 0;
        declined.DeckShuffled += _ => declineShuffles++;
        check(declined.ChangePosition(0, out _) &&
              declined.SkipOptionalCardSelection(out _) &&
              declined.PendingCardSelection is null &&
              declined.DeckSnapshot(0).SequenceEqual([copy1, unrelated]) &&
              declined.Monsters[0, 1] is null && declineShuffles == 0 &&
              !declined.SkipOptionalCardSelection(out _),
              "Bubonic Vermin: optional decline leaves Deck unchanged and closes request");

        var noCopies = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [unrelated]);
        noCopies.MonsterFaceDown[0, 0] = noCopies.DefensePosition[0, 0] = true;
        check(noCopies.ChangePosition(0, out _) &&
              noCopies.PendingCardSelection is null &&
              noCopies.DeckSnapshot(0).SequenceEqual([unrelated]) &&
              noCopies.Monsters[0, 0] == vermin,
              "Bubonic Vermin: Flip Summon without a same-name copy never queues a selection");

        var full = DemoDuel.FromPartialObservation(0, [],
            Slots(vermin, unrelated, unrelated, unrelated, unrelated), Slots(), 8000,
            side0Deck: [copy1, copy2]);
        full.MonsterFaceDown[0, 0] = full.DefensePosition[0, 0] = true;
        check(full.ChangePosition(0, out _) &&
              full.PendingCardSelection is null && full.DeckCount(0) == 2 &&
              full.Monsters[0, 1] == unrelated,
              "Bubonic Vermin: no free monster zone suppresses recruitment without blocking Flip Summon");

        var faceUp = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [copy1]);
        check(faceUp.ChangePosition(0, out _) &&
              faceUp.PendingCardSelection is null && faceUp.DeckCount(0) == 1,
              "Bubonic Vermin: an ordinary face-up position change does not activate FLIP");

        var cpu = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [copy1, unrelated]);
        cpu.MonsterFaceDown[0, 0] = cpu.DefensePosition[0, 0] = true;
        check(cpu.ChangePosition(0, out _) &&
              new CpuDuelController(0).Step(cpu) is {
                  Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
              cpu.PendingCardSelection is null &&
              cpu.Monsters[0, 1] == copy1 && cpu.MonsterFaceDown[0, 1] &&
              cpu.DefensePosition[0, 1] && cpu.DeckCount(0) == 1,
              "CPU: optional single-card selection places Bubonic Vermin face-down, not in attack");

        var battle = DemoDuel.FromPartialObservation(1, [], Slots(vermin),
            Slots(attacker), 8000, side0Deck: [copy1, unrelated]);
        battle.MonsterFaceDown[0, 0] = battle.DefensePosition[0, 0] = true;
        int battleShuffles = 0;
        battle.DeckShuffled += _ => battleShuffles++;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingCardSelection is { Owner: 0, EffectResourceId: 1307,
                  MinSelections: 0, MaxSelections: 1 } &&
              battle.IsMarkedForBattleDestruction(0, 0) &&
              battle.Monsters[0, 0] == vermin && battle.Graveyard(0).Count == 0 &&
              battle.Life.SequenceEqual([8000, 8000]),
              "Bubonic Vermin: attacked face-down source queues choice before battle destruction");
        check(battle.ResolveCardSelection(0, out _) &&
              battle.PendingCardSelection is null &&
              battle.Monsters[0, 0] is null &&
              battle.Monsters[0, 1] == copy1 &&
              battle.MonsterFaceDown[0, 1] && battle.DefensePosition[0, 1] &&
              battle.Graveyard(0).SequenceEqual([vermin]) &&
              battle.DeckSnapshot(0).SequenceEqual([unrelated]) &&
              battleShuffles == 1 && battle.Life.SequenceEqual([8000, 8000]),
              "Bubonic Vermin: recruit resolves before marked source enters Graveyard");

        var battleDecline = DemoDuel.FromPartialObservation(1, [], Slots(vermin),
            Slots(attacker), 8000, side0Deck: [copy1, unrelated]);
        battleDecline.MonsterFaceDown[0, 0] = battleDecline.DefensePosition[0, 0] = true;
        check(battleDecline.BeginBattle(out _) && battleDecline.Attack(0, 0, out _) &&
              battleDecline.SkipOptionalCardSelection(out _) &&
              battleDecline.Monsters[0, 0] is null &&
              battleDecline.Graveyard(0).SequenceEqual([vermin]) &&
              battleDecline.DeckSnapshot(0).SequenceEqual([copy1, unrelated]) &&
              battleDecline.PendingCardSelection is null,
              "Bubonic Vermin: skipping the optional battle FLIP continues original destruction");

        var external = DemoDuel.FromPartialObservation(0, [], Slots(vermin), Slots(),
            8000, side0Deck: [copy1]);
        external.MonsterFaceDown[0, 0] = external.DefensePosition[0, 0] = true;
        check(external.RevealMonsterByEffect(0, 0, out _) &&
              external.PendingCardSelection is { EffectResourceId: 1307 } &&
              external.DefensePosition[0, 0] && !external.MonsterFaceDown[0, 0] &&
              external.SkipOptionalCardSelection(out _),
              "Bubonic Vermin: external effect reveal follows the existing sandbox FLIP path");
    }
}
