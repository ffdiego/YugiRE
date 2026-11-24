using Joey.Core;

internal static class AttributeFieldSpellChecks
{
    // Resource ID, original Properties >>29 code, effect name.
    private static readonly (ushort Id, byte Attribute, string Name)[] Cases =
    [
        (1125, 5, "EARTH"), (1126, 3, "WATER"), (1127, 4, "FIRE"),
        (1128, 6, "WIND"), (1129, 1, "LIGHT"), (1130, 2, "DARK")
    ];

    private static Card Spell(ushort id) =>
        new(1400 + id, id, $"Test field {id}", "", 22u << 20);

    private static Card Warrior(byte attribute, int index = 147, int defense = 500) =>
        new(index, 712, $"Test attribute {attribute}", "",
            (15u << 20) | (4u << 25) | ((uint)attribute << 29) |
            (120u << 9) | (uint)(defense / 10));

    private static Card?[] Field(Card? monster = null) =>
        [monster, null, null, null, null];

    private static DemoDuel Duel(ushort id, byte attribute, int activeSide = 0) =>
        DemoDuel.FromPartialObservation(activeSide, [Spell(id)],
            Field(Warrior(attribute)), Field(Warrior(0, 148)),
            8000, equipmentContextResolved: true);

    private static string Snapshot(DemoDuel duel) => string.Join(";", duel.Turn, duel.Phase,
        duel.Winner, string.Join(",", duel.Life),
        string.Join(",", duel.Hand.Select(c => c.Id)),
        string.Join(",", duel.FieldSpells.Select(c => c?.Id)),
        string.Join(",", duel.FieldSpellFaceDown),
        string.Join(",", duel.Spells.Cast<Card?>().Select(c => c?.Id)),
        string.Join(",", Enumerable.Range(0, 2).Select(p =>
            string.Join(",", duel.Graveyard(p).Select(c => c.Id)))));

