using Joey.Core;

internal static class SummonTrapChecks
{
    private static Card Monster(ushort id, int index, int atk = 1200, int def = 800, int type = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)type << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Trap(ushort id, int index, int cls = 21) =>
        new(index, id, id == 1002 ? "Trap Hole" : "Torrential Tribute", "", (uint)cls << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null) =>
        [a, b, null, null, null];
    private static void PreSet(DemoDuel duel, int owner, int zone, Card trap)
    {
        duel.Spells[owner, zone] = trap;
        duel.SpellFaceDown[owner, zone] = true;
    }

    public static void Run(Action<bool, string> check)
    {
        var hole = Trap(1002, 120);
        var torrential = Trap(1425, 272);
        var attacker = Monster(250, 600, 1900, 1300);
        var threshold = Monster(251, 601, 1000, 800);
        var weak = Monster(252, 602, 990, 700);
        var sangan = Monster(47, 248, 1000, 600);
        var witch = Monster(573, 261, 1100, 1200);
        var recruit = Monster(1123, 900, 1400, 1100);
        var lowAtk = Monster(101, 700, 1500, 1800);
        var lowDef = Monster(102, 701, 1800, 1500);
        var sonic = Monster(1122, 497, 1400, 1000);
        var ritual = new Card(710, 669, "Ritual Spell", "", 22u << 20);

        check(SummonTrapRules.IsSupported(1002) &&
              SummonTrapRules.IsSupported(1425) &&
              !SummonTrapRules.IsSupported(335) &&
              MonsterWipeRules.Scope(1425) == MonsterWipeScope.BothPlayers &&
              !MonsterWipeRules.IsNormalSpell(1425) &&
              SummonTrapRules.CanActivate(hole, 0,
                  new DemoSummonEvent(1, 0, threshold, SummonTriggerKind.Normal), 1000) &&
              !SummonTrapRules.CanActivate(hole, 0,
                  new DemoSummonEvent(1, 0, weak, SummonTriggerKind.Normal), 990) &&
              !SummonTrapRules.CanActivate(hole, 1,
                  new DemoSummonEvent(1, 0, attacker, SummonTriggerKind.Normal), 1900) &&
              !SummonTrapRules.CanActivate(hole, 0,
                  new DemoSummonEvent(1, 0, attacker, SummonTriggerKind.Special), 1900) &&
              SummonTrapRules.CanActivate(torrential, 1,
                  new DemoSummonEvent(1, 0, attacker, SummonTriggerKind.Normal), 1900) &&
              SummonTrapRules.CanActivate(torrential, 0,
                  new DemoSummonEvent(1, 0, attacker, SummonTriggerKind.Special), 1900) &&
              !SummonTrapRules.CanActivate(Trap(1002, 150, cls: 22), 0,
                  new DemoSummonEvent(1, 0, attacker, SummonTriggerKind.Normal), 1900),
              "P196/P038: correct IDs, summon-kind limits, owner gate, inclusive 1000 ATK and trap DAT class");

        // Active side 1 (Turn 2), side 0 starts with a pre-set Trap Hole.
        var simple = DemoDuel.FromPartialObservation(1, [threshold], Slots(), Slots(), 8000);
        PreSet(simple, 0, 2, hole);
        check(simple.SummonLeftmost(0, false, out _) &&
              simple.PendingSummonResponse is { Summon: { Summoner: 1, Kind: SummonTriggerKind.Normal,
                                                         Zone: 0, Monster: var summoned } } &&
              ReferenceEquals(summoned, threshold) &&
              simple.PendingSummonResponse.Options.Count == 1 &&
              simple.PendingSummonResponse.Options.Single() is { Owner: 0, SpellZone: 2 } &&
              !simple.EndTurn(out _) && !simple.Draw(out _) &&
              !simple.CanSummonFromHand(0),
              "Trap Hole: opponent's Normal Summon opens explicit reaction and blocks other actions");

        check(!simple.ActivateSummonTrap(1, 2, out _) &&
              !simple.ActivateSummonTrap(0, 3, out _) &&
              !simple.ActivateSetSpell(2, out _) &&
              simple.Spells[0, 2] == hole &&
              simple.Monsters[1, 0] == threshold,
              "Trap response: unrelated owner/slot and ordinary main-phase activation are rejected atomically");
        check(simple.ActivateSummonTrap(0, 2, out _) &&
              simple.PendingSummonResponse is null &&
              simple.Spells[0, 2] is null && !simple.SpellFaceDown[0, 2] &&
              simple.Graveyard(0).Single() == hole &&
              simple.Graveyard(1).Single() == threshold &&
              simple.Monsters[1, 0] is null &&
              simple.LastMonsterWipe is null &&
              !simple.ActivateSummonTrap(0, 2, out _) &&
              !simple.PassSummonResponse(out _),
              "Trap Hole: consumes one Set Trap and destroys only the summoned occurrence");

        var low = DemoDuel.FromPartialObservation(1, [weak], Slots(), Slots(), 8000);
        PreSet(low, 0, 0, hole);
        check(low.SummonLeftmost(0, false, out _) &&
              low.PendingSummonResponse is null && low.Monsters[1, 0] == weak &&
              low.Spells[0, 0] == hole,
              "Trap Hole: 990 ATK summons do not open reaction windows");

        var boosted = DemoDuel.FromPartialObservation(1, [weak], Slots(), Slots(), 8000);
        boosted.FieldSpells[0] = new Card(800, 332, "Sogen", "", 22u << 20);
        PreSet(boosted, 0, 0, hole);
        // The summoned monster is a Warrior: current ATK includes Sogen +200.
        var warrior = Monster(253, 603, 990, 700, type: 15);
        var buff = DemoDuel.FromPartialObservation(1, [warrior], Slots(), Slots(), 8000);
        buff.FieldSpells[0] = boosted.FieldSpells[0];
        PreSet(buff, 0, 0, hole);
        check(buff.SummonLeftmost(0, false, out _) &&
              buff.AttackValue(1, 0) == 1190 &&
              buff.PendingSummonResponse is { Options.Count: 1 },
              "Trap Hole: qualifying ATK uses visible field-modified value, not printed base alone");

        var passed = DemoDuel.FromPartialObservation(1, [attacker], Slots(), Slots(), 8000);
        PreSet(passed, 0, 0, hole);
        PreSet(passed, 0, 1, torrential);
        check(passed.SummonLeftmost(0, false, out _) &&
              passed.PendingSummonResponse is { Options.Count: 2 } &&
              passed.PassSummonResponse(out _) &&
              passed.PendingSummonResponse is null &&
              passed.Monsters[1, 0] == attacker &&
              passed.Spells[0, 0] == hole && passed.Spells[0, 1] == torrential &&
              !passed.ActivateSummonTrap(0, 0, out _),
              "Multiple Set Traps: player may pass the one bounded response window without spending cards");

        var wave = DemoDuel.FromPartialObservation(1, [threshold],
            Slots(sangan, recruit), Slots(witch), 8000,
            side0Deck: [lowAtk], side1Deck: [lowDef]);
        PreSet(wave, 0, 0, torrential);
        check(wave.SummonLeftmost(0, false, out _) &&
              wave.PendingSummonResponse is { Summon: { Summoner: 1 } } &&
              wave.ActivateSummonTrap(0, 0, out _) &&
              wave.LastMonsterWipe is { EffectResourceId: 1425,
                  DestroyedPlayer0: 2, DestroyedPlayer1: 2,
                  TotalDestroyed: 4 } &&
              Enumerable.Range(0, 5).All(z => wave.Monsters[0, z] is null &&
                  wave.Monsters[1, z] is null) &&
              wave.Graveyard(0).SequenceEqual([torrential, sangan, recruit]) &&
              wave.Graveyard(1).SequenceEqual([witch, threshold]) &&
              wave.PendingCardSelection is { Owner: 0, EffectResourceId: 47,
                  Filter: CardSelectionFilter.MonsterAttackAtMost1500 },
              "Torrential: destroys all four monsters and defers Sangan/Witch searches until wipe finishes; no Tomato recruit");
        check(wave.ResolveCardSelection(0, out _) &&
              wave.PendingCardSelection is { Owner: 1, EffectResourceId: 573 } &&
              wave.ResolveCardSelection(0, out _) &&
              wave.PendingCardSelection is null &&
              wave.HandFor(0).Single() == lowAtk &&
              wave.HandFor(1).Single() == lowDef &&
              wave.Spells[0, 0] is null &&
              wave.DeckCount(0) == 0 && wave.DeckCount(1) == 0,
              "Torrential: post-wipe field-to-GY searches resolve once per owner");

        // A Set Trap cannot be used in its own placement turn even when
        // Torrential would otherwise react to its owner's own summon.
        var young = DemoDuel.FromPartialObservation(0, [torrential, attacker, weak],
            Slots(), Slots(), 8000);
        check(young.SetFromHand(0, out _) &&
              young.Spells[0, 0] == torrential &&
              young.SummonLeftmost(0, false, out _) &&
              young.PendingSummonResponse is null &&
              young.Monsters[0, 0] == attacker && young.Spells[0, 0] == torrential,
              "Torrential: a Trap just set this turn is not eligible for response");
        check(young.EndTurn(out _) && young.EndTurn(out _) &&
              young.ActivePlayer == 0 && young.Turn == 3 &&
              young.SummonLeftmost(0, false, out _) &&
              young.PendingSummonResponse is { Options.Count: 1 } &&
              young.PendingSummonResponse.Options.Single().Owner == 0 &&
              young.ActivateSummonTrap(0, 0, out _) &&
              young.LastMonsterWipe is { TotalDestroyed: 2 } &&
              young.Monsters[0, 0] is null && young.Monsters[0, 1] is null &&
              young.Graveyard(0).SequenceEqual([torrential, attacker, weak]),
              "Torrential: after one turn transition, can react to own Normal Summon and wipe own field");

        var flipped = DemoDuel.FromPartialObservation(1, [], Slots(), Slots(threshold), 8000);
        flipped.MonsterFaceDown[1, 0] = flipped.DefensePosition[1, 0] = true;
        PreSet(flipped, 0, 0, hole);
        check(flipped.ChangePosition(0, out _) &&
              !flipped.MonsterFaceDown[1, 0] &&
              flipped.PendingSummonResponse is { Summon.Kind: SummonTriggerKind.Flip } &&
              flipped.ActivateSummonTrap(0, 0, out _) &&
              flipped.Monsters[1, 0] is null &&
              flipped.Graveyard(1).Single() == threshold,
              "Trap Hole: voluntary Flip Summon triggers response with the same ATK threshold");

        var onlyOwnHole = DemoDuel.FromPartialObservation(1, [attacker],
            Slots(), Slots(), 8000);
        PreSet(onlyOwnHole, 1, 0, hole);
        check(onlyOwnHole.SummonLeftmost(0, false, out _) &&
              onlyOwnHole.PendingSummonResponse is null &&
              onlyOwnHole.Spells[1, 0] == hole,
              "Trap Hole: cannot respond to its owner's summon");

        var revealed = DemoDuel.FromPartialObservation(1, [], Slots(), Slots(threshold), 8000);
        revealed.MonsterFaceDown[1, 0] = true;
        PreSet(revealed, 0, 0, hole);
        PreSet(revealed, 0, 1, torrential);
        check(revealed.RevealMonsterByEffect(1, 0, out _) &&
              revealed.PendingSummonResponse is null &&
              revealed.Spells[0, 0] == hole && revealed.Spells[0, 1] == torrential,
              "Trap response: an external reveal is NOT a Flip Summon");

        var sonicDuel = DemoDuel.FromPartialObservation(1, [sonic],
            Slots(), Slots(), 8000, side1Deck: [ritual]);
        PreSet(sonicDuel, 0, 0, hole);
        check(sonicDuel.SummonLeftmost(0, false, out _) &&
              sonicDuel.PendingSummonResponse is not null &&
              sonicDuel.PendingCardSelection is null &&
              sonicDuel.PassSummonResponse(out _) &&
              sonicDuel.PendingCardSelection is { Owner: 1, EffectResourceId: 1122,
                  Destination: CardSelectionDestination.Hand } &&
              sonicDuel.ResolveCardSelection(0, out _) &&
              sonicDuel.HandFor(1).Single() == ritual,
              "On-summon Sonic Bird search waits for response Pass before opening deck selection");

        var killedSonic = DemoDuel.FromPartialObservation(1, [sonic],
            Slots(), Slots(), 8000, side1Deck: [ritual]);
        PreSet(killedSonic, 0, 0, hole);
        check(killedSonic.SummonLeftmost(0, false, out _) &&
              killedSonic.ActivateSummonTrap(0, 0, out _) &&
              killedSonic.Monsters[1, 0] is null &&
              killedSonic.PendingCardSelection is { Owner: 1, EffectResourceId: 1122 } &&
              killedSonic.ResolveCardSelection(0, out _) &&
              killedSonic.Graveyard(1).Single() == sonic &&
              killedSonic.HandFor(1).Single() == ritual,
              "P165 interaction: on-summon search remains queued when Trap Hole destroys Sonic Bird (sandbox timing)");

        var noMainActivation = DemoDuel.FromPartialObservation(0,
            [hole, torrential], Slots(), Slots(attacker), 8000);
        check(!noMainActivation.CanActivateFromHand(0) &&
              !noMainActivation.CanActivateFromHand(1) &&
              !noMainActivation.Play(0, out _) &&
              !noMainActivation.Play(1, out _) &&
              noMainActivation.HandFor(0).SequenceEqual([hole, torrential]) &&
              noMainActivation.LastMonsterWipe is null,
              "Neither Trap can be activated as a Normal Spell from the hand");
    }
}
