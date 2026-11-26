using Joey.Core;

internal static class QueuedBattleCalculationChecks
{
    private const ushort KineticSoldier = 1253;
    private const ushort MadSwordBeast = 1341;

    private static Card Monster(ushort id, int type, int attack, int defense) =>
        new(id, id, "Battle fixture", "", ((uint)type << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));

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
    }
}
