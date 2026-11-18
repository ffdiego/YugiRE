using Joey.Core;

internal static class TypeFieldSpellChecks
{
    private sealed record Case(ushort Id, string Name, int[] Boost, int[] Weaken);
    private static readonly Case[] Cases =
    [
        new(329, "Forest", [10, 11, 12, 13], []),
        new(330, "Wasteland", [2, 6, 9], []),
        new(331, "Mountain", [1, 16, 19], []),
        new(332, "Sogen", [12, 15], []),
        new(333, "Umi", [5, 8, 14, 19], [4, 7]),
        new(334, "Yami", [3, 18], [17])
    ];
    private static Card Spell(ushort id) =>
        new(1000 + id, id, $"Field {id}", "", 22u << 20);
    private static Card Monster(int type, int index = 90, int attack = 1200, int defense = 1000) =>
        new(index, (ushort)(100 + type), $"Type {type}", "",
            ((uint)type << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card?[] Slots(Card? card = null) => [card, null, null, null, null];
    private static DemoDuel Fixture(int active, ushort field, int type = 15) =>
        DemoDuel.FromPartialObservation(active, [Spell(field)],
            Slots(Monster(type)), Slots(Monster(type, 91)),
            8000, equipmentContextResolved: true);

    private static string Snapshot(DemoDuel d) => string.Join(";", d.Turn, d.Phase, d.Winner,
        string.Join(",", d.Life), string.Join(",", d.Hand.Select(c => c.Id)),
        string.Join(",", d.FieldSpells.Select(c => c?.Id)),
        string.Join(",", d.FieldSpellFaceDown),
        string.Join(",", d.Spells.Cast<Card?>().Select(c => c?.Id)),
        string.Join(",", Enumerable.Range(0, 2)
            .Select(p => string.Join(",", d.Graveyard(p).Select(c => c.Id)))));

    public static void Run(Action<bool, string> check)
    {
        foreach (var rule in Cases)
        {
            check(FieldSpellRules.Supports(rule.Id) &&
                  FieldSpellRules.EffectSummary(rule.Id).Length > 15,
                  $"{rule.Name}: P036 supported with visible effect description");

            // Exhaustively exercise the 20 native monster type codes for the
            // type-based fields, preventing accidental cross-type bonuses.
            bool typed = true;
            for (int type = 1; type <= 20; type++)
            {
                var expected = rule.Boost.Contains(type) ? new NativeAiAttributeDelta(200, 200) :
                    rule.Weaken.Contains(type) ? new NativeAiAttributeDelta(-200, -200) : default;
                typed &= FieldSpellRules.Bonus(rule.Id, type) == expected &&
                         FieldSpellRules.Bonus(rule.Id, type, 6) == expected;
            }
            typed &= FieldSpellRules.Bonus(rule.Id, 0) == default &&
                     FieldSpellRules.Bonus(rule.Id, 21) == default;
            check(typed, $"{rule.Name}: all monster type codes have exact signed effects");

            foreach (int side in new[] { 0, 1 })
            {
                int positiveType = rule.Boost[0];
                var fromHand = Fixture(side, rule.Id, positiveType);
                check(fromHand.CanActivateFromHand(0) && fromHand.Play(0, out _) &&
                      fromHand.ActiveFieldSpell?.Id == rule.Id &&
                      fromHand.FieldSpells[side]?.Id == rule.Id &&
                      fromHand.AttackValue(0, 0) == 1400 && fromHand.DefenseValue(0, 0) == 1200 &&
                      fromHand.AttackValue(1, 0) == 1400 && fromHand.DefenseValue(1, 0) == 1200 &&
                      fromHand.Hand.Count == 0 && fromHand.Graveyard(side).Count == 0,
                      $"{rule.Name}: activation globally modifies both players for side {side}");

                fromHand.MonsterFaceDown[1, 0] = true;
                check(fromHand.AttackValue(1, 0) == 1200 && fromHand.DefenseValue(1, 0) == 1000 &&
                      fromHand.AttackValue(0, 0) == 1400,
                      $"{rule.Name}: facedown card omits bonus on side {side}");
                fromHand.MonsterFaceDown[1, 0] = false;
                check(fromHand.RemoveFieldSpell(side) && fromHand.ActiveFieldSpell is null &&
                      fromHand.AttackValue(0, 0) == 1200 && fromHand.DefenseValue(1, 0) == 1000 &&
                      fromHand.Graveyard(side).Single().Id == rule.Id,
                      $"{rule.Name}: field removal restores base without duplicating cards");

                var fromSet = Fixture(side, rule.Id, positiveType);
                check(fromSet.CanSetFromHand(0) && fromSet.SetFromHand(0, out _) &&
                      fromSet.FieldSpellFaceDown[side] && fromSet.ActiveFieldSpell is null &&
                      fromSet.AttackValue(0, 0) == 1200 && fromSet.Graveyard(side).Count == 0,
                      $"{rule.Name}: set has no premature modifier");
                check(fromSet.ActivateSetFieldSpell(out _) &&
                      fromSet.ActiveFieldSpell?.Id == rule.Id && fromSet.AttackValue(0, 0) == 1400,
                      $"{rule.Name}: set field activates once");
                var afterSet = Snapshot(fromSet);
                check(!fromSet.ActivateSetFieldSpell(out _) && Snapshot(fromSet) == afterSet,
                      $"{rule.Name}: set field cannot resolve twice");

                var full = Fixture(side, rule.Id, positiveType);
                for (int z = 0; z < 5; z++)
                    full.Spells[side, z] = Spell(1130);
                check(full.CanActivateFromHand(0) && full.Play(0, out _) &&
                      full.Spells.Cast<Card?>().Count(c => c?.Id == 1130) == 5 &&
                      full.ActiveFieldSpell?.Id == rule.Id,
                      $"{rule.Name}: field slot works independently of full ordinary spell zones");
            }

            foreach (int type in rule.Weaken)
            {
                var negative = Fixture(0, rule.Id, type);
                negative.Monsters[0, 0] = Monster(type, attack: 100, defense: 100);
                check(negative.Play(0, out _) &&
                      negative.AttackValue(0, 0) == 0 && negative.DefenseValue(0, 0) == 0 &&
                      negative.Monsters[0, 0] is not null,
                      $"{rule.Name}: negative field modifier clamps ATK/DEF at zero");
            }
        }

        var beastWarrior = Fixture(0, 332, 12);
        check(beastWarrior.Play(0, out _) &&
              beastWarrior.AttackValue(0, 0) == 1400 &&
              beastWarrior.DefenseValue(1, 0) == 1200,
              "Sogen: Beast-Warrior type 12 gains +200/+200 using card-text rule");
        check(FieldSpellRules.Bonus(332, 11) == default &&
              FieldSpellRules.Bonus(329, 11) == new NativeAiAttributeDelta(200, 200),
              "Sogen/Forest: Beast type 11 differs from Beast-Warrior 12");

        var change = DemoDuel.FromPartialObservation(0, [Spell(329), Spell(334)],
            Slots(Monster(10)), Slots(Monster(18, 91)), 8000);
        check(change.Play(0, out _) && change.AttackValue(0, 0) == 1400 &&
              change.AttackValue(1, 0) == 1200 && change.Play(0, out _) &&
              change.ActiveFieldSpell?.Id == 334 &&
              change.AttackValue(0, 0) == 1200 && change.AttackValue(1, 0) == 1400 &&
              change.Graveyard(0).Single().Id == 329,
              "Forest -> Yami: immediate replacement with no additive ghost buff");

        var opponent = DemoDuel.FromDecks(
            Enumerable.Repeat(Spell(333), 40).ToArray(),
            Enumerable.Repeat(Spell(334), 40).ToArray());
        opponent.Monsters[0, 0] = Monster(7);
        opponent.Monsters[1, 0] = Monster(17, 91);
        check(opponent.Play(0, out _) && opponent.AttackValue(0, 0) == 1000,
              "Umi: first field starts with Machine penalty");
        opponent.NextTurn();
        check(opponent.Play(0, out _) &&
              opponent.AttackValue(0, 0) == 1200 && opponent.AttackValue(1, 0) == 1000 &&
              opponent.Graveyard(0).Single().Id == 333 &&
              opponent.FieldSpells[0] is null && opponent.FieldSpells[1]?.Id == 334,
              "Umi -> opponent Yami: previous owner receives card and stats recalculate");

        // Chorus affects DEF only in Defense Position and stays global.
        check(FieldSpellRules.Supports(1069) &&
              FieldSpellRules.Bonus(1069, 15) == default &&
              FieldSpellRules.Bonus(1069, 15, defensePosition: true) ==
                  new NativeAiAttributeDelta(0, 500),
              "Chorus: only Defense Position gets +500 DEF");
        foreach (int side in new[] { 0, 1 })
        {
            var chorus = Fixture(side, 1069);
            chorus.DefensePosition[0, 0] = true;
            chorus.DefensePosition[1, 0] = true;
            check(chorus.Play(0, out _) &&
                  chorus.AttackValue(0, 0) == 1200 &&
                  chorus.DefenseValue(0, 0) == 1500 &&
                  chorus.DefenseValue(1, 0) == 1500,
                  $"Chorus side {side}: both defenders get +500 DEF");
            chorus.DefensePosition[0, 0] = false;
            check(chorus.DefenseValue(0, 0) == 1000 &&
                  chorus.DefenseValue(1, 0) == 1500 &&
                  chorus.RemoveFieldSpell(side) && chorus.DefenseValue(1, 0) == 1000,
                  $"Chorus side {side}: position/removal adjusts DEF dynamically");
        }

        var combat = DemoDuel.FromPartialObservation(1, [Spell(1069)],
            Slots(Monster(11, attack: 500, defense: 1000)),
            Slots(Monster(15, 91, attack: 1200, defense: 1000)), 8000);
        combat.DefensePosition[0, 0] = true;
        check(combat.Play(0, out _) && combat.BeginBattle(out _) &&
              combat.Attack(0, 0, out _) && combat.Life[1] == 7700 &&
              combat.Monsters[0, 0] is not null &&
              combat.Monsters[1, 0] is not null,
              "Chorus: 1200 ATK vs 1500 DEF inflicts 300 to attacker without destruction");

        // An already set field cannot activate in Battle and does not mutate.
        var phase = Fixture(1, 329, 10);
        check(phase.SetFromHand(0, out _) && phase.BeginBattle(out _),
              "Forest: test fixture enters Battle with field set");
        var prior = Snapshot(phase);
        check(!phase.ActivateSetFieldSpell(out _) && Snapshot(phase) == prior,
              "Forest: set activation disallowed outside Main Phase");

        check(!FieldSpellRules.Supports(335) &&
              !FieldSpellRules.Supports(1131) &&
              FieldSpellRules.Bonus(335, 15) == default &&
              FieldSpellRules.Bonus(1069, 22, defensePosition: true) == default,
              "Field Spells: unsupported resource and non-monsters are not accepted");
    }
}
