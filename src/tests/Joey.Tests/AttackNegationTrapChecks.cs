using Joey.Core;

internal static class AttackNegationTrapChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 800, int kind = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)kind << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));

    private static Card Trap(ushort id, int index, int kind = 21) =>
        new(index, id, $"Trap {id}", "", (uint)kind << 20);

    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null) =>
        [a, b, c, null, null];

    public static void Run(Action<bool, string> check)
    {
        var cylinder = Trap(1214, 894);
        var negate = Trap(960, 282);
        var mirror = Trap(1056, 467);
        var bogus = Trap(1214, 1894, 22);
        var attacker = Monster(777, 1101, 2000);
        var secondAttacker = Monster(778, 1102, 1600);
        var defender = Monster(780, 1103, 1400);
        var flipped = Monster(561, 674, 750, 600);

        var attack = new DemoAttackDeclaration(1, 0, attacker, 0, defender);
        check(AttackTrapRules.Supports(1056) &&
              AttackTrapRules.Supports(1214) &&
              AttackTrapRules.Supports(960) &&
              !AttackTrapRules.Supports(120) &&
              AttackTrapRules.IsEligible(cylinder, 0, attack) &&
              AttackTrapRules.IsEligible(negate, 0, attack) &&
              !AttackTrapRules.IsEligible(negate, 1, attack) &&
              !AttackTrapRules.IsEligible(bogus, 0, attack),
              "P200/P192: closed attack-Trap IDs, correct DAT class and opponent-only response");

        var cylinderDuel = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(defender), Slots(attacker, secondAttacker), 8000);
        check(cylinderDuel.SetFromHand(0, out _) &&
              cylinderDuel.Spells[0, 0] == cylinder &&
              cylinderDuel.SpellFaceDown[0, 0] &&
              cylinderDuel.PendingAttackResponse is null,
              "Magic Cylinder: placing Trap on field does not activate it");
        cylinderDuel.NextTurn();
        check(cylinderDuel.BeginBattle(out _) &&
              cylinderDuel.Attack(0, 0, out _) &&
              cylinderDuel.PendingAttackResponse is {
                  Attack: { AttackingPlayer: 1, TargetZone: 0, DirectAttack: false }
              } &&
              cylinderDuel.Life.SequenceEqual([8000, 8000]) &&
              cylinderDuel.Monsters[0, 0] == defender &&
              cylinderDuel.Monsters[1, 0] == attacker &&
              !cylinderDuel.EndTurn(out _) &&
              cylinderDuel.ActivateAttackTrap(0, 0, out _) &&
              cylinderDuel.PendingAttackResponse is null &&
              cylinderDuel.LastAttackTrap is {
                  TrapResourceId: 1214, Responder: 0, AttackingPlayer: 1,
                  DestroyedMonsters: 0, AttackContinued: false,
                  ReflectedDamage: 2000, BattlePhaseEnded: false } &&
              cylinderDuel.Life.SequenceEqual([8000, 6000]) &&
              cylinderDuel.Monsters[0, 0] == defender &&
              cylinderDuel.Monsters[1, 0] == attacker &&
              cylinderDuel.Spells[0, 0] is null &&
              cylinderDuel.Graveyard(0).Single() == cylinder &&
              cylinderDuel.Graveyard(1).Count == 0 &&
              cylinderDuel.Phase == DemoPhase.Battle &&
              !cylinderDuel.Attack(0, 0, out _),
              "Magic Cylinder: negate attack, reflect current ATK to attacking player, preserve both monsters and Battle Phase");
        check(cylinderDuel.Attack(1, 0, out _) &&
              cylinderDuel.Monsters[0, 0] is null &&
              cylinderDuel.Monsters[1, 1] == secondAttacker &&
              cylinderDuel.Life.SequenceEqual([7800, 6000]),
              "Magic Cylinder: a different monster can attack afterward in the same Battle Phase");

        var direct = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(), Slots(attacker), 8000);
        direct.SetFromHand(0, out _);
        direct.NextTurn();
        check(direct.BeginBattle(out _) && direct.Attack(0, -1, out _) &&
              direct.PendingAttackResponse is { Attack: { DirectAttack: true } } &&
              direct.Life.SequenceEqual([8000, 8000]) &&
              direct.ActivateAttackTrap(0, 0, out _) &&
              direct.Life.SequenceEqual([8000, 6000]) &&
              direct.Monsters[1, 0] == attacker &&
              direct.PendingAttackResponse is null &&
              !direct.Attack(0, -1, out _) &&
              direct.LastAttackTrap?.ReflectedDamage == 2000,
              "Magic Cylinder: direct attack is negated before damage and damage reflects once");

        var facedown = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(flipped), Slots(attacker), 8000);
        facedown.MonsterFaceDown[0, 0] = facedown.DefensePosition[0, 0] = true;
        facedown.SetFromHand(0, out _); facedown.NextTurn();
        check(facedown.BeginBattle(out _) && facedown.Attack(0, 0, out _) &&
              facedown.MonsterFaceDown[0, 0] &&
              facedown.ActivateAttackTrap(0, 0, out _) &&
              facedown.MonsterFaceDown[0, 0] &&
              facedown.LastDeckFlow is null &&
              facedown.Graveyard(0).Single() == cylinder,
              "Magic Cylinder: attack negation does not reveal defender or trigger its FLIP");

        // The current attack statistic, including an already active field bonus,
        // determines reflected damage. Warriors gain 200 from Sogen.
        var warrior = Monster(303, 1303, 1900, 1200, kind: 15);
        var buff = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(), Slots(warrior), 8000);
        buff.SetFromHand(0, out _);
        buff.FieldSpells[0] = new Card(2001, 332, "Sogen", "", 22u << 20);
        buff.NextTurn();
        check(buff.AttackValue(1, 0) == 2100 &&
              buff.BeginBattle(out _) && buff.Attack(0, -1, out _) &&
              buff.ActivateAttackTrap(0, 0, out _) &&
              buff.LastAttackTrap is { ReflectedDamage: 2100 } &&
              buff.Life.SequenceEqual([8000, 5900]),
              "Magic Cylinder: use attacker current ATK with modeled Field bonus");

        var negation = DemoDuel.FromPartialObservation(0, [negate],
            Slots(defender), Slots(attacker, secondAttacker), 8000);
        negation.SetFromHand(0, out _); negation.NextTurn();
        check(negation.BeginBattle(out _) && negation.Attack(0, 0, out _) &&
              negation.PendingAttackResponse is not null &&
              negation.ActivateAttackTrap(0, 0, out _) &&
              negation.LastAttackTrap is {
                  TrapResourceId: 960, ReflectedDamage: 0,
                  DestroyedMonsters: 0, BattlePhaseEnded: true,
                  AttackContinued: false } &&
              negation.Life.SequenceEqual([8000, 8000]) &&
              negation.Monsters[0, 0] == defender &&
              negation.Monsters[1, 0] == attacker &&
              negation.Monsters[1, 1] == secondAttacker &&
              negation.Phase == DemoPhase.Main &&
              !negation.BeginBattle(out _) &&
              !negation.Attack(1, 0, out _) &&
              negation.Graveyard(0).Single() == negate &&
              negation.Graveyard(1).Count == 0 &&
              negation.EndTurn(out _) &&
              negation.ActivePlayer == 0,
              "Negate Attack: cancel current attack AND end Battle Phase; remaining attackers cannot attack this turn");
        negation.NextTurn();
        check(negation.ActivePlayer == 1 && negation.BeginBattle(out _),
              "Negate Attack: Battle Phase gate resets on next turn of attacking player");

        var directNegate = DemoDuel.FromPartialObservation(0, [negate],
            Slots(), Slots(attacker), 8000);
        directNegate.SetFromHand(0, out _); directNegate.NextTurn();
        check(directNegate.BeginBattle(out _) && directNegate.Attack(0, -1, out _) &&
              directNegate.PendingAttackResponse is { Attack: { DirectAttack: true } } &&
              directNegate.ActivateAttackTrap(0, 0, out _) &&
              directNegate.Life.SequenceEqual([8000, 8000]) &&
              directNegate.Phase == DemoPhase.Main && !directNegate.BeginBattle(out _),
              "Negate Attack: direct attack canceled without damage; Battle Phase ends");

        // A response window lists every eligible Trap. Choosing one consumes
        // exactly that Trap, not both. This is NOT a chain implementation.
        var options = DemoDuel.FromPartialObservation(0, [cylinder, negate, mirror],
            Slots(defender), Slots(attacker), 8000);
        check(options.SetFromHand(0, out _) &&
              options.SetFromHand(0, out _) &&
              options.SetFromHand(0, out _),
              "P200/P192: set three alternatives before opponent's turn");
        options.NextTurn();
        check(options.BeginBattle(out _) && options.Attack(0, 0, out _) &&
              options.PendingAttackResponse is { EligibleTraps.Count: 3 } &&
              options.PendingAttackResponse.EligibleTraps
                  .Select(x => x.Trap.Id).SequenceEqual(new ushort[] { 1214, 960, 1056 }) &&
              options.ActivateAttackTrap(0, 1, out _) &&
              options.Spells[0, 0] == cylinder && options.Spells[0, 1] is null &&
              options.Spells[0, 2] == mirror &&
              options.Graveyard(0).Single() == negate &&
              options.PendingAttackResponse is null &&
              options.Phase == DemoPhase.Main,
              "P200/P192/P201: choose one response; others stay set, no phantom chain");

        var passed = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(defender), Slots(attacker), 8000);
        passed.SetFromHand(0, out _); passed.NextTurn();
        check(passed.BeginBattle(out _) && passed.Attack(0, 0, out _) &&
              passed.PassAttackResponse(out _) &&
              passed.Life.SequenceEqual([7400, 8000]) &&
              passed.Spells[0, 0] == cylinder &&
              passed.PendingAttackResponse is null &&
              !passed.Attack(0, 0, out _),
              "Magic Cylinder: Pass executes original 2000 vs 1400 combat once, preserves Trap");

        var invalid = DemoDuel.FromPartialObservation(0, [bogus],
            Slots(), Slots(attacker), 8000);
        invalid.SetFromHand(0, out _); invalid.NextTurn();
        check(invalid.BeginBattle(out _) && invalid.Attack(0, -1, out _) &&
              invalid.PendingAttackResponse is null &&
              invalid.Life.SequenceEqual([6000, 8000]) &&
              invalid.Spells[0, 0] == bogus,
              "P200: wrong DAT class cannot respond even with same resource ID");

        var lethal = DemoDuel.FromPartialObservation(0, [cylinder],
            Slots(), Slots(attacker), 8000);
        lethal.Life[1] = 1500;
        lethal.SetFromHand(0, out _); lethal.NextTurn();
        check(lethal.BeginBattle(out _) && lethal.Attack(0, -1, out _) &&
              lethal.ActivateAttackTrap(0, 0, out _) &&
              lethal.Life[1] == 0 && lethal.Winner == 0 && lethal.GameOver &&
              lethal.PendingAttackResponse is null &&
              !lethal.BeginBattle(out _) && !lethal.EndTurn(out _),
              "Magic Cylinder: lethal reflection ends duel for defender, attacker stays on field");
    }
}
