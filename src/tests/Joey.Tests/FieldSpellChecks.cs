using Joey.Core;

internal static class FieldSpellChecks
{
    private static Card Sogen() => new(490, 332, "Sogen", "", 22u << 20);
    private static Card Warrior() =>
        new(147, 789, "Test Warrior", "", (15u << 20) | (4u << 25) | (80u << 9) | 80u);
    private static Card NonWarrior() =>
        new(148, 790, "Test Beast", "", (11u << 20) | (4u << 25) | (80u << 9) | 80u);
    private static Card Equipment() => new(614, 300, "Legendary Sword", "", 22u << 20);
    private static Card?[] Field(Card? monster = null) => [monster, null, null, null, null];

    private static DemoDuel Duel(int side, params Card[] hand) =>
        DemoDuel.FromPartialObservation(side, hand, Field(Warrior()), Field(NonWarrior()),
                                        8000, equipmentContextResolved: true);

    private static string State(DemoDuel duel) => string.Join(";",
        duel.Turn, duel.Phase, duel.Winner, string.Join(",", duel.Life),
        string.Join(",", duel.Hand.Select(c => c.Id)),
        string.Join(",", duel.FieldSpells.Select(c => c?.Id)),
        string.Join(",", duel.FieldSpellFaceDown),
        string.Join(",", duel.Spells.Cast<Card?>().Select(c => c?.Id)),
        string.Join(",", Enumerable.Range(0, 2)
            .Select(p => string.Join(",", duel.Graveyard(p).Select(c => c.Id)))));

