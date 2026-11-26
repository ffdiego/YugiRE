using Joey.Core;

internal static class QueuedHayabusaChecks
{
    private static Card Warrior(ushort id, uint attack) =>
        new(id, id, "Attack fixture", "", (15u << 20) | (4u << 25) | ((attack / 10) << 9));

    private static Card?[] Slots(Card? first = null) => [first, null, null, null, null];

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Hayabusa battle did not finish.");
    }

    public static void Run(Action<bool, string> check)
    {
        var knight = Warrior(1336, 1400);
        var weaker = Warrior(77, 1000);
        var duel = DemoDuel.FromPartialObservation(1, [], Slots(weaker), Slots(knight), 8000);
        var queue = duel.CreatePrimitiveActionQueue();
        var cpu = new CpuDuelController(1);
        check(duel.BeginBattle(out _) &&
            cpu.Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: 0 },
            "Hayabusa Knight's first queued attack targets the opposing monster");
        Drain(queue);
        check(duel.Life[0] == 7600 && duel.Monsters[0, 0] is null &&
            cpu.Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 },
            "Hayabusa Knight's second attack can be direct after clearing the opposing field");
        Drain(queue);
        check(duel.Life[0] == 6200 && !duel.CanAttack(0) &&
            !duel.TryQueueAttack(0, -1, out _, out bool unsupported) && !unsupported,
            "Hayabusa Knight can't make a third attack or route it through fallback");
        check(cpu.Step(duel).Action == CpuAction.EndBattle, "CPU leaves Battle Phase when Hayabusa is used twice");
        Drain(queue);

        for (int turn = 0; turn < 2; turn++)
        {
            check(duel.TryQueueEndTurn(out _), "Advance toward next Hayabusa Battle Phase");
            Drain(queue);
        }
        check(duel.BeginBattle(out _) && duel.TryQueueDirectAttack(0, out _),
            "Hayabusa Knight regains its attack quota next turn");
        Drain(queue);
        check(duel.TryQueueDirectAttack(0, out _), "The renewed quota includes the second attack");
        Drain(queue);

        ushort id = AttackTrapRules.MagicCylinder;
        var cylinder = new Card(id, id, "Magic Cylinder", "", 21u << 20);
        var response = DemoDuel.FromPartialObservation(0, [cylinder], Slots(), Slots(knight), 8000);
        var responseQueue = response.CreatePrimitiveActionQueue();
        check(response.TryQueueSpellTrapSet(0, out _), "Hayabusa response fixture Sets Magic Cylinder");
        Drain(responseQueue);
        check(response.TryQueueEndTurn(out _), "Advance to Hayabusa response Battle Phase");
        Drain(responseQueue);
        check(response.BeginBattle(out _) && response.TryQueueDirectAttack(0, out _),
            "Hayabusa declares first attack into a response window");
        Drain(responseQueue);
        var pending = response.PendingAttackResponse ?? throw new InvalidOperationException("Missing attack response.");
        check(response.TryQueueAttackResponse(pending, pending.EligibleTraps.Single(), out _),
            "Magic Cylinder negates Hayabusa's first attack through the queue");
        Drain(responseQueue);
        check(response.Life.SequenceEqual([8000, 6600]) && response.TryQueueDirectAttack(0, out _),
            "Negating the first declaration leaves one queued Hayabusa attack");
        Drain(responseQueue);
        check(response.Life.SequenceEqual([6600, 6600]) && !response.CanAttack(0),
            "Negated declaration consumes one of Hayabusa Knight's two attacks");

        var infinite = new Card(0x593, 0x593, "Infinite Cards", "", 22u << 20);
        var mixed = DemoDuel.FromPartialObservation(0, [infinite], Slots(), Slots(knight), 8000);
        var mixedQueue = mixed.CreatePrimitiveActionQueue();
        check(mixed.TryQueueActivation(0, false, out _), "Mixed battle fixture activates an unsupported face-up Spell");
        Drain(mixedQueue);
        check(mixed.TryQueueEndTurn(out _), "Advance mixed battle fixture");
        Drain(mixedQueue);
        check(mixed.BeginBattle(out _) &&
            new CpuDuelController(1).Step(mixed) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack } &&
            mixed.Life[0] == 6600,
            "Existing single-attack fallback remains available in an unsupported battle configuration");
        check(!mixed.TryQueueAttack(0, -1, out _, out bool extraUnsupported) &&
            !extraUnsupported && !mixed.Attack(0, -1, out _) &&
            new CpuDuelController(1).Step(mixed) is { Status: CpuStepStatus.Acted, Action: CpuAction.EndBattle },
            "The CPU leaves an unsupported extra attack and avoids a legacy second declaration");
        Drain(mixedQueue);
    }
}