    public static void Run(Action<bool, string> check)
    {
        foreach (var (id, attribute, name) in Cases)
        {
            byte wrong = (byte)(attribute == 1 ? 2 : 1);
            check(FieldSpellRules.Supports(id) &&
                  FieldSpellRules.Bonus(id, 15, attribute) == new NativeAiAttributeDelta(500, -400) &&
                  FieldSpellRules.Bonus(id, 15, wrong) == default &&
                  FieldSpellRules.Bonus(id, 15) == default &&
                  FieldSpellRules.Bonus(id, 22, attribute) == default,
                  $"Attribute Field {id}: {name} only, +500/-400 for an actual monster");
            check(FieldSpellRules.EffectSummary(id).Contains(name) &&
                  FieldSpellRules.EffectSummary(id).Contains("400 DEF"),
                  $"Attribute Field {id}: presentation describes the actual rule");

            foreach (int side in new[] { 0, 1 })
            {
                var hand = Duel(id, attribute, side);
                check(hand.AttackValue(0, 0) == 1200 && hand.DefenseValue(0, 0) == 500,
                      $"Attribute Field {id} side {side}: base stats unchanged");
                check(hand.CanActivateFromHand(0) && hand.Play(0, out string message) &&
                      message.Contains(name) && hand.FieldSpells[side]?.Id == id &&
                      !hand.FieldSpellFaceDown[side] && hand.Hand.Count == 0 &&
                      hand.Graveyard(side).Count == 0 &&
                      hand.Spells.Cast<Card?>().All(c => c is null),
                      $"Attribute Field {id} side {side}: activate from hand in dedicated zone");
                check(hand.AttackValue(0, 0) == 1700 && hand.DefenseValue(0, 0) == 100 &&
                      hand.AttackValue(1, 0) == 1200 && hand.DefenseValue(1, 0) == 500,
                      $"Attribute Field {id} side {side}: buff applies globally to matching attribute");

                hand.MonsterFaceDown[0, 0] = true;
                check(hand.AttackValue(0, 0) == 1200 && hand.DefenseValue(0, 0) == 500,
                      $"Attribute Field {id} side {side}: face-down stats show no field bonus");
                hand.MonsterFaceDown[0, 0] = false;
                check(hand.RemoveFieldSpell(side) && hand.AttackValue(0, 0) == 1200 &&
                      hand.DefenseValue(0, 0) == 500 && hand.FieldSpells[side] is null &&
                      hand.Graveyard(side).Single().Id == id,
                      $"Attribute Field {id} side {side}: removing field recalculates stats");

                var set = Duel(id, attribute, side);
                check(set.CanSetFromHand(0) && set.SetFromHand(0, out _) &&
                      set.FieldSpells[side]?.Id == id && set.FieldSpellFaceDown[side] &&
                      set.ActiveFieldSpell is null && set.AttackValue(0, 0) == 1200,
                      $"Attribute Field {id} side {side}: set field does not activate effect");
                check(set.ActivateSetFieldSpell(out _) && set.AttackValue(0, 0) == 1700 &&
                      !set.FieldSpellFaceDown[side] && set.Graveyard(side).Count == 0,
                      $"Attribute Field {id} side {side}: set activation starts effect");
                string after = Snapshot(set);
                check(!set.ActivateSetFieldSpell(out _) && Snapshot(set) == after,
                      $"Attribute Field {id} side {side}: cannot activate field twice");

                var full = Duel(id, attribute, side);
                for (int zone = 0; zone < 5; zone++)
                    full.Spells[side, zone] = new Card(10 + zone, 300, "Busy", "", 22u << 20);
                check(full.CanActivateFromHand(0) && full.Play(0, out _) &&
                      full.AttackValue(0, 0) == 1700 &&
                      full.Spells.Cast<Card?>().Count(c => c?.Id == 300) == 5,
                      $"Attribute Field {id} side {side}: independent of five full spell slots");
            }
        }

        // A field on either side affects matching monsters on both sides.
        var global = DemoDuel.FromPartialObservation(0, [Spell(1125)],
            Field(Warrior(5)), Field(Warrior(5, 148)), 8000);
        check(global.Play(0, out _) && global.AttackValue(0, 0) == 1700 &&
              global.AttackValue(1, 0) == 1700 &&
              global.DefenseValue(0, 0) == 100 && global.DefenseValue(1, 0) == 100,
              "Attribute Field EARTH: both players receive exactly one bonus");

        // Field replacement changes both sides immediately and tracks the old owner.
        var earth = Spell(1125); var water = Spell(1126);
        var cross = DemoDuel.FromDecks(Enumerable.Repeat(earth, 40).ToArray(),
                                       Enumerable.Repeat(water, 40).ToArray());
        cross.Monsters[0, 0] = Warrior(5);
        cross.Monsters[1, 0] = Warrior(3, 148);
        check(cross.Play(0, out _) && cross.AttackValue(0, 0) == 1700 &&
              cross.AttackValue(1, 0) == 1200,
              "Attribute Field: EARTH active before opponent replaces it");
        cross.NextTurn();
        check(cross.Play(0, out _) && cross.Graveyard(0).Single().Id == 1125 &&
              cross.Graveyard(1).Count == 0 && cross.FieldSpells[0] is null &&
              cross.FieldSpells[1]?.Id == 1126 &&
              cross.AttackValue(0, 0) == 1200 && cross.AttackValue(1, 0) == 1700,
              "Attribute Field: WATER from opponent replaces EARTH without stacking");

        // Field + equip are accumulated before clamp, independently removable.
        var combo = DemoDuel.FromPartialObservation(0,
            [Spell(1130), new Card(614, 301, "Sword of Dark Destruction", "", 22u << 20)],
            Field(Warrior(2)), Field(), 8000, equipmentContextResolved: true);
        check(combo.Play(0, out _) && combo.AttackValue(0, 0) == 1700 &&
              combo.DefenseValue(0, 0) == 100 &&
              combo.Equip(0, 0, 0, out _) &&
              combo.AttackValue(0, 0) == 2100 && combo.DefenseValue(0, 0) == 0,
              "DARK field + equipment: +900 ATK/-600 DEF, clamped only after accumulation");
        check(combo.RemoveFieldSpell(0) && combo.AttackValue(0, 0) == 1600 &&
              combo.DefenseValue(0, 0) == 300 &&
              combo.RemoveEquipment(0, 0) && combo.AttackValue(0, 0) == 1200 &&
              combo.DefenseValue(0, 0) == 500,
              "DARK field removal leaves equipment contribution intact");

        var combat = DemoDuel.FromPartialObservation(1, [Spell(1125)],
            Field(Warrior(3)), Field(Warrior(5, 148)), 8000);
        check(combat.Play(0, out _) && combat.BeginBattle(out _) &&
              combat.Attack(0, 0, out _) && combat.Life[0] == 7500 &&
              combat.Monsters[0, 0] is null && combat.Monsters[1, 0] is not null,
              "EARTH field affects actual battle outcome via effective ATK");

        var cancelled = Duel(1125, 5, 1);
        check(cancelled.SetFromHand(0, out _) && cancelled.BeginBattle(out _),
              "Attribute Field: preparation for wrong-phase activation test");
        string before = Snapshot(cancelled);
        check(!cancelled.ActivateSetFieldSpell(out _) && Snapshot(cancelled) == before,
              "Attribute Field: set activation during Battle Phase is atomic");

        check(!FieldSpellRules.Supports(1131) &&
              FieldSpellRules.Bonus(1131, 15, 5) == default &&
              FieldSpellRules.EffectSummary(1131).Contains("not yet"),
              "Field attributes: unknown resources remain explicitly unsupported");
    }
}
