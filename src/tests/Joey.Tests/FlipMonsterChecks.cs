using Joey.Core;

internal static class FlipMonsterChecks
{
    private static Card Monster(ushort id, int index, int attack = 1000, int defense = 600) =>
        new(index, id, id == 500 ? "Man-Eater Bug" : id == 590 ? "Morphing Jar" : $"Monster {id}",
            "", (1u << 20) | (4u << 25) | ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card?[] Slots(Card? first = null, Card? second = null) =>
        [first, second, null, null, null];
    private static string Snapshot(DemoDuel duel) => string.Join(";", duel.Turn,
        duel.Phase, duel.Winner, duel.IsDraw, duel.PendingFlipTarget,
        string.Join(",", duel.Life),
        string.Join(",", duel.HandFor(0).Select(c => c.Index)),
        string.Join(",", duel.HandFor(1).Select(c => c.Index)),
        string.Join(",", duel.Monsters.Cast<Card?>().Select(c => c?.Index)),
        string.Join(",", Enumerable.Range(0, 2).Select(p => duel.Graveyard(p).Count)));

    public static void Run(Action<bool, string> check)
    {
        check(FlipEffectRules.Effect(500) == DemoFlipEffect.DestroyMonster &&
              FlipEffectRules.Effect(590) == DemoFlipEffect.ReplaceHands &&
              FlipEffectRules.Effect(1163) == DemoFlipEffect.None &&
              !FlipEffectRules.Supports(0),
              "FLIP: exact Man-Eater Bug and Morphing Jar resource IDs, not internal indices");

        var bug = Monster(500, 753, 450, 600);
        var jar = Monster(590, 972, 700, 600);
        var enemy = Monster(110, 114, 1300, 1000);

        // A face-down set cannot flip on its own summon turn.
        var flipSummon = DemoDuel.FromPartialObservation(0, [bug], Slots(), Slots(enemy), 8000);
        check(flipSummon.SetFromHand(0, out _) && flipSummon.MonsterFaceDown[0, 0] &&
              flipSummon.DefensePosition[0, 0] && !flipSummon.ChangePosition(0, out _) &&
              flipSummon.PendingFlipTarget is null,
              "FLIP: setting consumes normal summon and locks position on summon turn");
        flipSummon.NextTurn(); flipSummon.NextTurn();
        check(flipSummon.ChangePosition(0, out string prompt) &&
              !flipSummon.MonsterFaceDown[0, 0] && !flipSummon.DefensePosition[0, 0] &&
              flipSummon.PendingFlipTarget is { Owner: 0, SourceZone: 0, SourceCard.Id: 500 } &&
              prompt.Contains("choose", StringComparison.OrdinalIgnoreCase),
              "Man-Eater Bug: flip summon reveals and opens an explicit target selection");
        var pendingSnapshot = Snapshot(flipSummon);
        check(!flipSummon.ChangePosition(0, out _) && !flipSummon.BeginBattle(out _) &&
              !flipSummon.EndTurn(out _) && !flipSummon.Draw(out _) &&
              !flipSummon.CanSummonFromHand(0) && !flipSummon.Attack(0, 0, out _) &&
              Snapshot(flipSummon) == pendingSnapshot,
              "FLIP: no other action can bypass pending target resolution");
        check(!flipSummon.ResolveFlipTarget(-1, 0, out _) &&
              !flipSummon.ResolveFlipTarget(0, 4, out _) &&
              Snapshot(flipSummon) == pendingSnapshot,
              "FLIP: invalid or unoccupied target is rejected atomically");
        check(flipSummon.ResolveFlipTarget(1, 0, out string outcome) &&
              outcome.Contains("destroyed", StringComparison.OrdinalIgnoreCase) &&
              flipSummon.PendingFlipTarget is null &&
              flipSummon.Monsters[1, 0] is null &&
              flipSummon.Graveyard(1).Single() == enemy &&
              flipSummon.Monsters[0, 0] == bug && flipSummon.Life.SequenceEqual([8000, 8000]),
              "Man-Eater Bug: selecting opponent's monster destroys it without combat or LP change");
        check(!flipSummon.ResolveFlipTarget(1, 0, out _),
              "FLIP: one target resolves exactly once");

        var self = DemoDuel.FromPartialObservation(0, [bug], Slots(), Slots(), 8000);
        self.SetFromHand(0, out _); self.NextTurn(); self.NextTurn();
        check(self.ChangePosition(0, out _) && self.ResolveFlipTarget(0, 0, out _) &&
              self.Monsters[0, 0] is null && self.Graveyard(0).Single() == bug,
              "Man-Eater Bug: its mandatory selection may destroy its own monster");

        var defend = DemoDuel.FromPartialObservation(1, [], Slots(bug), Slots(enemy), 8000);
        defend.MonsterFaceDown[0, 0] = true; defend.DefensePosition[0, 0] = true;
        check(defend.BeginBattle(out _) && defend.Attack(0, 0, out _) &&
              defend.Monsters[0, 0] == bug && !defend.MonsterFaceDown[0, 0] &&
              defend.PendingFlipTarget is { Owner: 0 } &&
              defend.Life.SequenceEqual([8000, 8000]) &&
              !defend.Attack(0, 0, out _),
              "Man-Eater Bug: being attacked reveals it and pauses battle before damage");
        check(defend.ResolveFlipTarget(1, 0, out _) &&
              defend.Monsters[1, 0] is null && defend.Monsters[0, 0] == bug &&
              defend.Graveyard(1).Single() == enemy && defend.Life.SequenceEqual([8000, 8000]) &&
              !defend.Attack(0, 0, out _),
              "Man-Eater Bug: destroying attacker cancels battle damage, attack remains spent");

        var third = Monster(111, 115, 900, 800);
        var continueBattle = DemoDuel.FromPartialObservation(1, [],
            Slots(bug, third), Slots(enemy), 8000);
        continueBattle.MonsterFaceDown[0, 0] = true;
        continueBattle.DefensePosition[0, 0] = true;
        check(continueBattle.BeginBattle(out _) && continueBattle.Attack(0, 0, out _) &&
              continueBattle.ResolveFlipTarget(0, 1, out _) &&
              continueBattle.Monsters[0, 1] is null &&
              continueBattle.Monsters[0, 0] is null &&
              continueBattle.Monsters[1, 0] == enemy &&
              continueBattle.Graveyard(0).Select(c => c.Id).OrderBy(x => x)
                  .SequenceEqual(new ushort[] { 111, 500 }.OrderBy(x => x)) &&
              continueBattle.Life.SequenceEqual([8000, 8000]),
              "Man-Eater Bug: after destroying a third monster, pending battle resumes vs DEF");

        // Closed synthetic decks give both sides an actual draw pile of 35 cards
        // after the initial five-card deal. Do not rely on card names or RNG order.
        Card[] deck0 = Enumerable.Range(0, 40).Select(i => Monster(50, 100 + i)).ToArray();
        Card[] deck1 = Enumerable.Range(0, 40).Select(i => Monster(51, 200 + i)).ToArray();
        var hands = DemoDuel.FromDecks(deck0, deck1, seed: 23);
        hands.Monsters[0, 0] = jar;
        hands.MonsterFaceDown[0, 0] = true;
        hands.DefensePosition[0, 0] = true;
        var original0 = hands.HandFor(0).Select(c => c.Index).ToHashSet();
        var original1 = hands.HandFor(1).Select(c => c.Index).ToHashSet();
        hands.NextTurn(); hands.NextTurn();
        check(hands.ChangePosition(0, out _) && hands.PendingFlipTarget is null &&
              hands.Monsters[0, 0] == jar &&
              hands.HandFor(0).Count == 5 && hands.HandFor(1).Count == 5 &&
              hands.DeckCount(0) == 30 && hands.DeckCount(1) == 30 &&
              hands.Graveyard(0).Count == 5 && hands.Graveyard(1).Count == 5 &&
              !hands.HandFor(0).Any(c => original0.Contains(c.Index)) &&
              !hands.HandFor(1).Any(c => original1.Contains(c.Index)) &&
              !hands.GameOver,
              "Morphing Jar: flip summon discards both hands and draws five for each side");

        var attackedJar = DemoDuel.FromDecks(deck0, deck1, seed: 37);
        attackedJar.Monsters[0, 0] = jar;
        attackedJar.DefensePosition[0, 0] = true;
        attackedJar.MonsterFaceDown[0, 0] = true;
        attackedJar.Monsters[1, 0] = enemy;
        attackedJar.NextTurn();
        check(attackedJar.BeginBattle(out _) && attackedJar.Attack(0, 0, out _) &&
              attackedJar.Graveyard(0).Count == 6 && attackedJar.Graveyard(1).Count == 5 &&
              attackedJar.DeckCount(0) == 30 && attackedJar.DeckCount(1) == 30 &&
              attackedJar.Monsters[0, 0] is null && attackedJar.Life[1] == 8000 &&
              attackedJar.HandFor(0).Count == 5 && attackedJar.HandFor(1).Count == 5,
              "Morphing Jar: being attacked triggers both draws before normal battle destruction");

        var empty = DemoDuel.FromPartialObservation(0, [jar, third],
            Slots(jar), Slots(enemy), 8000);
        empty.MonsterFaceDown[0, 0] = true;
        empty.DefensePosition[0, 0] = true;
        check(empty.ChangePosition(0, out _) && empty.GameOver && empty.IsDraw &&
              empty.Winner is null && empty.HandFor(0).Count == 0 &&
              empty.Graveyard(0).Count == 2 &&
              !empty.Draw(out _) && !empty.EndTurn(out _),
              "Morphing Jar: both empty decks end the bounded sandbox in a draw");

        var noEffect = DemoDuel.FromPartialObservation(1, [],
            Slots(Monster(1163, 146)), Slots(enemy), 8000);
        noEffect.MonsterFaceDown[0, 0] = true;
        noEffect.DefensePosition[0, 0] = true;
        check(noEffect.BeginBattle(out _) && noEffect.Attack(0, 0, out _) &&
              noEffect.PendingFlipTarget is null && noEffect.Graveyard(0).Count == 1 &&
              noEffect.Life.SequenceEqual([8000, 8000]),
              "FLIP: Morphing Jar #2 must not silently reuse Morphing Jar behavior");
    }
}
