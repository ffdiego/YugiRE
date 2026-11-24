using Joey.Core;

internal static class DimensionalWarriorChecks
{
    private static Card Monster(ushort id, int index, int atk = 1200, int def = 1000,
        int type = 15, int level = 4) =>
        new(index, id, id == 255 ? "Dimensional Warrior" : $"Monster {id}", "",
            ((uint)type << 20) | ((uint)level << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Trap(ushort id, int index) =>
        new(index, id, $"Trap {id}", "", 21u << 20);
    private static Card?[] Slots(Card? first = null) =>
        [first, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var dimensional = Monster(255, 46, 1200, 1000);
        var dimensional2 = Monster(255, 1046, 1300, 1100);
        var stronger = Monster(700, 1700, 2400, 2000);
        var weaker = Monster(701, 1701, 1000, 900);
        var germ = Monster(1114, 383, 1000, 1000, type: 3, level: 2);
        var sangan = Monster(47, 248, 1000, 600, type: 3, level: 3);
        var spear = Monster(1116, 516, 500, 500, type: 3, level: 2);
        var immortal = Monster(461, 164, 1500, 1300, type: 19, level: 4);
        var searched = Monster(800, 1800, 900, 2000);

        check(DimensionalWarriorRules.ResourceId == 255 &&
              DimensionalWarriorRules.BanishBothAfterDamageCalculation(255, 700) &&
              DimensionalWarriorRules.BanishBothAfterDamageCalculation(700, 255) &&
              DimensionalWarriorRules.BanishBothAfterDamageCalculation(255, 255) &&
              !DimensionalWarriorRules.BanishBothAfterDamageCalculation(46, 700) &&
              !DimensionalWarriorRules.BanishBothAfterDamageCalculation(700, 701),
              "P188: physical resource ID 255 not internal DAT index 46, either combatant");

        var attacking = DemoDuel.FromPartialObservation(1, [],
            Slots(stronger), Slots(dimensional), 8000);
        check(attacking.BeginBattle(out _) && attacking.Attack(0, 0, out _) &&
              attacking.Life.SequenceEqual([8000, 6800]) &&
              attacking.Monsters[0, 0] is null && attacking.Monsters[1, 0] is null &&
              attacking.Banished(0).SequenceEqual([stronger]) &&
              attacking.Banished(1).SequenceEqual([dimensional]) &&
              attacking.Graveyard(0).Count == 0 && attacking.Graveyard(1).Count == 0,
              "P188: weaker attacking Warrior suffers normal battle damage, then both banished");

        var defending = DemoDuel.FromPartialObservation(1, [],
            Slots(dimensional), Slots(stronger), 8000);
        defending.DefensePosition[0, 0] = defending.MonsterFaceDown[0, 0] = true;
        check(defending.BeginBattle(out _) && defending.Attack(0, 0, out _) &&
              defending.Life.SequenceEqual([8000, 8000]) &&
              defending.Banished(0).SequenceEqual([dimensional]) &&
              defending.Banished(1).SequenceEqual([stronger]) &&
              !defending.MonsterWasFlipped[0, 0] &&
              defending.Monsters[0, 0] is null && defending.Monsters[1, 0] is null,
              "P188: face-down Defense Warrior is revealed and banishes both, even if battle-destroyed");

        var defenseDamage = DemoDuel.FromPartialObservation(1, [],
            Slots(dimensional), Slots(weaker), 8000);
        defenseDamage.DefensePosition[0, 0] = true;
        check(defenseDamage.BeginBattle(out _) && defenseDamage.Attack(0, 0, out _) &&
              defenseDamage.Life.SequenceEqual([8000, 7800]) &&
              defenseDamage.Banished(0).Single() == dimensional &&
              defenseDamage.Banished(1).Single() == weaker,
              "P188: defense stat check and reflected battle damage occur before banishing both");

        var defenderAttack = DemoDuel.FromPartialObservation(1, [],
            Slots(dimensional), Slots(weaker), 8000);
        check(defenderAttack.BeginBattle(out _) && defenderAttack.Attack(0, 0, out _) &&
              defenderAttack.Life.SequenceEqual([8000, 7800]) &&
              defenderAttack.Banished(0).Single() == dimensional &&
              defenderAttack.Banished(1).Single() == weaker,
              "P188: attacking weaker monster takes battle damage against defending Warrior");

        var twoWarriors = DemoDuel.FromPartialObservation(1, [],
            Slots(dimensional2), Slots(dimensional), 8000);
        check(twoWarriors.BeginBattle(out _) && twoWarriors.Attack(0, 0, out _) &&
              twoWarriors.Banished(0).SequenceEqual([dimensional2]) &&
              twoWarriors.Banished(1).SequenceEqual([dimensional]) &&
              twoWarriors.Graveyard(0).Count == 0 && twoWarriors.Graveyard(1).Count == 0,
              "P188: when both cards are Dimensional Warrior, each physical occurrence banishes once");

        var againstGerm = DemoDuel.FromPartialObservation(1, [],
            Slots(germ), Slots(dimensional), 8000);
        againstGerm.DefensePosition[0, 0] = true;
        check(againstGerm.BeginBattle(out _) && againstGerm.Attack(0, 0, out _) &&
              againstGerm.Banished(0).SequenceEqual([germ]) &&
              againstGerm.Graveyard(0).Count == 0 &&
              againstGerm.PendingCardSelection is null &&
              againstGerm.Life.SequenceEqual([8000,8000]),
              "P188: banished Giant Germ cannot trigger its battle-to-Graveyard 500 burn or recruit");

        var againstSangan = DemoDuel.FromPartialObservation(1, [],
            Slots(sangan), Slots(dimensional), 8000, side0Deck: [searched]);
        check(againstSangan.BeginBattle(out _) && againstSangan.Attack(0, 0, out _) &&
              againstSangan.Banished(0).SequenceEqual([sangan]) &&
              againstSangan.DeckSnapshot(0).SequenceEqual([searched]) &&
              againstSangan.PendingCardSelection is null &&
              againstSangan.Graveyard(0).Count == 0,
              "P188: banished Sangan never enters the field-to-Graveyard search pipeline");

        var againstSpear = DemoDuel.FromPartialObservation(1, [],
            Slots(spear), Slots(dimensional), 8000);
        againstSpear.DefensePosition[0,0] = againstSpear.MonsterFaceDown[0,0] = true;
        check(againstSpear.RevealMonsterByEffect(0,0,out _) &&
              againstSpear.MonsterWasFlipped[0,0] &&
              againstSpear.BeginBattle(out _) && againstSpear.Attack(0,0,out _) &&
              againstSpear.Banished(0).SequenceEqual([spear]) &&
              againstSpear.Graveyard(0).Count == 0 &&
              againstSpear.PendingCardSelection is null,
              "P188: flipped Spear Cretin banished in battle does not revive from Graveyard");

        var againstImmortal = DemoDuel.FromPartialObservation(1, [],
            Slots(immortal), Slots(dimensional), 8000);
        againstImmortal.DefensePosition[0, 0] = againstImmortal.MonsterFaceDown[0, 0] = true;
        check(againstImmortal.RevealMonsterByEffect(0, 0, out _) &&
              againstImmortal.Life[0] == 11000 &&
              againstImmortal.BeginBattle(out _) && againstImmortal.Attack(0, 0, out _) &&
              againstImmortal.Banished(0).SequenceEqual([immortal]) &&
              againstImmortal.Graveyard(0).Count == 0 &&
              againstImmortal.Life.SequenceEqual([11000, 8000]),
              "P188: previously flipped Immortal Thunder is banished, no 5000 LP loss");

        var direct = DemoDuel.FromPartialObservation(1, [],
            Slots(), Slots(dimensional), 8000);
        check(direct.BeginBattle(out _) && direct.Attack(0, -1, out _) &&
              direct.Life.SequenceEqual([6800,8000]) &&
              direct.Monsters[1,0] == dimensional &&
              direct.Banished(0).Count == 0 && direct.Banished(1).Count == 0,
              "P188: direct attack damages player but does not activate two-monster banishment");

        var ordinary = DemoDuel.FromPartialObservation(1, [],
            Slots(weaker), Slots(stronger), 8000);
        check(ordinary.BeginBattle(out _) && ordinary.Attack(0,0,out _) &&
              ordinary.Monsters[1,0] == stronger &&
              ordinary.Graveyard(0).SequenceEqual([weaker]) &&
              ordinary.Banished(0).Count == 0 && ordinary.Banished(1).Count == 0,
              "P188 regression: battles without the resource ID still use Graveyard destruction");

        var royal = DemoDuel.FromPartialObservation(1, [],
            Slots(stronger), Slots(dimensional), 8000);
        royal.Spells[0,0] = Trap(1530, 11530);
        check(royal.FlipEffectsNegated &&
              royal.BeginBattle(out _) && royal.Attack(0,0,out _) &&
              royal.Banished(0).SequenceEqual([stronger]) &&
              royal.Banished(1).SequenceEqual([dimensional]),
              "P188: Royal Command only suppresses FLIP effects, not Dimensional Warrior battle rule");

        var mirror = DemoDuel.FromPartialObservation(1, [],
            Slots(stronger), Slots(dimensional), 8000);
        var mirrorForce = Trap(AttackTrapRules.MirrorForce, 445);
        mirror.Spells[0,0] = mirrorForce;
        mirror.SpellFaceDown[0,0] = true;
        check(mirror.BeginBattle(out _) && mirror.Attack(0,0,out _) &&
              mirror.PendingAttackResponse is not null &&
              mirror.ActivateAttackTrap(0,0,out _) &&
              mirror.Graveyard(1).Contains(dimensional) &&
              mirror.Graveyard(0).Contains(mirrorForce) &&
              mirror.Banished(0).Count == 0 && mirror.Banished(1).Count == 0 &&
              mirror.Monsters[1,0] is null && mirror.Monsters[0,0] == stronger,
              "P188: Mirror Force ends attack before damage calculation; no banish");

        var hiddenSangan = DemoDuel.FromPartialObservation(1, [],
            Slots(sangan), Slots(dimensional), 8000, side0Deck: [searched]);
        hiddenSangan.DefensePosition[0,0] = hiddenSangan.MonsterFaceDown[0,0] = true;
        check(hiddenSangan.BeginBattle(out _) && hiddenSangan.Attack(0,0,out _) &&
              hiddenSangan.Banished(0).Single() == sangan &&
              hiddenSangan.PendingCardSelection is null &&
              hiddenSangan.DeckCount(0) == 1,
              "P188: face-down non-FLIP defender is banished with no false Graveyard effect");
    }
}
