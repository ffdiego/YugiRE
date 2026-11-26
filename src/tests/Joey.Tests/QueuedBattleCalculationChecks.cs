using Joey.Core;

internal static class QueuedBattleCalculationChecks
{
    private const ushort KineticSoldier = 1253;
    private const ushort MadSwordBeast = 1341;

    private static Card Monster(ushort id, int type, int attack, int defense, uint attribute = 0) =>
        new(id, id, "Battle fixture", "", ((uint)type << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10) | (attribute << 29));

    private static DemoDuel Battle(Card attacker, Card defender, bool defense = false, bool faceDown = false)
    {
        var duel = DemoDuel.FromPartialObservation(1, [], [defender, null, null, null, null],
            [attacker, null, null, null, null], 8000);
        duel.DefensePosition[0, 0] = defense;
        duel.MonsterFaceDown[0, 0] = faceDown;
        if (!duel.BeginBattle(out _)) throw new InvalidOperationException("Expected available Battle Phase.");
        return duel;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Battle did not finish.");
    }

    public static void Run(Action<bool, string> check)
    {
        var soldier = Monster(KineticSoldier, 7, 1350, 1800);
        var warrior = Monster(77, 15, 2000, 1500);
        var beast = Monster(MadSwordBeast, 9, 1400, 1200);
        var normal = Monster(24, 3, 1600, 1000);

        var cpu = Battle(soldier, warrior);
        var cpuQueue = cpu.CreatePrimitiveActionQueue();
        check(cpu.AttackValue(1, 0) == 1350 &&
            new CpuDuelController(1).Step(cpu) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack },
            "Kinetic Soldier CPU attacks a visible Warrior using the battle-only bonus");
        Drain(cpuQueue);
        check(cpu.Life.SequenceEqual([6650, 8000]) && cpu.Monsters[0, 0] is null &&
            cpu.AttackValue(1, 0) == 1350,
            "Kinetic Soldier gains ATK in damage calculation and retains displayed ATK");

        var defending = Battle(warrior, soldier, defense: true, faceDown: true);
        var defenseQueue = defending.CreatePrimitiveActionQueue();
        check(defending.TryQueueTargetedAttack(0, 0, out _) && defending.MonsterFaceDown[0, 0],
            "Kinetic Soldier can enter queued battle while Set");
        Drain(defenseQueue);
        check(defending.Life.SequenceEqual([8000, 6200]) && defending.DefenseValue(0, 0) == 1800 &&
            !defending.MonsterFaceDown[0, 0] && defending.Monsters[1, 0] is not null,
            "Kinetic Soldier gains DEF only after reveal for damage calculation");

        var nonWarrior = Battle(soldier, normal);
        var nonWarriorQueue = nonWarrior.CreatePrimitiveActionQueue();
        check(nonWarrior.TryQueueTargetedAttack(0, 0, out _), "Kinetic Soldier can attack a non-Warrior");
        Drain(nonWarriorQueue);
        check(nonWarrior.Life.SequenceEqual([8000, 7750]) && nonWarrior.Monsters[1, 0] is null,
            "Kinetic Soldier receives no bonus against a non-Warrior");

        foreach (bool faceDown in new[] { false, true })
        {
            var pierce = Battle(beast, normal, defense: true, faceDown: faceDown);
            var pierceQueue = pierce.CreatePrimitiveActionQueue();
            check(pierce.TryQueueTargetedAttack(0, 0, out _) && pierce.Life[0] == 8000,
                "Mad Sword Beast declaration leaves LP unchanged");
            Drain(pierceQueue);
            check(pierce.Life.SequenceEqual([7600, 8000]) && pierce.Monsters[0, 0] is null &&
                pierceQueue.Trace.Count(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is DamageCommand { Side: 0, Amount: 400 }) == 1,
                "Mad Sword Beast deals the positive difference against either defense visibility");
        }

