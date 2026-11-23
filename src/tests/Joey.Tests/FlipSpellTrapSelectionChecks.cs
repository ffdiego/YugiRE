using Joey.Core;

internal static class FlipSpellTrapSelectionChecks
{
    private static Card Spell(ushort id, int index, bool trap = false) =>
        new(index, id, $"S/T {id}", "", (uint)(trap ? 21 : 22) << 20);
    private static Card Monster(ushort id, int index, int type = 15, int atk = 1000, int def = 800) =>
        new(index, id, id == 585 ? "Greenkappa" : id == 1521 ? "Tornado Bird" : $"Monster {id}",
            "", ((uint)type << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card?[] Slots(Card? a = null, Card? b = null) =>
        [a, b, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var kappa = Monster(585, 638);
        var bird = Monster(1521, 660);
        var trapA = Spell(700, 1700, trap: true);
        var trapB = Spell(701, 1701, trap: true);
        var spellC = Spell(702, 1702);
        var field = Spell(332, 1703);

        check(FlipEffectRules.Effect(kappa.Id) == DemoFlipEffect.DestroyTwoSetSpellTraps &&
              FlipEffectRules.Effect(bird.Id) == DemoFlipEffect.ReturnTwoSpellTraps &&
              FlipSpellTrapSelectionRules.IsEligible(585, trapA, true) &&
              !FlipSpellTrapSelectionRules.IsEligible(585, trapA, false) &&
              FlipSpellTrapSelectionRules.IsEligible(1521, trapA, false) &&
              FlipSpellTrapSelectionRules.IsEligible(1521, spellC, true) &&
              !FlipSpellTrapSelectionRules.IsEligible(900, trapA, true),
              "P085/P255: IDs dispatch to distinct operations and filters");

        var k = DemoDuel.FromPartialObservation(0, [], Slots(kappa), Slots(), 8000);
        k.MonsterFaceDown[0, 0] = k.DefensePosition[0, 0] = true;
        k.Spells[0, 1] = trapA;
        k.SpellFaceDown[0, 1] = true;
        k.Spells[1, 2] = trapB;
        k.SpellFaceDown[1, 2] = true;
        k.Spells[1, 3] = spellC; // Face-up: not eligible for Greenkappa.
        check(k.ChangePosition(0, out _) &&
              k.PendingFlipSpellTrapTargets is { Owner: 0, Operation: FlipSpellTrapOperation.Destroy,
                  RequireFaceDown: true, EligibleTargets.Count: 2 } &&
              k.PendingFlipSpellTrapTargets.EligibleTargets
                  .Select(t => (t.Player, t.Zone)).SequenceEqual([(0, 1), (1, 2)]) &&
              !k.EndTurn(out _) && k.Monsters[0, 0] == kappa &&
              k.Graveyard(0).Count == 0 && k.Graveyard(1).Count == 0,
              "Greenkappa: FLIP opens mandatory two-target request for both sides, only Set S/T");
        check(!k.SelectFlipSpellTrapTarget(1, 3, out _) &&
              k.SelectFlipSpellTrapTarget(0, 1, out _) &&
              k.SelectedFlipSpellTrapTargets.Count == 1 &&
              k.Spells[0, 1] == trapA && k.Graveyard(0).Count == 0 &&
              !k.EndTurn(out _),
              "Greenkappa: cannot choose face-up card; first selection is nonmutating");
        check(k.SelectFlipSpellTrapTarget(1, 2, out _) &&
              k.PendingFlipSpellTrapTargets is null &&
              k.SelectedFlipSpellTrapTargets.Count == 0 &&
              k.Spells[0, 1] is null && k.Spells[1, 2] is null &&
              !k.SpellFaceDown[0, 1] && !k.SpellFaceDown[1, 2] &&
              k.Spells[1, 3] == spellC &&
              k.Graveyard(0).SequenceEqual([trapA]) &&
              k.Graveyard(1).SequenceEqual([trapB]),
              "Greenkappa: two Set targets are destroyed atomically; other cards survive");

        var one = DemoDuel.FromPartialObservation(0, [], Slots(kappa), Slots(), 8000);
        one.MonsterFaceDown[0, 0] = one.DefensePosition[0, 0] = true;
        one.Spells[1, 0] = trapA;
        one.SpellFaceDown[1, 0] = true;
        one.Spells[1, 1] = trapB; // Face-up is ineligible.
        check(one.ChangePosition(0, out _) &&
              one.PendingFlipSpellTrapTargets is null &&
              one.Spells[1, 0] == trapA && one.Spells[1, 1] == trapB &&
              one.Graveyard(1).Count == 0,
              "Greenkappa: fewer than two Set S/T never destroys the solitary Set card");

        var toggle = DemoDuel.FromPartialObservation(0, [], Slots(kappa), Slots(), 8000);
        toggle.MonsterFaceDown[0, 0] = toggle.DefensePosition[0, 0] = true;
        toggle.Spells[1, 0] = trapA;
        toggle.SpellFaceDown[1, 0] = true;
        toggle.FieldSpells[0] = field;
        toggle.FieldSpellFaceDown[0] = true;
        check(toggle.ChangePosition(0, out _) &&
              toggle.PendingFlipSpellTrapTargets is { EligibleTargets.Count: 2 } &&
              toggle.SelectFlipSpellTrapTarget(1, 0, out _) &&
              toggle.SelectFlipSpellTrapTarget(1, 0, out _) &&
              toggle.SelectedFlipSpellTrapTargets.Count == 0 &&
              toggle.SelectFlipSpellTrapTarget(0, 5, out _) &&
              toggle.SelectFlipSpellTrapTarget(1, 0, out _) &&
              toggle.FieldSpells[0] is null &&
              toggle.Graveyard(0).SequenceEqual([field]) &&
              toggle.Graveyard(1).SequenceEqual([trapA]),
              "Greenkappa: duplicate toggles off and a set Field Spell is a valid target");

        var b = DemoDuel.FromPartialObservation(0, [], Slots(bird), Slots(), 8000);
        b.MonsterFaceDown[0, 0] = b.DefensePosition[0, 0] = true;
        b.Spells[0, 1] = trapA;
        b.SpellFaceDown[0, 1] = true;
        b.Spells[1, 2] = spellC;
        check(b.ChangePosition(0, out _) &&
              b.PendingFlipSpellTrapTargets is {
                  Owner: 0, Operation: FlipSpellTrapOperation.ReturnToOwnerHand,
                  RequireFaceDown: false, EligibleTargets.Count: 2 } &&
              b.SelectFlipSpellTrapTarget(0, 1, out _) &&
              b.SelectFlipSpellTrapTarget(1, 2, out _) &&
              b.Spells[0, 1] is null && b.Spells[1, 2] is null &&
              b.HandFor(0).SequenceEqual([trapA]) &&
              b.HandFor(1).SequenceEqual([spellC]) &&
              b.Graveyard(0).Count == 0 && b.Graveyard(1).Count == 0,
              "Tornado Bird: Set and face-up S/T return to original owners, not Graveyard");

        var oneBird = DemoDuel.FromPartialObservation(0, [], Slots(bird), Slots(), 8000);
        oneBird.MonsterFaceDown[0, 0] = oneBird.DefensePosition[0, 0] = true;
        oneBird.Spells[1, 1] = trapA;
        check(oneBird.ChangePosition(0, out _) &&
              oneBird.PendingFlipSpellTrapTargets is null && oneBird.HandFor(1).Count == 0 &&
              oneBird.Spells[1, 1] == trapA,
              "Tornado Bird: fewer than two S/T does not open a partial selection");

        var warrior = Monster(400, 1810, type: 15);
        var sword = Spell(300, 1811);
        var equipment = DemoDuel.FromPartialObservation(0, [sword],
            Slots(bird, warrior), Slots(), 8000, equipmentContextResolved: true);
        equipment.MonsterFaceDown[0, 0] = equipment.DefensePosition[0, 0] = true;
        check(equipment.Equip(0, 0, 1, out _) && equipment.EquipmentCards.Count == 1 &&
              equipment.AttackValue(0, 1) == 1300,
              "Tornado Bird: equipped Legendary Sword fixture applies ATK bonus");
        equipment.FieldSpells[1] = field;
        check(equipment.ChangePosition(0, out _) &&
              equipment.PendingFlipSpellTrapTargets is { EligibleTargets.Count: 2 } &&
              equipment.SelectFlipSpellTrapTarget(0, 0, out _) &&
              equipment.SelectFlipSpellTrapTarget(1, SpellTrapTargetRules.FieldZone, out _) &&
              equipment.EquipmentCards.Count == 0 &&
              equipment.AttackValue(0, 1) == 1000 &&
              equipment.HandFor(0).Contains(sword) &&
              equipment.HandFor(1).Contains(field) &&
              equipment.Graveyard(0).Count == 0 && equipment.Graveyard(1).Count == 0 &&
              equipment.FieldSpells[1] is null,
              "Tornado Bird: bounced equip detaches without going to Graveyard; Field Spell returns");

        var stale = DemoDuel.FromPartialObservation(0, [], Slots(kappa), Slots(), 8000);
        stale.MonsterFaceDown[0, 0] = stale.DefensePosition[0, 0] = true;
        stale.Spells[1, 0] = trapA; stale.SpellFaceDown[1, 0] = true;
        stale.Spells[1, 1] = trapB; stale.SpellFaceDown[1, 1] = true;
        check(stale.ChangePosition(0, out _) &&
              stale.SelectFlipSpellTrapTarget(1, 0, out _),
              "Selection: first target records card occurrence without moving it");
        stale.Spells[1, 0] = Spell(trapA.Id, 9900, trap: true); // Same ID but new object.
        check(!stale.SelectFlipSpellTrapTarget(1, 1, out _) &&
              stale.PendingFlipSpellTrapTargets is not null &&
              stale.SelectedFlipSpellTrapTargets.Count == 1 &&
              stale.Graveyard(1).Count == 0 &&
              stale.Spells[1, 1] == trapB &&
              stale.SelectFlipSpellTrapTarget(1, 0, out _) &&
              stale.SelectedFlipSpellTrapTargets.Count == 0,
              "Selection: replacement invalidates first target, no partial destruction");

        var ai = DemoDuel.FromPartialObservation(0, [], Slots(bird), Slots(), 8000);
        ai.MonsterFaceDown[0, 0] = ai.DefensePosition[0, 0] = true;
        ai.Spells[0, 2] = trapA;
        ai.Spells[1, 3] = trapB;
        check(ai.ChangePosition(0, out _) &&
              new CpuDuelController(0).Step(ai) is {
                  Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
              ai.SelectedFlipSpellTrapTargets.Count == 1 &&
              new CpuDuelController(0).Step(ai) is {
                  Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
              ai.PendingFlipSpellTrapTargets is null &&
              ai.HandFor(0).Contains(trapA) &&
              ai.HandFor(1).Contains(trapB),
              "CPU: chooses exactly two S/T for Tornado Bird, including own when needed");

        var attacker = Monster(50, 1850, type: 1, atk: 2400);
        var battle = DemoDuel.FromPartialObservation(1, [], Slots(kappa),
            Slots(attacker), 8000);
        battle.MonsterFaceDown[0, 0] = battle.DefensePosition[0, 0] = true;
        battle.Spells[1, 1] = trapA;
        battle.SpellFaceDown[1, 1] = true;
        battle.Spells[1, 2] = trapB;
        battle.SpellFaceDown[1, 2] = true;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingFlipSpellTrapTargets is { Owner: 0 } &&
              battle.IsMarkedForBattleDestruction(0, 0) &&
              battle.Graveyard(0).Count == 0 &&
              battle.SelectFlipSpellTrapTarget(1, 1, out _) &&
              battle.SelectFlipSpellTrapTarget(1, 2, out _) &&
              battle.Graveyard(0).SequenceEqual([kappa]) &&
              battle.Graveyard(1).SequenceEqual([trapA, trapB]) &&
              battle.Monsters[0, 0] is null &&
              battle.PendingFlipSpellTrapTargets is null,
              "Greenkappa: combat reveal destroys two S/T before marking defender to Graveyard");

        var external = DemoDuel.FromPartialObservation(0, [], Slots(bird), Slots(), 8000);
        external.MonsterFaceDown[0, 0] = external.DefensePosition[0, 0] = true;
        external.Spells[0, 0] = trapA; external.Spells[0, 1] = trapB;
        check(external.RevealMonsterByEffect(0, 0, out _) &&
              external.DefensePosition[0, 0] && !external.MonsterFaceDown[0, 0] &&
              external.PendingFlipSpellTrapTargets is not null &&
              external.SelectFlipSpellTrapTarget(0, 0, out _) &&
              external.SelectFlipSpellTrapTarget(0, 1, out _) &&
              external.HandFor(0).SequenceEqual([trapA, trapB]),
              "Tornado Bird: external revelation follows the existing FLIP pipeline");
    }
}
