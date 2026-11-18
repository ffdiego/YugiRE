using Joey.Core;

internal static class CardSelectionChecks
{
    private static Card Monster(ushort id, int index, int attack = 400, int defense = 500) =>
        new(index, id, $"Monster {id}", "", (1u << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card Spell(int index) => new(index, (ushort)(342 + index), $"Spell {index}", "", 22u << 20);
    private static Card Trap(int index) => new(index, (ushort)(1300 + index), $"Trap {index}", "", 21u << 20);
    private static Card?[] Slots(Card? first = null) => [first, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var faith = Monster(427, 501);
        var mask = Monster(101, 502);
        var spell1 = Spell(31);
        var spell2 = Spell(32);
        var trap = Trap(41);
        var attacker = Monster(8, 600, 2000, 1200);

        check(FlipEffectRules.Effect(427) == DemoFlipEffect.RecoverFromGraveyard &&
              FlipEffectRules.Effect(101) == DemoFlipEffect.RecoverFromGraveyard &&
              !FlipEffectRules.Supports(47) && !FlipEffectRules.Supports(573) &&
              CardSelectionRules.FlipGraveyardFilter(427) == CardSelectionFilter.Spell &&
              CardSelectionRules.FlipGraveyardFilter(101) == CardSelectionFilter.Trap &&
              CardSelectionRules.IsEligible(spell1, CardSelectionFilter.Spell) &&
              !CardSelectionRules.IsEligible(trap, CardSelectionFilter.Spell),
              "P018: exact IDs, DAT Spell=22/Trap=21, Sangan/Witch not FLIPs");

        var summon = DemoDuel.FromPartialObservation(0, [faith], Slots(), Slots(), 8000,
            side0Graveyard: [trap, spell1, spell2]);
        check(summon.SetFromHand(0, out _), "Magician of Faith: Set before FLIP");
        summon.NextTurn(); summon.NextTurn();
        check(summon.ChangePosition(0, out _) &&
              summon.PendingCardSelection is { Owner: 0,
                  Source: CardSelectionSource.Graveyard, Destination: CardSelectionDestination.Hand,
                  Filter: CardSelectionFilter.Spell, MinSelections: 1, MaxSelections: 1 } &&
              summon.PendingCardSelection.EligibleCards.Select(c => c.SourceIndex).SequenceEqual([1, 2]) &&
              summon.PendingFlipTarget is null &&
              !summon.EndTurn(out _) && !summon.BeginBattle(out _) && !summon.Draw(out _) &&
              !summon.ChangePosition(0, out _),
              "Magician of Faith: one spell; normal actions blocked during choice");
        check(!summon.ResolveCardSelection(0, out _) && !summon.ResolveCardSelection(99, out _) &&
              summon.Graveyard(0).SequenceEqual([trap, spell1, spell2]) &&
              summon.HandFor(0).Count == 0 && summon.PendingCardSelection is not null,
              "P018: invalid source positions are rejected atomically");
        check(summon.ResolveCardSelection(2, out _) &&
              summon.PendingCardSelection is null && summon.HandFor(0).Single() == spell2 &&
              summon.Graveyard(0).SequenceEqual([trap, spell1]) &&
              !summon.ResolveCardSelection(1, out _),
              "Magician of Faith: transfers selected instance once");

        var duplicates = DemoDuel.FromPartialObservation(0, [faith], Slots(), Slots(), 8000,
            side0Graveyard: [spell1, spell1]);
        duplicates.SetFromHand(0, out _); duplicates.NextTurn(); duplicates.NextTurn();
        check(duplicates.ChangePosition(0, out _) &&
              duplicates.PendingCardSelection!.EligibleCards.Select(c => c.SourceIndex).SequenceEqual([0, 1]) &&
              duplicates.ResolveCardSelection(1, out _) && duplicates.HandFor(0).Count == 1 &&
              duplicates.Graveyard(0).Count == 1,
              "P018: duplicate cards use zone positions, not catalog index");

        var darkness = DemoDuel.FromPartialObservation(0, [mask], Slots(), Slots(), 8000,
            side0Graveyard: [spell1, trap]);
        darkness.SetFromHand(0, out _); darkness.NextTurn(); darkness.NextTurn();
        check(darkness.ChangePosition(0, out _) &&
              darkness.PendingCardSelection is { Owner: 0, Filter: CardSelectionFilter.Trap } &&
              darkness.PendingCardSelection.EligibleCards.Single().SourceIndex == 1 &&
              darkness.ResolveCardSelection(1, out _) &&
              darkness.HandFor(0).Single() == trap && darkness.Graveyard(0).Single() == spell1,
              "Mask of Darkness: only trap returns to owner's hand");

        var empty = DemoDuel.FromPartialObservation(0, [mask], Slots(), Slots(), 8000,
            side0Graveyard: [spell1]);
        empty.SetFromHand(0, out _); empty.NextTurn(); empty.NextTurn();
        check(empty.ChangePosition(0, out _) && empty.PendingCardSelection is null &&
              empty.HandFor(0).Count == 0 && empty.Graveyard(0).Single() == spell1 &&
              empty.EndTurn(out _),
              "Mask of Darkness: no trap means no empty selection");

        var battle = DemoDuel.FromPartialObservation(1, [], Slots(faith), Slots(attacker), 8000,
            side0Graveyard: [spell1, trap]);
        battle.MonsterFaceDown[0, 0] = battle.DefensePosition[0, 0] = true;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.IsMarkedForBattleDestruction(0, 0) &&
              battle.PendingCardSelection is { Owner: 0, Filter: CardSelectionFilter.Spell } &&
              battle.Monsters[0, 0] == faith && battle.Graveyard(0).Count == 2 &&
              !battle.EndTurn(out _) && battle.ResolveCardSelection(0, out _) &&
              battle.PendingCardSelection is null && battle.Monsters[0, 0] is null &&
              battle.HandFor(0).Single() == spell1 &&
              battle.Graveyard(0).SequenceEqual([trap, faith]) &&
              battle.HandFor(1).Count == 0 && battle.Life[1] == 8000,
              "Magician of Faith: defender selects during attack, then marked monster dies");

        var noneInBattle = DemoDuel.FromPartialObservation(1, [], Slots(mask), Slots(attacker), 8000,
            side0Graveyard: [spell1]);
        noneInBattle.MonsterFaceDown[0, 0] = noneInBattle.DefensePosition[0, 0] = true;
        check(noneInBattle.BeginBattle(out _) && noneInBattle.Attack(0, 0, out _) &&
              noneInBattle.PendingCardSelection is null && noneInBattle.Monsters[0, 0] is null &&
              noneInBattle.Graveyard(0).SequenceEqual([spell1, mask]),
              "Mask of Darkness: normal battle completion if no trap is eligible");

        var revealed = DemoDuel.FromPartialObservation(0, [], Slots(faith), Slots(), 8000,
            side0Graveyard: [spell2]);
        revealed.MonsterFaceDown[0, 0] = true;
        check(revealed.RevealMonsterByEffect(0, 0, out _) &&
              revealed.PendingCardSelection is not null &&
              revealed.ResolveCardSelection(0, out _) &&
              !revealed.RevealMonsterByEffect(0, 0, out _) &&
              revealed.HandFor(0).Single() == spell2,
              "Magician of Faith: external reveal activates only once");
    }
}
