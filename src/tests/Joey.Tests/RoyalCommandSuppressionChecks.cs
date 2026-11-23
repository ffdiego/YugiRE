using Joey.Core;

internal static class RoyalCommandSuppressionChecks
{
    private static Card Monster(ushort id, int index, int type = 10, int atk = 500, int def = 600) =>
        new(index, id, id switch { 500 => "Man-Eater Bug", 590 => "Morphing Jar",
            539 => "Skelengel", 427 => "Magician of Faith",
            751 => "Jinzo", _ => $"Monster {id}" },
            "", ((uint)type << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Trap(ushort id, int index) =>
        new(index, id, id switch { 1530 => "Royal Command", 1033 => "Royal Decree", _ => $"Trap {id}" },
            "", 21u << 20);
    private static Card Spell(ushort id, int index) =>
        new(index, id, $"Spell {id}", "", 22u << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null) =>
        [a, b, null, null, null];

    private static void PrepareFaceDown(DemoDuel duel, int player, int zone)
    {
        duel.MonsterFaceDown[player, zone] = true;
        duel.DefensePosition[player, zone] = true;
    }

    public static void Run(Action<bool, string> check)
    {
        var command = Trap(1530, 108);
        var decree = Trap(1033, 107);
        var jinzo = Monster(751, 428, type: 7, atk: 2400, def: 1500);
        var bug = Monster(500, 753);
        var bug2 = Monster(500, 1753);
        var enemy = Monster(140, 1754, atk: 2200, def: 1400);
        var jar = Monster(590, 972);
        var faith = Monster(427, 501);
        var skelengel = Monster(539, 440);
        var spell = Spell(342, 1755);

        var set = DemoDuel.FromPartialObservation(0, [command], Slots(bug),
            Slots(enemy), 8000);
        PrepareFaceDown(set, 0, 0);
        check(set.CanSetFromHand(0) && !set.CanActivateFromHand(0) &&
              set.SetFromHand(0, out _) && set.Spells[0, 0] == command &&
              set.SpellFaceDown[0, 0] && !set.FlipEffectsNegated &&
              !set.ActivateSetSpell(0, out _) && set.SpellFaceDown[0, 0],
              "Royal Command: can Set Trap but cannot activate it from hand or on Set turn");
        set.NextTurn(); set.NextTurn();
        check(set.ActivateSetSpell(0, out _) && !set.SpellFaceDown[0, 0] &&
              set.FlipEffectsNegated && set.Graveyard(0).Count == 0,
              "Royal Command: activation in later owner's Main Phase establishes continuous suppression");
        check(set.ChangePosition(0, out string flipMessage) &&
              flipMessage.Contains("negated", StringComparison.OrdinalIgnoreCase) &&
              !set.MonsterFaceDown[0, 0] && set.MonsterWasFlipped[0, 0] &&
              set.PendingFlipTarget is null && set.Monsters[1, 0] == enemy,
              "Royal Command: Flip Summon reveals Man-Eater Bug without executing destruction");

        var own = DemoDuel.FromPartialObservation(0, [], Slots(bug), Slots(enemy), 8000);
        PrepareFaceDown(own, 0, 0);
        own.Spells[1, 3] = command;
        own.SpellFaceDown[1, 3] = true;
        check(!own.FlipEffectsNegated &&
              own.RevealMonsterByEffect(0, 0, out _) &&
              own.PendingFlipTarget is { Owner: 0 } &&
              own.ResolveFlipTarget(1, 0, out _) && own.Monsters[1, 0] is null,
              "Royal Command: opponent's Set Trap does not negate FLIP");

        var on = DemoDuel.FromPartialObservation(0, [], Slots(bug, bug2), Slots(enemy), 8000);
        PrepareFaceDown(on, 0, 0);
        PrepareFaceDown(on, 0, 1);
        on.Spells[1, 1] = command;
        check(on.FlipEffectsNegated &&
              on.RevealMonsterByEffect(0, 0, out _) &&
              on.PendingFlipTarget is null && on.Monsters[1, 0] == enemy,
              "Royal Command: opponent's active Trap suppresses FLIPs across both fields");
        on.Spells[1, 1] = null;
        check(!on.FlipEffectsNegated &&
              on.RevealMonsterByEffect(0, 1, out _) &&
              on.PendingFlipTarget is { Owner: 0, SourceZone: 1 } &&
              on.ResolveFlipTarget(1, 0, out _) && on.Monsters[1, 0] is null,
              "Royal Command: removing active Trap restores FLIP activation for a new occurrence");

        var jinzoFixture = DemoDuel.FromPartialObservation(0, [],
            Slots(bug), Slots(jinzo), 8000);
        PrepareFaceDown(jinzoFixture, 0, 0);
        jinzoFixture.Spells[0, 2] = command;
        check(!jinzoFixture.FlipEffectsNegated &&
              jinzoFixture.RevealMonsterByEffect(0, 0, out _) &&
              jinzoFixture.PendingFlipTarget is not null,
              "Jinzo: face-up monster suppresses Royal Command instead of suppressing FLIP");
        var hiddenJinzo = DemoDuel.FromPartialObservation(0, [],
            Slots(bug), Slots(jinzo), 8000);
        PrepareFaceDown(hiddenJinzo, 0, 0);
        hiddenJinzo.MonsterFaceDown[1, 0] = true;
        hiddenJinzo.Spells[0, 2] = command;
        check(hiddenJinzo.FlipEffectsNegated &&
              hiddenJinzo.RevealMonsterByEffect(0, 0, out _) &&
              hiddenJinzo.PendingFlipTarget is null,
              "Jinzo: face-down Jinzo cannot negate active Royal Command");

        var decreeFixture = DemoDuel.FromPartialObservation(0, [], Slots(bug),
            Slots(enemy), 8000);
        PrepareFaceDown(decreeFixture, 0, 0);
        decreeFixture.Spells[0, 0] = command;
        decreeFixture.Spells[1, 4] = decree;
        check(!decreeFixture.FlipEffectsNegated &&
              decreeFixture.RevealMonsterByEffect(0, 0, out _) &&
              decreeFixture.PendingFlipTarget is not null,
              "Royal Decree: face-up trap suppresses Royal Command and FLIP remains available");
        var setDecree = DemoDuel.FromPartialObservation(0, [], Slots(bug),
            Slots(enemy), 8000);
        PrepareFaceDown(setDecree, 0, 0);
        setDecree.Spells[0, 0] = command;
        setDecree.Spells[1, 4] = decree;
        setDecree.SpellFaceDown[1, 4] = true;
        check(setDecree.FlipEffectsNegated &&
              setDecree.RevealMonsterByEffect(0, 0, out _) &&
              setDecree.PendingFlipTarget is null,
              "Royal Decree: Set Royal Decree does not suppress active Royal Command");

        var cannotActivateUnderJinzo = DemoDuel.FromPartialObservation(0, [command],
            Slots(), Slots(jinzo), 8000);
        check(cannotActivateUnderJinzo.SetFromHand(0, out _),
              "Royal Command: set fixture with Jinzo");
        cannotActivateUnderJinzo.NextTurn(); cannotActivateUnderJinzo.NextTurn();
        check(!cannotActivateUnderJinzo.ActivateSetSpell(0, out _) &&
              cannotActivateUnderJinzo.SpellFaceDown[0, 0] &&
              !cannotActivateUnderJinzo.FlipEffectsNegated,
              "Jinzo: prevents activation of Set Royal Command");
        cannotActivateUnderJinzo.Monsters[1, 0] = null;
        check(cannotActivateUnderJinzo.ActivateSetSpell(0, out _) &&
              cannotActivateUnderJinzo.FlipEffectsNegated,
              "Jinzo: removing Jinzo allows later Royal Command activation");

        var jarDuel = DemoDuel.FromPartialObservation(0, [], Slots(jar),
            Slots(enemy), 8000);
        PrepareFaceDown(jarDuel, 0, 0);
        jarDuel.Spells[0, 0] = command;
        check(jarDuel.RevealMonsterByEffect(0, 0, out _) &&
              jarDuel.Monsters[0, 0] == jar && !jarDuel.GameOver &&
              jarDuel.PendingFlipTarget is null &&
              jarDuel.Graveyard(0).Count == 0 && jarDuel.Graveyard(1).Count == 0,
              "Royal Command: Morphing Jar reveal does not discard hands or draw five");

        var battle = DemoDuel.FromPartialObservation(1, [], Slots(bug),
            Slots(enemy), 8000);
        PrepareFaceDown(battle, 0, 0);
        battle.Spells[1, 0] = command;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingFlipTarget is null && battle.Monsters[0, 0] is null &&
              battle.Monsters[1, 0] == enemy &&
              battle.Graveyard(0).SequenceEqual([bug]) &&
              battle.Graveyard(1).Count == 0 &&
              battle.Life.SequenceEqual([8000, 8000]),
              "Royal Command: negated battle FLIP still calculates and completes battle destruction");

        var lateTarget = DemoDuel.FromPartialObservation(0, [],
            Slots(bug), Slots(enemy), 8000);
        PrepareFaceDown(lateTarget, 0, 0);
        check(lateTarget.RevealMonsterByEffect(0, 0, out _) &&
              lateTarget.PendingFlipTarget is not null,
              "Royal Command: a FLIP request may exist before suppression arrives");
        lateTarget.Spells[1, 0] = command; // Closed fixture simulates a later continuous-source change.
        check(lateTarget.ResolveFlipTarget(1, 0, out string lateMessage) &&
              lateMessage.Contains("negated", StringComparison.OrdinalIgnoreCase) &&
              lateTarget.PendingFlipTarget is null &&
              lateTarget.Monsters[1, 0] == enemy &&
              lateTarget.Graveyard(1).Count == 0 &&
              !lateTarget.ResolveFlipTarget(1, 0, out _),
              "Royal Command: revalidation cancels already pending FLIP monster target");

        var faithDuel = DemoDuel.FromPartialObservation(0, [],
            Slots(faith), Slots(enemy), 8000, side0Graveyard: [spell]);
        PrepareFaceDown(faithDuel, 0, 0);
        check(faithDuel.RevealMonsterByEffect(0, 0, out _) &&
              faithDuel.PendingCardSelection is { EffectResourceId: 427 } &&
              faithDuel.HandFor(0).Count == 0,
              "Royal Command: Magician of Faith queues Graveyard-to-Hand target before suppression");
        faithDuel.Spells[1, 0] = command;
        check(faithDuel.ResolveCardSelection(0, out _) &&
              faithDuel.PendingCardSelection is null &&
              faithDuel.Graveyard(0).SequenceEqual([spell]) &&
              faithDuel.HandFor(0).Count == 0,
              "Royal Command: pending FLIP Graveyard selection cancels without moving a card");

        var decisionDuel = DemoDuel.FromPartialObservation(0, [], Slots(skelengel),
            Slots(enemy), 8000, side0Deck: [spell]);
        PrepareFaceDown(decisionDuel, 0, 0);
        check(decisionDuel.RevealMonsterByEffect(0, 0, out _) &&
              decisionDuel.PendingEffectDecision is {
                  Kind: EffectDecisionKind.DrawOneOnFlip },
              "Royal Command: Skelengel queues optional draw before suppression");
        var pendingId = decisionDuel.PendingEffectDecision!.Id;
        decisionDuel.Spells[1, 0] = command;
        check(decisionDuel.ResolveEffectDecision(pendingId, EffectDecisionAnswer.Accept, out _) &&
              decisionDuel.PendingEffectDecision is null &&
              decisionDuel.HandFor(0).Count == 0 &&
              decisionDuel.DeckCount(0) == 1,
              "Royal Command: pending Skelengel draw cancels even when player accepts");

        var storm = Spell(1061, 1661);
        var remove = DemoDuel.FromPartialObservation(0, [storm],
            Slots(), Slots(enemy), 8000);
        remove.Spells[1, 0] = command;
        check(remove.FlipEffectsNegated && remove.Play(0, out _) &&
              remove.Spells[1, 0] is null && !remove.FlipEffectsNegated &&
              remove.Graveyard(1).Single() == command,
              "Royal Command: global suppression recalculates when Heavy Storm removes its source");
    }
}