        var needleWorm = Monster(FlipEffectRules.NeedleWorm, 14, 750, 600);
        var flip = Battle(beast, needleWorm, defense: true, faceDown: true);
        var flipQueue = flip.CreatePrimitiveActionQueue();
        check(flip.TryQueueTargetedAttack(0, 0, out _), "Mad Sword Beast can attack a supported FLIP defender");
        Drain(flipQueue);
        var commands = flipQueue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed)
            .Select(entry => entry.Entry.Command.GetType().Name).ToList();
        int damage = commands.IndexOf(nameof(DamageCommand));
        check(flip.Life[0] == 7200 && flip.LastDeckFlow is not null && damage >= 0 &&
            commands.IndexOf("OpenBattleFlipDeckFlowCommand") > damage,
            "Piercing damage precedes a supported battle FLIP");

        var lethal = Battle(beast, needleWorm, defense: true, faceDown: true);
        lethal.Life[0] = 800;
        var lethalQueue = lethal.CreatePrimitiveActionQueue();
        check(lethal.TryQueueTargetedAttack(0, 0, out _), "Lethal piercing attack is admitted");
        Drain(lethalQueue);
        check(lethal.Winner == 1 && lethal.LastDeckFlow is null && lethal.Graveyard(0).Single() == needleWorm,
            "Lethal piercing resolves cleanup without opening the defeated player's FLIP");

        var failed = Battle(beast, warrior, defense: true);
        var failedQueue = failed.CreatePrimitiveActionQueue();
        check(failed.TryQueueTargetedAttack(0, 0, out _), "Mad Sword Beast can attack a higher defense");
        Drain(failedQueue);
        check(failed.Life.SequenceEqual([8000, 7900]) && failed.Monsters[0, 0] is not null,
            "Higher DEF damages Mad Sword Beast's controller without piercing");

        CheckInsectSoldiers(check);
        CheckZombyra(check);
    }

    private static void CheckInsectSoldiers(Action<bool, string> check)
    {
        var soldiers = Monster(477, 10, 1000, 800);
        var wind = Monster(77, 15, 1800, 1700, attribute: 6);
        var faceUp = Battle(soldiers, wind);
        var faceUpQueue = faceUp.CreatePrimitiveActionQueue();
        check(new CpuDuelController(1).Step(faceUp) is { Action: CpuAction.Attack, Status: CpuStepStatus.Acted },
            "CPU uses Insect Soldiers' battle bonus against a visible WIND monster");
        Drain(faceUpQueue);
        check(faceUp.Life.SequenceEqual([7800, 8000]) && faceUp.Monsters[0, 0] is null &&
            faceUp.AttackValue(1, 0) == 1000 && faceUp.DefenseValue(1, 0) == 800,
            "Insect Soldiers add 1000 only to damage-calculation ATK against WIND");

        var hidden = Battle(soldiers, wind, defense: true, faceDown: true);
        var hiddenQueue = hidden.CreatePrimitiveActionQueue();
        check(hidden.TryQueueTargetedAttack(0, 0, out _) && hidden.MonsterFaceDown[0, 0],
            "Insect Soldiers declare before the WIND defender is revealed");
        Drain(hiddenQueue);
        check(hidden.Life.SequenceEqual([8000, 8000]) && hidden.Graveyard(0).Single() == wind,
            "Revealed WIND defender is destroyed without piercing damage");

        var nonWind = Battle(soldiers, Monster(77, 15, 1800, 1700, attribute: 5));
        var nonWindQueue = nonWind.CreatePrimitiveActionQueue();
        check(nonWind.TryQueueTargetedAttack(0, 0, out _), "Insect Soldiers may attack a non-WIND monster");
        Drain(nonWindQueue);
        check(nonWind.Life.SequenceEqual([8000, 7200]) && nonWind.Graveyard(1).Single() == soldiers,
            "Insect Soldiers gain no bonus against a non-WIND opponent");

        var defending = Battle(wind, soldiers);
        var defendingQueue = defending.CreatePrimitiveActionQueue();
        check(defending.TryQueueTargetedAttack(0, 0, out _), "WIND may attack Insect Soldiers in attack position");
        Drain(defendingQueue);
        check(defending.Life.SequenceEqual([7200, 8000]) && defending.Graveyard(0).Single() == soldiers,
            "Insect Soldiers gain no bonus while defending against WIND");
    }

    private static void CheckZombyra(Action<bool, string> check)
    {
        var zombyra = Monster(1523, 15, 2100, 500);
        var weaker = Monster(77, 15, 1300, 1000);
        var duel = DemoDuel.FromPartialObservation(1, [], [weaker, weaker, null, null, null],
            [zombyra, null, null, null, null], 8000);
        var queue = duel.CreatePrimitiveActionQueue();
        void NextOwnTurn()
        {
            for (int turn = 0; turn < 2; turn++)
            {
                if (!duel.TryQueueEndTurn(out _)) throw new InvalidOperationException("Could not advance Zombyra fixture.");
                Drain(queue);
            }
        }

        for (int battle = 0; battle < 2; battle++)
        {
            check(duel.BeginBattle(out _) && duel.TryQueueTargetedAttack(0, battle, out _) &&
                duel.AttackValue(1, 0) == 2100 - 200 * battle, "Zombyra declares using its existing ATK");
            Drain(queue);
            check(duel.Monsters[0, battle] is null && duel.AttackValue(1, 0) == 1900 - 200 * battle &&
                duel.DefenseValue(1, 0) == 500, "Zombyra loses 200 ATK per completed battle destruction");
            if (battle == 0) NextOwnTurn();
        }
        check(duel.Life[0] == 6600 && !duel.CanAttack(0) && !duel.CanAttackDirectly(0) &&
            !duel.TryQueueAttack(0, -1, out _, out bool unsupported) && !unsupported,
            "Zombyra retains the accumulated loss across turns and cannot declare on an empty opposing field");
        var commands = queue.Trace.Where(entry => entry.Stage == ActionTraceStage.Executed)
            .Select(entry => entry.Entry.Command.GetType().Name).ToList();
        check(commands.Count(name => name == "ApplyZombyraBattleAttackLossCommand") == 2 &&
            commands.LastIndexOf("ApplyZombyraBattleAttackLossCommand") > commands.LastIndexOf("FinishBattleRemovalCommand"),
            "Each Zombyra attack loss has its own command after battle removal");

        check(duel.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var source), "Capture weakened Zombyra");
        queue.Enqueue(new ReturnToHandCommand(1, source));
        Drain(queue);
        NextOwnTurn();
        check(duel.TryQueueNormalPlacement(0, 0, false, out _), "The same Zombyra can be summoned after leaving the field");
        Drain(queue);
        check(duel.AttackValue(1, 0) == 2100 &&
            duel.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var returned) && returned == source,
            "Returning Zombyra to the field resets the penalty for the same occurrence");

        var defending = Battle(weaker, zombyra);
        var defenseQueue = defending.CreatePrimitiveActionQueue();
        check(defending.TryQueueTargetedAttack(0, 0, out _), "Zombyra can win while being attacked");
        Drain(defenseQueue);
        check(defending.AttackValue(0, 0) == 1900 && defending.Monsters[1, 0] is null,
            "Zombyra applies the loss when it destroys the attacking monster");

        var tied = Battle(zombyra, Monster(77, 15, 2100, 1000));
        var tieQueue = tied.CreatePrimitiveActionQueue();
        check(tied.TryQueueTargetedAttack(0, 0, out _), "Equal attack battle fixture is admitted");
        Drain(tieQueue);
        check(tied.Monsters[0, 0] is null && tied.Monsters[1, 0] is null &&
            !tieQueue.Trace.Any(entry => entry.Entry.Command.GetType().Name == "ApplyZombyraBattleAttackLossCommand"),
            "A Zombyra destroyed in the same battle leaves no surviving occurrence to modify");

        var flip = Battle(zombyra, Monster(FlipEffectRules.NeedleWorm, 10, 750, 600), defense: true, faceDown: true);
        var flipQueue = flip.CreatePrimitiveActionQueue();
        check(flip.TryQueueTargetedAttack(0, 0, out _), "Zombyra can attack a supported FLIP defender");
        Drain(flipQueue);
        check(flip.LastDeckFlow is not null && flip.AttackValue(1, 0) == 1900 && flip.Monsters[0, 0] is null,
            "Zombyra loses ATK after a successful battle destruction following FLIP");
    }
}
