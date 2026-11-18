using Joey.Core;

/// <summary>
/// Hane-Hane is a mandatory single-target FLIP, unlike Penguin Soldier's
/// optional 0-2 returns. All tests are bounded sandbox observations.
/// </summary>
internal static class HaneHaneChecks
{
    private static Card Monster(ushort resourceId, int index, int attack, int defense, string name) =>
        new(index, resourceId, name, "",
            ((resourceId == FlipEffectRules.HaneHane ? 11u : 1u) << 20) |
            (4u << 25) | ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card?[] Slots(Card? first = null) => [first, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        Card hane = Monster(540, 723, 450, 500, "Hane-Hane");
        Card enemy = Monster(111, 300, 1300, 1000, "Attacker");
        Card weaker = Monster(112, 301, 400, 200, "Weaker");

        check(FlipEffectRules.Effect(FlipEffectRules.HaneHane) == DemoFlipEffect.ReturnOne &&
              FlipEffectRules.Supports(540) &&
              FlipEffectRules.Effect(1163) == DemoFlipEffect.ShuffleAndExcavate &&
              FlipEffectRules.Effect(601) == DemoFlipEffect.ReturnUpToTwo,
              "Hane-Hane: exact resource ID 540 has mandatory single return, not Penguin or Morphing Jar #2");

        var self = DemoDuel.FromPartialObservation(0, [hane], Slots(), Slots(), 8000);
        check(self.SetFromHand(0, out _) && self.MonsterFaceDown[0, 0] &&
              !self.ChangePosition(0, out _),
              "Hane-Hane: a newly set monster cannot Flip Summon on its summon turn");
        self.NextTurn(); self.NextTurn();
        check(self.ChangePosition(0, out string prompt) &&
              self.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnOne, Owner: 0 } &&
              prompt.Contains("exactly one", StringComparison.OrdinalIgnoreCase) &&
              self.SelectedFlipTargets.Count == 0 && self.Monsters[0, 0] == hane,
              "Hane-Hane: Flip Summon pauses for exactly one legal target");
        check(!self.FinishFlipTargets(out _) && self.PendingFlipTarget is not null &&
              !self.BeginBattle(out _) && !self.EndTurn(out _) &&
              !self.ResolveFlipTarget(1, 0, out _),
              "Hane-Hane: mandatory target cannot be skipped and other actions stay blocked");
        check(self.ResolveFlipTarget(0, 0, out _) &&
              self.PendingFlipTarget is null &&
              self.Monsters[0, 0] is null &&
              self.HandFor(0).Single() == hane &&
              self.Graveyard(0).Count == 0 &&
              !self.FinishFlipTargets(out _) && !self.ResolveFlipTarget(0, 0, out _),
              "Hane-Hane: after Flip Summon, itself may be the single monster returned");

        var other = DemoDuel.FromPartialObservation(0, [hane], Slots(), Slots(enemy), 8000);
        other.SetFromHand(0, out _); other.NextTurn(); other.NextTurn();
        check(other.ChangePosition(0, out _) && other.ResolveFlipTarget(1, 0, out _) &&
              other.HandFor(1).Single() == enemy &&
              other.Monsters[1, 0] is null && other.Monsters[0, 0] == hane &&
              other.HandFor(0).Count == 0 && other.Graveyard(1).Count == 0 &&
              other.SelectedFlipTargets.Count == 0,
              "Hane-Hane: selecting the opposing monster returns it to its owner's hand");

        var dying = DemoDuel.FromPartialObservation(1, [],
            Slots(hane), Slots(enemy), 8000);
        dying.MonsterFaceDown[0, 0] = dying.DefensePosition[0, 0] = true;
        check(dying.BeginBattle(out _) && dying.Attack(0, 0, out _) &&
              dying.IsMarkedForBattleDestruction(0, 0) &&
              dying.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnOne } &&
              !dying.ResolveFlipTarget(0, 0, out string rejection) &&
              rejection.Contains("battle-destroyed", StringComparison.OrdinalIgnoreCase) &&
              dying.Monsters[0, 0] == hane,
              "Hane-Hane: battle-destroyed FLIP cannot return itself despite remaining on field");
        check(dying.ResolveFlipTarget(1, 0, out _) &&
              dying.Monsters[0, 0] is null &&
              dying.Graveyard(0).Single() == hane &&
              dying.HandFor(0).Count == 0 &&
              dying.Monsters[1, 0] is null &&
              dying.HandFor(1).Single() == enemy &&
              dying.Life.SequenceEqual([8000, 8000]),
              "Hane-Hane: can bounce surviving attacker then be sent to graveyard by battle");

        var survived = DemoDuel.FromPartialObservation(1, [],
            Slots(hane), Slots(weaker), 8000);
        survived.MonsterFaceDown[0, 0] = survived.DefensePosition[0, 0] = true;
        check(survived.BeginBattle(out _) && survived.Attack(0, 0, out _) &&
              !survived.IsMarkedForBattleDestruction(0, 0) &&
              survived.Life[1] == 7900 &&
              survived.ResolveFlipTarget(0, 0, out _) &&
              survived.HandFor(0).Single() == hane &&
              survived.Monsters[0, 0] is null &&
              survived.Monsters[1, 0] == weaker &&
              survived.Life[1] == 7900,
              "Hane-Hane: surviving battle permits self-return after battle damage");

        var doubleDestruction = DemoDuel.FromPartialObservation(1, [],
            Slots(Monster(540, 724, 1300, 1000, "Hane-Hane")),
            Slots(enemy), 8000);
        doubleDestruction.MonsterFaceDown[0, 0] = true;
        // Deliberately face-down attack position to test no-target cleanup
        // when both combatants are marked for destruction by equal ATK.
        check(doubleDestruction.BeginBattle(out _) &&
              doubleDestruction.Attack(0, 0, out string noTargetMessage) &&
              doubleDestruction.PendingFlipTarget is null &&
              noTargetMessage.Contains("no eligible", StringComparison.OrdinalIgnoreCase) &&
              doubleDestruction.Monsters[0, 0] is null &&
              doubleDestruction.Monsters[1, 0] is null &&
              doubleDestruction.Graveyard(0).Count == 1 &&
              doubleDestruction.Graveyard(1).Count == 1,
              "Hane-Hane: no eligible targets cannot leave a deadlocked pending FLIP");

        var byEffect = DemoDuel.FromPartialObservation(0, [], Slots(hane), Slots(), 8000);
        byEffect.MonsterFaceDown[0, 0] = byEffect.DefensePosition[0, 0] = true;
        check(byEffect.RevealMonsterByEffect(0, 0, out _) &&
              byEffect.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnOne } &&
              !byEffect.MonsterFaceDown[0, 0] &&
              byEffect.DefensePosition[0, 0] &&
              byEffect.ResolveFlipTarget(0, 0, out _) &&
              byEffect.HandFor(0).Single() == hane &&
              !byEffect.RevealMonsterByEffect(0, 0, out _),
              "Hane-Hane: reveal-by-effect triggers once without a position change");
    }
}
