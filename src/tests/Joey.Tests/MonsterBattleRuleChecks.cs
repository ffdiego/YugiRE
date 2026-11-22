using Joey.Core;

internal static class MonsterBattleRuleChecks
{
    private static Card Monster(ushort id, byte attribute, int attack = 1000) =>
        new(id, id, $"Monster {id}", "",
            (3u << 20) | (4u << 25) | ((uint)attribute << 29) | ((uint)(attack / 10) << 9) | 80u);

    public static void Run(Action<bool, string> check)
    {
        (ushort Id, byte Boost, byte Weaken)[] auras =
            [(491, 1, 2), (627, 2, 1), (523, 3, 4), (597, 4, 3), (526, 5, 6), (608, 6, 5)];
        foreach (var (id, boost, weaken) in auras)
        {
            var aura = Monster(id, boost, 500);
            var ally = Monster(900, weaken, 300);
            var enemy = Monster(901, boost, 1800);
            var duel = DemoDuel.FromPartialObservation(0, [aura],
                [ally, null, null, null, null], [enemy, null, null, null, null], 8000);
            check(duel.Summon(0, 0, 1, true, out _) &&
                  duel.AttackValue(0, 0) == 0 && duel.AttackValue(0, 1) == 1000 &&
                  duel.AttackValue(1, 0) == 2300 && duel.DefenseValue(1, 0) == 800 &&
                  MonsterAuraRules.AttackDelta(id, 0) == 0,
                $"Aura {id}: both sides, self, ATK only, nonnegative total and no unknown attribute bonus");
            duel.MonsterFaceDown[0, 1] = duel.DefensePosition[0, 1] = true;
            check(duel.AttackValue(0, 0) == 300 && duel.AttackValue(1, 0) == 1800,
                $"Aura {id}: face-down source contributes nothing");
            duel.MonsterFaceDown[0, 1] = false;
            duel.NextTurn();
            check(duel.BeginBattle(out _) && duel.Attack(0, 1, out _) &&
                  duel.Monsters[0, 1] is null && duel.AttackValue(0, 0) == 300 &&
                  duel.AttackValue(1, 0) == 1800,
                $"Aura {id}: face-up defense source applies until destroyed");
        }

        var light = Monster(910, 1);
        var dark = Monster(911, 2);
        var hoshiningen = Monster(491, 1);
        var apprentice = Monster(627, 2);
        var field = new Card(1129, 1129, "Luminous Spark", "", 22u << 20);
        var stacked = DemoDuel.FromPartialObservation(0, [field],
            [light, hoshiningen, null, null, null], [dark, hoshiningen, apprentice, null, null], 8000);
        check(stacked.Play(0, out _) && stacked.AttackValue(0, 0) == 2100 &&
              stacked.AttackValue(1, 0) == 700 && stacked.DefenseValue(0, 0) == 400,
            "Auras stack across sides and compose with a field spell before clamping");
        stacked.MonsterFaceDown[0, 0] = true;
        check(stacked.AttackValue(0, 0) == 1000 && stacked.DefenseValue(0, 0) == 800,
            "Face-down aura target keeps its underlying stats");

        var blocker = Monster(912, 2, 2400);
        foreach (ushort id in new ushort[] { 386, 396, 397, 421, 487, 634 })
        {
            var attacker = Monster(id, 2, 600);
            var duel = DemoDuel.FromPartialObservation(1, [],
                [blocker, null, null, null, null], [attacker, null, null, null, null], 8000);
            check(!duel.CanAttackDirectly(0) && duel.BeginBattle(out _) &&
                  duel.CanAttackDirectly(0) && duel.Attack(0, -1, out _) &&
                  duel.Life[0] == 7400 && duel.Monsters[0, 0] == blocker &&
                  !duel.CanAttackDirectly(0) && !duel.Attack(0, -1, out _),
                $"Direct attack {id}: ignores the blocker and uses up the normal attack");
        }

        var alligator = Monster(812, 6);
        var water = Monster(913, 3);
        var fire = Monster(914, 4);
        var earth = Monster(915, 5);
        var filtered = DemoDuel.FromPartialObservation(1, [],
            [water, fire, earth, blocker, null], [alligator, null, null, null, null], 8000);
        filtered.DefensePosition[0, 3] = true;
        check(filtered.BeginBattle(out _) && !filtered.CanAttackDirectly(0) &&
              !filtered.Attack(0, -1, out _) && filtered.CanAttack(0),
            "Alligator: disallowed face-up defender rejects direct attack without consuming it");
        filtered.MonsterFaceDown[0, 3] = true;
        check(filtered.CanAttackDirectly(0) && filtered.Attack(0, -1, out _) &&
              filtered.Life[0] == 7000 && filtered.MonsterFaceDown[0, 3],
            "Alligator: allowed visible attributes and a hidden defender permit direct damage");

        var mirror = new Card(1056, 1056, "Mirror Force", "", 21u << 20);
        var response = DemoDuel.FromPartialObservation(0, [mirror],
            [water, null, null, null, null], [alligator, null, null, null, null], 8000);
        check(response.SetFromHand(0, out _), "Direct response fixture sets Mirror Force");
        response.NextTurn();
        check(response.BeginBattle(out _) && response.Attack(0, -1, out _) &&
              response.PendingAttackResponse?.Attack.DirectAttack == true &&
              response.Life[0] == 8000,
            "A direct attack through a defender opens the standard response window");
        response.Monsters[0, 0] = blocker;
        check(response.PassAttackResponse(out _) && response.Life[0] == 8000 && !response.CanAttack(0),
            "Direct attack legality is checked again after a response");

        var ordinary = DemoDuel.FromPartialObservation(1, [],
            [blocker, null, null, null, null], [light, null, null, null, null], 8000);
        ordinary.MonsterFaceDown[0, 0] = true;
        check(ordinary.BeginBattle(out _) && !ordinary.CanAttackDirectly(0),
            "A hidden enemy still blocks an ordinary direct attack");

        var jinzo = Monster(421, 2, 600);
        var cpu = DemoDuel.FromPartialObservation(1, [],
            [blocker, null, null, null, null], [jinzo, null, null, null, null], 8000);
        check(cpu.BeginBattle(out _) &&
              new CpuDuelController(1).Step(cpu) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 } &&
              cpu.Life[0] == 7400,
            "CPU can make a legal direct attack through a stronger defending monster");
    }
}
