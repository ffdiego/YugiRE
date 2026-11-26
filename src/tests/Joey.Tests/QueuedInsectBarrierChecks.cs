using Joey.Core;

internal static class QueuedInsectBarrierChecks
{
    private static readonly Card Barrier = new(1352, 1352, "Insect Barrier", "", 22u << 20);
    private static readonly Card Typhoon = new(1079, SpellTrapTargetRules.MysticalSpaceTyphoon, "Mystical Space Typhoon", "", 22u << 20);
    private static readonly Card Insect = new(1335, 1335, "Insect fixture", "", (10u << 20) | (4u << 25) | (180u << 9) | 200u);
    private static readonly Card Warrior = new(77, 77, "Warrior fixture", "", (15u << 20) | (4u << 25) | (120u << 9) | 100u);

    private static Card?[] Slots(Card? first = null, Card? second = null) => [first, second, null, null, null];

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Insect Barrier fixture did not drain.");
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (int owner in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        {
            int enemy = 1 - owner;
            var ownField = Slots(Insect);
            var enemyField = Slots(Insect, Warrior);
            var duel = DemoDuel.FromPartialObservation(owner, [Barrier], owner == 0 ? ownField : enemyField,
                owner == 1 ? ownField : enemyField, 8000,
                side0Deck: enemy == 0 ? [Typhoon] : [], side1Deck: enemy == 1 ? [Typhoon] : []);
            duel.DefensePosition[owner, 0] = true;
            var queue = duel.CreatePrimitiveActionQueue();
            if (fromSet)
            {
                check(duel.TryQueueSpellTrapSet(0, out _), "Insect Barrier may be Set");
                Drain(queue);
            }
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            check(duel.TryCaptureCardTarget(owner, zone, 0, out var source) &&
                duel.CanQueueFixedSpellActivation(zone, 0) &&
                duel.TryQueueActivation(0, fromSet, out _) &&
                (duel.Spells[owner, 0] is null || duel.SpellFaceDown[owner, 0]),
                "Insect Barrier hand and Set admission leave source untouched");
            Drain(queue);
            check(duel.TryCaptureCardTarget(owner, DuelZone.SpellTrap, 0, out var active) &&
                active.Occurrence == source.Occurrence && !duel.SpellFaceDown[owner, 0] &&
                duel.Graveyard(owner).Count == 0 && !duel.TryQueueActivation(0, true, out _),
                "Insect Barrier remains face-up as the same occurrence");

            check(duel.TryQueueEndTurn(out _), "Advance to the opposing Insect's turn");
            Drain(queue);
            check(duel.ActivePlayer == enemy && duel.TryQueueTurnDraw(out _), "Draw Typhoon on the opposing turn");
            Drain(queue);
            check(duel.BeginBattle(out _), "Start opposing Battle Phase");
            int before = queue.Trace.Count;
            check(!duel.CanAttack(0) && !duel.TryQueueAttack(0, 0, out _, out bool unsupported) &&
                !unsupported && queue.Trace.Count == before,
                "Face-up Insect Barrier rejects an opposing Insect before attack admission or fallback");
            check(duel.TryQueueTargetedAttack(1, 0, out _), "Warrior can still attack with opposing Insect Barrier active");
            Drain(queue);
            check(duel.Monsters[owner, 0] is not null && duel.Life[enemy] == 7200,
                "Warrior resolves combat while Insect Barrier stays active");

            check(duel.EndBattle(out _) && duel.TryCaptureCardTarget(enemy, DuelZone.Hand, 0, out var typhoon) &&
                duel.TryQueueTargetedSpellTrap(typhoon, active, out _),
                "Mystical Space Typhoon targets the exact face-up Barrier in Main Phase 2");
            Drain(queue);
            check(duel.Graveyard(owner).Count == 1 && duel.Graveyard(owner)[0] == Barrier,
                "Resolving Mystical Space Typhoon removes the continuous source");
            for (int turn = 0; turn < 2; turn++)
            {
                check(duel.TryQueueEndTurn(out _), "Advance toward the next Insect Battle Phase");
                Drain(queue);
            }
            check(duel.BeginBattle(out _) && duel.TryQueueTargetedAttack(0, 0, out _),
                "Removing the continuous source restores the Insect's attack");
            Drain(queue);
        }

        foreach (bool activate in new[] { false, true })
        {
            var direct = DemoDuel.FromPartialObservation(0, [Barrier], Slots(), Slots(Insect), 8000);
            var queue = direct.CreatePrimitiveActionQueue();
            check(activate ? direct.TryQueueActivation(0, false, out _) : direct.TryQueueSpellTrapSet(0, out _),
                "Direct attack fixture places Insect Barrier");
            Drain(queue);
            check(direct.TryQueueEndTurn(out _), "Direct attack fixture admits turn end");
            Drain(queue);
            check(direct.BeginBattle(out _) &&
                direct.TryQueueDirectAttack(0, out _) == !activate,
                "Insect Barrier applies to a direct attack only while face-up");
            Drain(queue);
            check(direct.Life[0] == (activate ? 8000 : 6200), "Only legal direct attack changes LP");
        }

        var cpuDuel = DemoDuel.FromPartialObservation(1, [Barrier], Slots(Insect), Slots(Insect), 8000,
            side1Deck: [Warrior]);
        var cpuQueue = cpuDuel.CreatePrimitiveActionQueue();
        var cpu = new CpuDuelController(1);
        check(cpu.Step(cpuDuel).Action == CpuAction.Draw, "CPU uses normal turn draw with Insect Barrier in hand");
        Drain(cpuQueue);
        check(cpu.Step(cpuDuel) is { Action: CpuAction.Spell, Status: CpuStepStatus.Acted },
            "CPU activates Insect Barrier against a visible Insect");
        Drain(cpuQueue);
        check(cpuDuel.BeginBattle(out _) && cpuDuel.TryQueueTargetedAttack(0, 0, out _),
            "Insect Barrier permits its controller's own Insect to attack");
        Drain(cpuQueue);
    }
}