    public static void Run(Action<bool, string> check)
    {
        check(FieldSpellRules.Supports(332) && !FieldSpellRules.Supports(335) &&
              !FieldSpellRules.Supports(0) && !FieldSpellRules.Supports(2332),
              "Field Spell: support requires the exact Sogen resource ID");
        check(FieldSpellRules.Bonus(332, 15) == new NativeAiAttributeDelta(200, 200) &&
              FieldSpellRules.Bonus(332, 11) == default &&
              FieldSpellRules.Bonus(0, 15) == default,
              "Sogen: only observed Warrior class 15 receives +200/+200 in this slice");

        foreach (int side in new[] { 0, 1 })
        {
            var hand = Duel(side, Sogen());
            check(hand.AttackValue(0, 0) == 800 && hand.DefenseValue(0, 0) == 800 &&
                  hand.AttackValue(1, 0) == 800, $"Sogen side {side}: baseline stats unchanged");
            check(hand.CanActivateFromHand(0) && hand.Play(0, out _) &&
                  hand.FieldSpells[side]?.Id == 332 && !hand.FieldSpellFaceDown[side] &&
                  hand.ActiveFieldSpell?.Id == 332 && hand.Hand.Count == 0 &&
                  hand.Graveyard(side).Count == 0 && hand.Spells.Cast<Card?>().All(c => c is null),
                  $"Sogen side {side}: activation occupies only the dedicated field zone");
            check(hand.AttackValue(0, 0) == 1000 && hand.DefenseValue(0, 0) == 1000 &&
                  hand.AttackValue(1, 0) == 800 && hand.DefenseValue(1, 0) == 800,
                  $"Sogen side {side}: observed Warrior bonus, unsupported Beast unchanged");

            hand.MonsterFaceDown[0, 0] = true;
            check(hand.AttackValue(0, 0) == 800 && hand.DefenseValue(0, 0) == 800,
                  "Sogen: face-down Warrior has no dynamic field modifier");
            hand.MonsterFaceDown[0, 0] = false;
            check(hand.AttackValue(0, 0) == 1000 && hand.RemoveFieldSpell(side) &&
                  hand.FieldSpells[side] is null && hand.ActiveFieldSpell is null &&
                  hand.Graveyard(side).Single().Id == 332 && hand.AttackValue(0, 0) == 800,
                  $"Sogen side {side}: removing field returns stats to base without mutation of monster");
            string removed = State(hand);
            check(!hand.RemoveFieldSpell(side) && State(hand) == removed,
                  $"Sogen side {side}: field removal is idempotent");

            var set = Duel(side, Sogen());
            check(set.CanSetFromHand(0) && set.SetFromHand(0, out _) &&
                  set.FieldSpells[side]?.Id == 332 && set.FieldSpellFaceDown[side] &&
                  set.ActiveFieldSpell is null && set.AttackValue(0, 0) == 800 &&
                  set.Graveyard(side).Count == 0,
                  $"Sogen side {side}: field set is separate and has no bonus");
            check(set.ActivateSetFieldSpell(out _) && !set.FieldSpellFaceDown[side] &&
                  set.ActiveFieldSpell?.Id == 332 && set.AttackValue(0, 0) == 1000,
                  $"Sogen side {side}: set field activates in Main Phase");
            string activated = State(set);
            check(!set.ActivateSetFieldSpell(out _) && State(set) == activated,
                  $"Sogen side {side}: active field cannot reactivate from set slot");

            var fullSpells = Duel(side, Sogen());
            for (int z = 0; z < 5; z++) fullSpells.Spells[side, z] = Equipment();
            check(fullSpells.CanActivateFromHand(0) && fullSpells.Play(0, out _) &&
                  fullSpells.ActiveFieldSpell?.Id == 332 &&
                  fullSpells.Spells.Cast<Card?>().Count(c => c?.Id == 300) == 5,
                  $"Sogen side {side}: the field zone is independent of the five spell slots");

            var fullSet = Duel(side, Sogen());
            for (int z = 0; z < 5; z++) fullSet.Spells[side, z] = Equipment();
            check(fullSet.CanSetFromHand(0) && fullSet.SetFromHand(0, out _) &&
                  fullSet.FieldSpellFaceDown[side] && fullSet.Hand.Count == 0,
                  $"Sogen side {side}: can set field while ordinary spell slots are full");
        }

        var two = Duel(0, Sogen(), Sogen());
        check(two.Play(0, out _) && two.AttackValue(0, 0) == 1000 &&
              two.Play(0, out _) && two.AttackValue(0, 0) == 1000 &&
              two.FieldSpells[0] is not null && two.Graveyard(0).Count == 1,
              "Sogen: playing a second field replaces own first, never stacking bonuses");

        var ownOccupied = Duel(0, Sogen(), Sogen());
        check(ownOccupied.SetFromHand(0, out _), "Sogen: prepare own field set");
        string before = State(ownOccupied);
        check(!ownOccupied.CanSetFromHand(0) && !ownOccupied.SetFromHand(0, out _) &&
              State(ownOccupied) == before,
              "Sogen: cannot overwrite own set field by setting another; preserves state");
        check(ownOccupied.Play(0, out _) && ownOccupied.Graveyard(0).Single().Id == 332 &&
              ownOccupied.FieldSpells[0] is not null && !ownOccupied.FieldSpellFaceDown[0],
              "Sogen: hand activation replaces own set field in its zone");

        // Both sides have a Warrior: a Field Spell affects both players, not just its owner.
        var twoWarriors = DemoDuel.FromPartialObservation(0, [Sogen()],
            Field(Warrior()), Field(Warrior()), 8000);
        check(twoWarriors.Play(0, out _) && twoWarriors.AttackValue(0, 0) == 1000 &&
              twoWarriors.AttackValue(1, 0) == 1000,
              "Sogen: active field gives the observed Warrior bonus to both duelists");

        var enemy = new DemoDuel([Warrior(), Warrior(), Warrior(), Sogen()]);
        check(enemy.Play(3, out _) && enemy.FieldSpells[0] is not null,
              "Sogen: first player activates field before opponent's turn");
        enemy.NextTurn();
        check(enemy.SetFromHand(3, out _) && enemy.FieldSpellFaceDown[1] &&
              enemy.FieldSpells[0] is not null && enemy.Graveyard(0).Count == 0 &&
              enemy.ActiveFieldSpell is not null,
              "Sogen: setting an opposing field does not destroy the existing active field");
        check(enemy.ActivateSetFieldSpell(out _) && enemy.FieldSpells[0] is null &&
              enemy.FieldSpells[1] is not null && enemy.Graveyard(0).Single().Id == 332 &&
              enemy.Graveyard(1).Count == 0 && enemy.ActiveFieldSpell?.Id == 332,
              "Sogen: activating opposing set field sends prior field to its owner's graveyard");

        var equip = DemoDuel.FromPartialObservation(0, [Sogen(), Equipment()],
            Field(Warrior()), Field(), 8000, equipmentContextResolved: true);
        check(equip.Play(0, out _) && equip.Equip(0, 0, 0, out _) &&
              equip.AttackValue(0, 0) == 1300 && equip.DefenseValue(0, 0) == 1300,
              "Sogen + Legendary Sword: field/equipment contributions combine before final clamp");
        check(equip.RemoveFieldSpell(0) && equip.AttackValue(0, 0) == 1100 &&
              equip.DefenseValue(0, 0) == 1100 &&
              equip.RemoveEquipment(0, 0) && equip.AttackValue(0, 0) == 800,
              "Sogen: removing each modifier recalculates attributes independently");

        var battle = DemoDuel.FromPartialObservation(1, [Sogen()],
            Field(Warrior()), Field(NonWarrior()), 8000);
        check(battle.Play(0, out _) && battle.BeginBattle(out _) &&
              battle.Attack(0, 0, out _) && battle.Life[1] == 7800 &&
              battle.Monsters[1, 0] is null && battle.Monsters[0, 0] is not null,
              "Sogen: a buffed Warrior changes actual battle damage in playable duel");

        var battleSet = Duel(1, Sogen());
        check(battleSet.SetFromHand(0, out _) && battleSet.BeginBattle(out _),
              "Sogen: setup of set field before battle phase");
        before = State(battleSet);
        check(!battleSet.ActivateSetFieldSpell(out _) && State(battleSet) == before,
              "Sogen: cannot activate a set field during Battle Phase");

        var invalid = Duel(0, Sogen());
        before = State(invalid);
        check(!invalid.RemoveFieldSpell(-1) && !invalid.RemoveFieldSpell(2) &&
              !invalid.ActivateSetFieldSpell(out _) && State(invalid) == before,
              "Sogen: invalid field removal and empty activation do not mutate state");

        var unsupported = Duel(0, new Card(491, 1999, "Unknown field", "", 22u << 20));
        before = State(unsupported);
        check(!unsupported.CanActivateFromHand(0) && !unsupported.Play(0, out _) &&
              State(unsupported) == before,
              "Sogen: other field spells remain explicitly unsupported");
    }
}
