using Joey.Core;

internal static class SpellTrapWipeChecks
{
    private static Card CardOfType(ushort id, int type = 22) =>
        new(id, id, $"Card {id}", "", (uint)type << 20);

    public static void Run(Action<bool, string> check)
    {
        var duster = CardOfType(SpellTrapWipeRules.HarpiesFeatherDuster);
        var storm = CardOfType(SpellTrapWipeRules.HeavyStorm);
        var trap = CardOfType(1056, 21);
        var equip = CardOfType(300);
        var field = CardOfType(332);
        var otherField = CardOfType(331);
        var warrior = new Card(77, 77, "Warrior", "", (15u << 20) | (4u << 25) | (100u << 9) | 80u);
        Card?[] units = [warrior, null, null, null, null];

        var dusterDuel = DemoDuel.FromPartialObservation(0, [equip],
            units, units, 8000, equipmentContextResolved: true);
        check(dusterDuel.Equip(0, 1, 0, out _) && dusterDuel.AttackValue(1, 0) == 1300,
            "Duster fixture: equipment affects a monster across the field");
        dusterDuel.Spells[0, 4] = trap;
        dusterDuel.SpellFaceDown[0, 4] = true;
        dusterDuel.FieldSpells[0] = field;
        dusterDuel.FieldSpells[1] = otherField;
        dusterDuel.FieldSpellFaceDown[1] = true;
        dusterDuel.Spells[1, 2] = trap;
        dusterDuel.NextTurn();
        // Set activation by player 1. Its own set field and face-up Trap survive.
        dusterDuel.Spells[1, 3] = duster;
        dusterDuel.SpellFaceDown[1, 3] = true;
        check(dusterDuel.ActivateSetSpell(3, out _) &&
              dusterDuel.Spells[0, 0] is null && dusterDuel.Spells[0, 4] is null &&
              !dusterDuel.SpellFaceDown[0, 4] && dusterDuel.FieldSpells[0] is null &&
              dusterDuel.EquipmentCards.Count == 0 && dusterDuel.AttackValue(1, 0) == 1000 &&
              dusterDuel.Spells[1, 2] == trap && dusterDuel.FieldSpells[1] == otherField &&
              dusterDuel.FieldSpellFaceDown[1] &&
              dusterDuel.Spells[1, 3] is null && !dusterDuel.SpellFaceDown[1, 3] &&
              dusterDuel.Graveyard(0).SequenceEqual([equip, trap, field]) &&
              dusterDuel.Graveyard(1).SequenceEqual([duster]) &&
              dusterDuel.Monsters[0, 0] == warrior && dusterDuel.Monsters[1, 0] == warrior,
            "Duster: clears only the opponent's Spell/Trap and field zones, including hidden cards and equipment");

        var stormDuel = DemoDuel.FromPartialObservation(0, [storm], units, units, 8000);
        stormDuel.FieldSpells[0] = field;
        stormDuel.FieldSpells[1] = otherField;
        stormDuel.FieldSpellFaceDown[1] = true;
        stormDuel.Spells[0, 0] = trap;
        stormDuel.SpellFaceDown[0, 0] = true;
        stormDuel.Spells[1, 4] = trap;
        check(stormDuel.CanActivateFromHand(0) && stormDuel.Play(0, out _) &&
              stormDuel.Hand.Count == 0 && stormDuel.Spells.Cast<Card?>().All(card => card is null) &&
              stormDuel.FieldSpells.All(card => card is null) &&
              stormDuel.FieldSpellFaceDown.All(hidden => !hidden) &&
              stormDuel.Graveyard(0).SequenceEqual([storm, trap, field]) &&
              stormDuel.Graveyard(1).SequenceEqual([trap, otherField]) &&
              stormDuel.Monsters[0, 0] == warrior && stormDuel.Monsters[1, 0] == warrior,
            "Heavy Storm: hand activation clears both fields without touching monsters");

        var lonely = DemoDuel.FromPartialObservation(0, [storm], units, units, 8000);
        check(!lonely.CanActivateFromHand(0) && !lonely.Play(0, out _) &&
              lonely.Hand.Single() == storm && lonely.SetFromHand(0, out _) &&
              !lonely.ActivateSetSpell(0, out _) && lonely.Spells[0, 0] == storm &&
              lonely.SpellFaceDown[0, 0] && lonely.Graveyard(0).Count == 0,
            "Heavy Storm: cannot count the card being activated as a destruction candidate");
        lonely.FieldSpells[0] = field;
        check(lonely.ActivateSetSpell(0, out _) &&
              lonely.Graveyard(0).SequenceEqual([storm, field]),
            "Heavy Storm: a Field Spell on the caster's side alone is enough");

        var cpu = DemoDuel.FromPartialObservation(1, [duster], units, units, 8000, side1Deck: [warrior]);
        cpu.FieldSpells[0] = field;
        check(cpu.Draw(out _) &&
              new CpuDuelController(1).Step(cpu) is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell } &&
              cpu.FieldSpells[0] is null && cpu.Graveyard(0).Single() == field &&
              cpu.Graveyard(1).Single() == duster,
            "Duster: opponent Field Spell alone is eligible for CPU activation");

        var onlyOwn = DemoDuel.FromPartialObservation(0, [duster], units, units, 8000);
        onlyOwn.FieldSpells[0] = field;
        check(!onlyOwn.CanActivateFromHand(0) && !onlyOwn.Play(0, out _) &&
              onlyOwn.Hand.Single() == duster && onlyOwn.FieldSpells[0] == field,
            "Duster: an occupied own field cannot satisfy its opponent-only condition");
    }
}
