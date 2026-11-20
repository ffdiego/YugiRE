using Joey.Core;

internal static class AttackTrapChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 800,
        int level = 4, int kind = 3, byte attribute = 2) =>
        new(index, id, $"Monster {id}", "", ((uint)kind << 20) |
            ((uint)level << 25) | ((uint)attribute << 29) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));

    private static Card Trap(ushort id, int index, int kind = 21) =>
        new(index, id, $"Trap {id}", "", (uint)kind << 20);

    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null,
        Card? d = null, Card? e = null) => [a, b, c, d, e];

    public static void Run(Action<bool, string> check)
    {
        var mirror = Trap(1056, 467);
        var wrongClass = Trap(1056, 1467, kind: 22);
        var otherTrap = Trap(1002, 120);
        var own = Monster(201, 301, 1300, 950);
        var attacker = Monster(202, 302, 2000, 1000);
        var attacker2 = Monster(203, 303, 1600, 800);
        var defense = Monster(204, 304, 1100, 1800);
        var facedown = Monster(561, 674, 750, 600);
        var tomato = Monster(1123, 940, 1400, 1100);

        var declaration = new DemoAttackDeclaration(1, 0, attacker, 0, own);
        check(AttackTrapRules.Supports(1056) &&
              !AttackTrapRules.Supports(1055) &&
              AttackTrapRules.IsEligible(mirror, 0, declaration) &&
              !AttackTrapRules.IsEligible(mirror, 1, declaration) &&
              !AttackTrapRules.IsEligible(wrongClass, 0, declaration) &&
              !AttackTrapRules.IsEligible(otherTrap, 0, declaration) &&
              MonsterWipeRules.AttackPositionTargets(1,
                  new Card?[2, 5], new bool[2, 5], new bool[2, 5]).Count == 0,
              "P201: original ID 1056, class 21, opponent attack declaration only");

        // Mirror Force must resolve BEFORE damage calculation and before the
        // defender is flipped face-up by a battle.
        var wipe = DemoDuel.FromPartialObservation(0, [mirror],
            Slots(facedown, own), Slots(attacker, tomato, defense, attacker2), 8000,
            side1Deck: [tomato]);
        wipe.MonsterFaceDown[0, 0] = wipe.DefensePosition[0, 0] = true;
        wipe.DefensePosition[1, 2] = true;
        wipe.MonsterFaceDown[1, 3] = wipe.DefensePosition[1, 3] = true;
        check(wipe.SetFromHand(0, out _) && wipe.Spells[0, 0] == mirror &&
              wipe.SpellFaceDown[0, 0] && wipe.PendingAttackResponse is null,
              "Mirror Force: Set does not activate Trap or consume an attack");
        wipe.NextTurn();
        check(wipe.BeginBattle(out _) && wipe.Attack(0, 0, out _) &&
              wipe.PendingAttackResponse is {
                  Attack: { AttackingPlayer: 1, AttackerZone: 0,
                      TargetZone: 0, DirectAttack: false } } &&
              wipe.PendingAttackResponse.EligibleTraps.Single() is {
                  Owner: 0, Zone: 0 } &&
              wipe.PendingAttackResponse.EligibleTraps.Single().Trap == mirror &&
              wipe.MonsterFaceDown[0, 0] &&
              wipe.Monsters[0, 0] == facedown &&
              wipe.Monsters[1, 0] == attacker &&
              wipe.Graveyard(0).Count == 0 &&
              wipe.Graveyard(1).Count == 0 &&
              wipe.Life.SequenceEqual([8000, 8000]) &&
              !wipe.EndTurn(out _) && !wipe.Attack(1, 1, out _),
              "Mirror Force: declare attack opens response before FLIP/damage and blocks another action");
        check(!wipe.ActivateAttackTrap(1, 0, out _) &&
              !wipe.ActivateAttackTrap(0, 1, out _) &&
              wipe.Graveyard(0).Count == 0 && wipe.PendingAttackResponse is not null,
              "Mirror Force: invalid owner or zone cannot consume a Trap");
        check(wipe.ActivateAttackTrap(0, 0, out _) &&
              wipe.PendingAttackResponse is null &&
              wipe.LastAttackTrap is {
                  TrapResourceId: 1056, Responder: 0, AttackingPlayer: 1,
                  DestroyedMonsters: 2, AttackContinued: false } &&
              wipe.LastMonsterWipe is {
                  EffectResourceId: 1056, Caster: 0,
                  DestroyedPlayer0: 0, DestroyedPlayer1: 2, DestroyedFaceDown: 0 } &&
              wipe.Monsters[1, 0] is null && wipe.Monsters[1, 1] is null &&
              wipe.Monsters[1, 2] == defense && wipe.Monsters[1, 3] == attacker2 &&
              wipe.Monsters[0, 0] == facedown && wipe.MonsterFaceDown[0, 0] &&
              wipe.Graveyard(0).Single() == mirror &&
              wipe.Graveyard(1).SequenceEqual([attacker, tomato]) &&
              wipe.DeckSnapshot(1).Single() == tomato &&
              wipe.Life.SequenceEqual([8000, 8000]) &&
              wipe.PendingCardSelection is null &&
              !wipe.PassAttackResponse(out _) &&
              !wipe.ActivateAttackTrap(0, 0, out _),
              "Mirror Force: destroy ALL attack-position opponent monsters, not own/DEF/set; no FLIP, battle effects or LP");
        check(wipe.Attack(3, 0, out _) == false,
              "Mirror Force: face-down defender cannot be attacked by a set monster");

        // Passing the response continues the SAME attack, with no second
        // declaration/window, then performs Damage Step and FLIP as usual.
        var mask = Monster(101, 306, 900, 400);
        var trapInGrave = Trap(109, 512);
        var passed = DemoDuel.FromPartialObservation(0, [mirror],
            Slots(mask), Slots(attacker), 8000,
            side0Graveyard: [trapInGrave]);
        passed.MonsterFaceDown[0, 0] = passed.DefensePosition[0, 0] = true;
        passed.SetFromHand(0, out _);
        passed.NextTurn();
        check(passed.BeginBattle(out _) && passed.Attack(0, 0, out _) &&
              passed.PendingAttackResponse is not null &&
              passed.PendingCardSelection is null && passed.MonsterFaceDown[0, 0] &&
              passed.Graveyard(0).SequenceEqual([trapInGrave]) &&
              passed.PassAttackResponse(out _) &&
              passed.PendingAttackResponse is null &&
              passed.Spells[0, 0] == mirror && passed.SpellFaceDown[0, 0] &&
              !passed.MonsterFaceDown[0, 0] &&
              passed.PendingCardSelection is {
                  Owner: 0, EffectResourceId: 101,
                  Source: CardSelectionSource.Graveyard,
                  Destination: CardSelectionDestination.Hand } &&
              !passed.Attack(0, 0, out _) &&
              passed.ResolveCardSelection(0, out _) &&
              passed.HandFor(0).Single() == trapInGrave &&
              passed.Graveyard(0).Single() == mask &&
              passed.Life.SequenceEqual([8000, 8000]),
              "Mirror Force Pass: same attack resumes once, defender FLIP occurs, no Trap spent or repeated attack");

        // Response also applies to direct attack, before LP is touched.
        var direct = DemoDuel.FromPartialObservation(0, [mirror],
            Slots(), Slots(attacker), 8000);
        direct.SetFromHand(0, out _); direct.NextTurn();
        check(direct.BeginBattle(out _) && direct.Attack(0, -1, out _) &&
              direct.PendingAttackResponse is { Attack: { DirectAttack: true, TargetZone: -1 } } &&
              direct.Life.SequenceEqual([8000, 8000]) &&
              direct.ActivateAttackTrap(0, 0, out _) &&
              direct.Life.SequenceEqual([8000, 8000]) &&
              direct.Monsters[1, 0] is null &&
              direct.Graveyard(1).Single() == attacker &&
              direct.Graveyard(0).Single() == mirror,
              "Mirror Force: direct attack declaration offers Trap before damage; activation prevents damage");

        var directPass = DemoDuel.FromPartialObservation(0, [mirror],
            Slots(), Slots(attacker), 8000);
        directPass.SetFromHand(0, out _); directPass.NextTurn();
        check(directPass.BeginBattle(out _) && directPass.Attack(0, -1, out _) &&
              directPass.PassAttackResponse(out _) &&
              directPass.Life[0] == 6000 && directPass.Life[1] == 8000 &&
              directPass.Spells[0, 0] == mirror &&
              !directPass.Attack(0, -1, out _) &&
              directPass.PendingAttackResponse is null,
              "Mirror Force: passing direct attack applies exactly one hit and consumes attack");

        // A Trap with the right resource ID but wrong DAT class cannot react.
        var wrong = DemoDuel.FromPartialObservation(0, [wrongClass],
            Slots(own), Slots(attacker), 8000);
        wrong.SetFromHand(0, out _); wrong.NextTurn();
        check(wrong.BeginBattle(out _) && wrong.Attack(0, 0, out _) &&
              wrong.PendingAttackResponse is null &&
              wrong.Spells[0, 0] == wrongClass &&
              wrong.Life[0] == 7300 &&
              wrong.Graveyard(0).Single() == own,
              "Mirror Force: DAT class mismatch is not a legal Trap activation");

        // Resolved card-effect destruction still emits Sangan/Witch triggers
        // after all attackers have gone to their respective graveyards.
        var sangan = Monster(47, 307, 1000, 600);
        var witch = Monster(573, 308, 1100, 1200);
        var lowAtk = Monster(310, 309, 1200, 1900);
        var lowDef = Monster(311, 310, 1900, 1200);
        var triggers = DemoDuel.FromPartialObservation(0, [mirror],
            Slots(own), Slots(attacker, sangan, witch), 8000,
            side1Deck: [lowAtk, lowDef]);
        triggers.SetFromHand(0, out _); triggers.NextTurn();
        check(triggers.BeginBattle(out _) && triggers.Attack(0, 0, out _) &&
              triggers.ActivateAttackTrap(0, 0, out _) &&
              triggers.Graveyard(1).SequenceEqual([attacker, sangan, witch]) &&
              triggers.PendingCardSelection is { Owner: 1, EffectResourceId: 47 } &&
              triggers.PendingCardSelection.EligibleCards.Single().Card == lowAtk &&
              triggers.ResolveCardSelection(0, out _) &&
              triggers.HandFor(1).Single() == lowAtk &&
              triggers.PendingCardSelection is { Owner: 1, EffectResourceId: 573 } &&
              triggers.PendingCardSelection.EligibleCards.Single().Card == lowDef &&
              triggers.ResolveCardSelection(0, out _) &&
              triggers.HandFor(1).SequenceEqual([lowAtk, lowDef]) &&
              triggers.PendingCardSelection is null &&
              triggers.Graveyard(1).Count == 3,
              "Mirror Force: field-to-grave Sangan/Witch effects remain queued after wipe and resolve serially");

        // Absent eligible Trap: normal attack should still resolve immediately.
        var noTrap = DemoDuel.FromPartialObservation(1, [],
            Slots(own), Slots(attacker), 8000);
        check(noTrap.BeginBattle(out _) && noTrap.Attack(0, 0, out _) &&
              noTrap.PendingAttackResponse is null &&
              noTrap.Monsters[0, 0] is null &&
              noTrap.Graveyard(0).Single() == own &&
              noTrap.Life[0] == 7300 &&
              noTrap.Monsters[1, 0] == attacker,
              "P201: no Mirror Force set means original battle outcome and damage remain unchanged");
    }
}
