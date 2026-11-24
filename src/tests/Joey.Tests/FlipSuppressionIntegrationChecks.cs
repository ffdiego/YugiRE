using Joey.Core;

internal static class FlipSuppressionIntegrationChecks
{
    private static Card Monster(ushort id, int index, int attack = 700, int defense = 600) =>
        new(index, id, $"Monster {id}", "", (10u << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card Trap(ushort id, int index) =>
        new(index, id, $"Trap {id}", "", 21u << 20);
    private static Card Spell(ushort id, int index) =>
        new(index, id, $"Spell {id}", "", 22u << 20);
    private static Card?[] Slots(Card? first = null) =>
        [first, null, null, null, null];
    private static void Hide(DemoDuel duel)
    {
        duel.MonsterFaceDown[0, 0] = true;
        duel.DefensePosition[0, 0] = true;
    }

    public static void Run(Action<bool, string> check)
    {
        var green = Monster(585, 9585);
        var tornado = Monster(1521, 11521);
        var command = Trap(1530, 11530);
        var decree = Trap(1033, 11033);
        var first = Trap(1400, 14001);
        var second = Spell(342, 13421);
        var striker = Monster(45, 10500, attack: 2300, defense: 1000);

        var blockedGreen = DemoDuel.FromPartialObservation(0, [], Slots(green), Slots(), 8000);
        Hide(blockedGreen);
        blockedGreen.Spells[1, 4] = command;
        blockedGreen.Spells[0, 1] = first;
        blockedGreen.SpellFaceDown[0, 1] = true;
        blockedGreen.Spells[1, 2] = second;
        blockedGreen.SpellFaceDown[1, 2] = true;
        check(blockedGreen.ChangePosition(0, out string prompt) &&
              blockedGreen.FlipEffectsNegated &&
              prompt.Contains("negated", StringComparison.OrdinalIgnoreCase) &&
              blockedGreen.PendingFlipSpellTrapTargets is null &&
              blockedGreen.Spells[0, 1] == first && blockedGreen.Spells[1, 2] == second &&
              blockedGreen.MonsterWasFlipped[0, 0],
              "Integration: Royal Command denies Greenkappa without undoing its Flip Summon");

        var blockedBird = DemoDuel.FromPartialObservation(0, [], Slots(tornado), Slots(), 8000);
        Hide(blockedBird);
        blockedBird.Spells[1, 4] = command;
        blockedBird.Spells[0, 1] = first;
        blockedBird.Spells[1, 2] = second;
        check(blockedBird.RevealMonsterByEffect(0, 0, out _) &&
              blockedBird.PendingFlipSpellTrapTargets is null &&
              blockedBird.Spells[0, 1] == first && blockedBird.Spells[1, 2] == second &&
              blockedBird.HandFor(0).Count == 0 && blockedBird.HandFor(1).Count == 0,
              "Integration: Royal Command denies Tornado Bird external reveal");

        var lateGreen = DemoDuel.FromPartialObservation(0, [], Slots(green), Slots(), 8000);
        Hide(lateGreen);
        lateGreen.Spells[0, 1] = first;
        lateGreen.SpellFaceDown[0, 1] = true;
        lateGreen.Spells[1, 2] = second;
        lateGreen.SpellFaceDown[1, 2] = true;
        check(lateGreen.ChangePosition(0, out _) &&
              lateGreen.PendingFlipSpellTrapTargets is { Owner: 0 } &&
              lateGreen.SelectedFlipSpellTrapTargets.Count == 0,
              "Integration: Greenkappa queues both S/T targets before late suppression");
        lateGreen.Spells[1, 4] = command;
        check(lateGreen.SelectFlipSpellTrapTarget(0, 1, out string negation) &&
              negation.Contains("negated", StringComparison.OrdinalIgnoreCase) &&
              lateGreen.PendingFlipSpellTrapTargets is null &&
              lateGreen.SelectedFlipSpellTrapTargets.Count == 0 &&
              lateGreen.Spells[0, 1] == first && lateGreen.Spells[1, 2] == second &&
              lateGreen.Graveyard(0).Count == 0 && lateGreen.Graveyard(1).Count == 0 &&
              !lateGreen.SelectFlipSpellTrapTarget(1, 2, out _),
              "Integration: late Royal Command cancels Greenkappa before moving any S/T");

        var lateBird = DemoDuel.FromPartialObservation(0, [], Slots(tornado), Slots(), 8000);
        Hide(lateBird);
        lateBird.Spells[0, 1] = first;
        lateBird.Spells[1, 2] = second;
        check(lateBird.ChangePosition(0, out _) &&
              lateBird.SelectFlipSpellTrapTarget(0, 1, out _) &&
              lateBird.SelectedFlipSpellTrapTargets.Count == 1,
              "Integration: Tornado Bird remembers first target without moving it");
        lateBird.Spells[1, 4] = command;
        check(lateBird.SelectFlipSpellTrapTarget(1, 2, out _) &&
              lateBird.PendingFlipSpellTrapTargets is null &&
              lateBird.SelectedFlipSpellTrapTargets.Count == 0 &&
              lateBird.Spells[0, 1] == first && lateBird.Spells[1, 2] == second &&
              lateBird.HandFor(0).Count == 0 && lateBird.HandFor(1).Count == 0,
              "Integration: Royal Command cancels Tornado Bird after its first selection");

        var decreeWins = DemoDuel.FromPartialObservation(0, [], Slots(tornado), Slots(), 8000);
        Hide(decreeWins);
        decreeWins.Spells[0, 0] = command;
        decreeWins.Spells[1, 4] = decree;
        decreeWins.Spells[0, 1] = first;
        decreeWins.Spells[1, 2] = second;
        check(!decreeWins.FlipEffectsNegated &&
              decreeWins.RevealMonsterByEffect(0, 0, out _) &&
              decreeWins.PendingFlipSpellTrapTargets is not null &&
              decreeWins.SelectFlipSpellTrapTarget(1, 2, out _) &&
              decreeWins.SelectFlipSpellTrapTarget(0, 1, out _) &&
              decreeWins.HandFor(0).SequenceEqual([first]) &&
              decreeWins.HandFor(1).SequenceEqual([second]),
              "Integration: Royal Decree negates Royal Command so Tornado Bird returns two S/T");

        var battle = DemoDuel.FromPartialObservation(1, [], Slots(green), Slots(striker), 8000);
        Hide(battle);
        battle.Spells[1, 4] = command;
        battle.Spells[0, 1] = first;
        battle.SpellFaceDown[0, 1] = true;
        battle.Spells[1, 2] = second;
        battle.SpellFaceDown[1, 2] = true;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingFlipSpellTrapTargets is null &&
              battle.Spells[0, 1] == first && battle.Spells[1, 2] == second &&
              battle.Monsters[0, 0] is null &&
              battle.Graveyard(0).SequenceEqual([green]) &&
              battle.Life.SequenceEqual([8000, 8000]),
              "Integration: suppressed Greenkappa still dies by battle without destroying S/T");
    }
}
