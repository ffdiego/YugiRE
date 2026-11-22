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

        RunTargeted(check, warrior, units, trap, equip, field);
    }

    private static void RunTargeted(Action<bool, string> check, Card warrior, Card?[] units,
        Card trap, Card equip, Card field)
    {
        var typhoon = CardOfType(SpellTrapTargetRules.MysticalSpaceTyphoon);
        var removeTrap = CardOfType(SpellTrapTargetRules.RemoveTrap);
        var duel = DemoDuel.FromPartialObservation(0, [equip, typhoon], units, units, 8000,
            equipmentContextResolved: true);
        check(duel.Equip(0, 1, 0, out _), "Targeted destruction fixture: attached equip");
        duel.FieldSpells[1] = field;
        duel.FieldSpellFaceDown[1] = true;
        duel.Spells[1, 4] = trap;
        check(duel.CanActivateFromHand(0) && duel.Play(0, out _) &&
              duel.PendingSpellTrapTarget is { Owner: 0, EligibleTargets.Count: 3 } &&
              duel.Hand.Single() == typhoon && !duel.EndTurn(out _) && !duel.SetFromHand(0, out _),
            "Typhoon: opens a blocking choice covering both sides and the field without spending the spell");
        check(duel.CancelSpellTrapTarget(out _) && duel.PendingSpellTrapTarget is null &&
              duel.Hand.Single() == typhoon && duel.Play(0, out _),
            "Typhoon: cancel preserves the spell and permits reopening the choice");
        duel.FieldSpells[1] = CardOfType(field.Id);
        check(!duel.ResolveSpellTrapTarget(1, SpellTrapTargetRules.FieldZone, out _) &&
              duel.Hand.Single() == typhoon && duel.Graveyard(1).Count == 0,
            "Typhoon: a different occurrence with the same ID cannot replace the selected target");
        duel.FieldSpells[1] = field;
        check(duel.ResolveSpellTrapTarget(1, SpellTrapTargetRules.FieldZone, out _) &&
              duel.FieldSpells[1] is null && !duel.FieldSpellFaceDown[1] &&
              duel.Graveyard(1).Single() == field && duel.Graveyard(0).Single() == typhoon &&
              duel.Spells[1, 4] == trap && duel.Spells[0, 0] == equip && duel.PendingSpellTrapTarget is null,
            "Typhoon: destroys just the chosen set card in the field zone");

        var ownEquip = DemoDuel.FromPartialObservation(0, [equip, typhoon], units, units, 8000,
            equipmentContextResolved: true);
        check(ownEquip.Equip(0, 1, 0, out _) && ownEquip.AttackValue(1, 0) == 1300 &&
              ownEquip.Play(0, out _) && ownEquip.ResolveSpellTrapTarget(0, 0, out _) &&
              ownEquip.EquipmentCards.Count == 0 && ownEquip.AttackValue(1, 0) == 1000 &&
              ownEquip.Graveyard(0).SequenceEqual([typhoon, equip]),
            "Typhoon: destroying its own side's equipped Spell clears the stat modifier");

        var set = DemoDuel.FromPartialObservation(0, [typhoon], units, units, 8000,
            equipmentContextResolved: true);
        set.Spells[1, 0] = trap;
        set.SpellFaceDown[1, 0] = true;
        check(set.SetFromHand(0, out _) && !set.ActivateSetSpell(0, out _),
            "Typhoon: a Quick-Play Spell cannot activate on the turn it was set");
        set.NextTurn();
        set.NextTurn();
        check(set.ActivateSetSpell(0, out _) &&
              set.PendingSpellTrapTarget is { FromSet: true, EligibleTargets.Count: 1 } &&
              !set.ResolveSpellTrapTarget(0, 0, out _) && set.ResolveSpellTrapTarget(1, 0, out _) &&
              set.Graveyard(0).Single() == typhoon && set.Graveyard(1).Single() == trap &&
              !set.SpellFaceDown[0, 0] && !set.SpellFaceDown[1, 0],
            "Typhoon: after the Set turn it can destroy an opposing hidden Trap but cannot target itself");

        var linked = DemoDuel.FromPartialObservation(0, [removeTrap], units, units, 8000);
        var board = CardOfType(DestinyBoardRules.BoardResourceId, 21);
        var message = CardOfType(1541);
        var hiddenPiece = CardOfType(1542);
        linked.Spells[0, 1] = trap;
        linked.SpellFaceDown[0, 1] = true;
        linked.Spells[1, 2] = message;
        linked.Spells[1, 3] = hiddenPiece;
        linked.SpellFaceDown[1, 3] = true;
        linked.FieldSpells[1] = field;
        check(!linked.CanActivateFromHand(0) && !linked.Play(0, out _),
            "Remove Trap: a set Trap and face-up Spells do not provide a target");
        linked.Spells[1, 0] = board;
        linked.Spells[0, 2] = trap;
        check(linked.SetFromHand(0, out _) && linked.ActivateSetSpell(0, out _) &&
              linked.PendingSpellTrapTarget is { FromSet: true, EligibleTargets.Count: 2 },
            "Remove Trap: a Normal Spell can activate the turn it was set and can choose either side");
        linked.SpellFaceDown[1, 0] = true;
        check(!linked.ResolveSpellTrapTarget(1, 0, out _) && linked.Spells[0, 0] == removeTrap,
            "Remove Trap: rechecks that the target is still face-up");
        linked.SpellFaceDown[1, 0] = false;
        check(linked.ResolveSpellTrapTarget(1, 0, out _) &&
              linked.Graveyard(0).Single() == removeTrap &&
              linked.Graveyard(1).SequenceEqual([board, message]) &&
              linked.Spells[1, 3] == hiddenPiece && linked.SpellFaceDown[1, 3] &&
              linked.Spells[0, 1] == trap && linked.Spells[0, 2] == trap && linked.FieldSpells[1] == field,
            "Remove Trap: destroying an active Destiny Board also destroys its side's face-up Spirit Messages");

        var cpuDuel = DemoDuel.FromPartialObservation(1, [typhoon], units, units, 8000, side1Deck: [warrior]);
        cpuDuel.Spells[0, 0] = trap;
        cpuDuel.SpellFaceDown[0, 0] = true;
        cpuDuel.FieldSpells[1] = field;
        var cpu = new CpuDuelController(1);
        check(cpuDuel.Draw(out _) &&
              cpu.Step(cpuDuel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
              CpuDuelController.PendingOwner(cpuDuel) == 1 && cpuDuel.Hand.Contains(typhoon),
            "Typhoon CPU: initiating the choice keeps the spell in hand and does not announce activation");
        check(cpu.Step(cpuDuel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell } &&
              cpuDuel.PendingSpellTrapTarget is null && cpuDuel.Graveyard(0).Single() == trap &&
              cpuDuel.Graveyard(1).Single() == typhoon && cpuDuel.FieldSpells[1] == field,
            "Typhoon CPU: resolves on the opponent's card instead of its own");
    }
}
