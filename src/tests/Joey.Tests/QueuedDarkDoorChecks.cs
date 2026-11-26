using Joey.Core;

internal static class QueuedDarkDoorChecks
{
    private static readonly Card Door = new(1538, 1538, "The Dark Door", "", 22u << 20);
    private static readonly Card Typhoon = new(1079, SpellTrapTargetRules.MysticalSpaceTyphoon, "Mystical Space Typhoon", "", 22u << 20);

    private static Card Warrior(ushort id, uint attack) =>
        new(id, id, "Attack fixture", "", (15u << 20) | (4u << 25) | ((attack / 10) << 9));

    private static Card?[] Slots(Card? first = null, Card? second = null) => [first, second, null, null, null];

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("The Dark Door fixture did not drain.");
    }

    private static void EndTurn(DemoDuel duel, ActionQueue queue)
    {
        if (!duel.TryQueueEndTurn(out _)) throw new InvalidOperationException("Fixture could not end turn.");
        Drain(queue);
    }

    public static void Run(Action<bool, string> check)
    {
        var knight = Warrior(1336, 1400);
        var weaker = Warrior(77, 1000);
        foreach (int owner in new[] { 0, 1 })
        {
            int enemy = 1 - owner;
            var attackers = Slots(knight, knight);
            var defender = Slots(weaker);
            var duel = DemoDuel.FromPartialObservation(owner, [Door], owner == 0 ? defender : attackers,
                owner == 1 ? defender : attackers, 8000);
            var queue = duel.CreatePrimitiveActionQueue();
            bool fromSet = owner == 1;
            if (fromSet)
            {
                check(duel.TryQueueSpellTrapSet(0, out _), "The Dark Door can be Set before activation");
                Drain(queue);
            }
            check(duel.TryQueueActivation(0, fromSet, out _), "The Dark Door uses the continuous activation queue");
            Drain(queue);
            EndTurn(duel, queue);
            check(duel.BeginBattle(out _) && duel.CanAttack(1) && duel.TryQueueTargetedAttack(0, 0, out _),
                "Either occurrence can attack before the Battle Phase has a declared attacker");
            Drain(queue);
            check(duel.Monsters[owner, 0] is null && !duel.CanAttack(1) &&
                !duel.TryQueueAttack(1, -1, out _, out bool unsupported) && !unsupported &&
                duel.TryQueueDirectAttack(0, out _),
                "The Dark Door allows Hayabusa's second attack but rejects another copy without fallback");
            Drain(queue);
            check(duel.TryCaptureCardTarget(enemy, DuelZone.Monster, 0, out var first),
                "Capture the first attacker before its removal");
            queue.Enqueue(new DestroyCommand(enemy, first));
            Drain(queue);
            check(duel.Monsters[enemy, 0] is null && !duel.CanAttack(1),
                "Removing the first attacker does not allow another occurrence in the same phase");
            EndTurn(duel, queue);
            EndTurn(duel, queue);
            check(duel.BeginBattle(out _) && duel.TryQueueDirectAttack(1, out _),
                "The Dark Door allows a different attacker in the next Battle Phase");
            Drain(queue);
        }

        var controller = DemoDuel.FromPartialObservation(1, [Door], Slots(), Slots(weaker, weaker), 8000);
        var controllerQueue = controller.CreatePrimitiveActionQueue();
        check(controller.TryQueueActivation(0, false, out _), "Activate The Dark Door on the attacking side");
        Drain(controllerQueue);
        check(controller.BeginBattle(out _) && controller.TryQueueDirectAttack(0, out _),
            "The Dark Door permits its controller's first attacker");
        Drain(controllerQueue);
        check(!controller.CanAttack(1), "The Dark Door also limits the side that controls it");

        var copies = DemoDuel.FromPartialObservation(0, [Door, Door], Slots(), Slots(weaker, weaker), 8000,
            side1Deck: [Typhoon, Typhoon]);
        var copyQueue = copies.CreatePrimitiveActionQueue();
        for (int copy = 0; copy < 2; copy++)
        {
            check(copies.TryQueueActivation(0, false, out _), "Activate a distinct copy of The Dark Door");
            Drain(copyQueue);
        }
        EndTurn(copies, copyQueue);
        for (int removed = 1; removed <= 2; removed++)
        {
            check(copies.TryQueueTurnDraw(out _), "Draw the next Mystical Space Typhoon");
            Drain(copyQueue);
            check(copies.TryCaptureCardTarget(1, DuelZone.Hand, 0, out var spell) &&
                copies.TryCaptureCardTarget(0, DuelZone.SpellTrap, removed - 1, out var target) &&
                copies.TryQueueTargetedSpellTrap(spell, target, out _),
                "Destroy a single face-up Door occurrence before battle");
            Drain(copyQueue);
            check(copies.BeginBattle(out _) && copies.TryQueueDirectAttack(0, out _),
                "First attacker remains legal when one or both sources leave");
            Drain(copyQueue);
            check(copies.TryQueueDirectAttack(1, out _) == (removed == 2),
                "Only removing the final copy releases the second attacker");
            Drain(copyQueue);
            EndTurn(copies, copyQueue);
            EndTurn(copies, copyQueue);
        }

        var ai = DemoDuel.FromPartialObservation(1, [Door], Slots(weaker, weaker), Slots(knight), 8000,
            side1Deck: [weaker]);
        var aiQueue = ai.CreatePrimitiveActionQueue();
        var cpu = new CpuDuelController(1);
        check(cpu.Step(ai).Action == CpuAction.Draw, "CPU draws before considering The Dark Door");
        Drain(aiQueue);
        check(cpu.Step(ai) is { Action: CpuAction.Spell, Status: CpuStepStatus.Acted },
            "CPU activates The Dark Door against more visible attackers than it controls");
        Drain(aiQueue);
        check(ai.Spells[1, 0] == Door && !ai.SpellFaceDown[1, 0], "CPU places The Dark Door face-up");
    }
}
