using Joey.Core;

internal static class QueuedAttackRestrictionMonsterChecks
{
    private static Card Monster(ushort id, uint attack, uint defense = 800) =>
        new(id, id, "Monster fixture", "", (15u << 20) | (4u << 25) | ((attack / 10) << 9) | (defense / 10));

    private static Card?[] Slots(Card? first = null, Card? second = null) => [first, second, null, null, null];

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Monster attack restriction fixture did not drain.");
    }

    private static void EndTurn(DemoDuel duel, ActionQueue queue)
    {
        if (!duel.TryQueueEndTurn(out _)) throw new InvalidOperationException("Fixture could not end turn.");
        Drain(queue);
    }

    private static DemoFlipTarget Choice(DemoDuel duel) =>
        duel.PendingFlipTarget ?? throw new InvalidOperationException("Expected a monster selection.");

    public static void Run(Action<bool, string> check)
    {
        var darkEyes = Monster(FlipEffectRules.DarkEyesIllusionist, 0, 1100);
        var invitation = Monster(1332, 1500);
        var ordinary = Monster(24, 1600);

        var flip = DemoDuel.FromPartialObservation(0, [], Slots(darkEyes, ordinary), Slots(ordinary, ordinary), 8000);
        flip.MonsterFaceDown[0, 0] = flip.DefensePosition[0, 0] = true;
        var flipQueue = flip.CreatePrimitiveActionQueue();
        check(flip.TryQueueChangePosition(0, out _), "Dark-Eyes is Flip Summoned through the queue");
        Drain(flipQueue);
        var request = Choice(flip);
        check(request.Trigger == DemoMonsterEffectTrigger.Flip && flip.QueuedMonsterFlipCandidates().Count == 4 &&
            !flip.ResolveFlipTarget(1, 0, out _) && flip.TrySelectQueuedMonsterFlipTarget(request, 1, 0, out _),
            "Dark-Eyes can designate either side through the occurrence-based queue");
        Drain(flipQueue);
        EndTurn(flip, flipQueue);
        check(flip.BeginBattle(out _) && !flip.CanAttack(0) && flip.CanAttack(1) &&
            !flip.TryQueueAttack(0, 0, out _, out bool unsupported) && !unsupported,
            "Dark-Eyes restricts only the designated copy of a monster");
        flip.MonsterFaceDown[0, 0] = flip.DefensePosition[0, 0] = true;
        check(!flip.CanAttack(0), "Dark-Eyes still restricts the target while it remains on the field face-down");
        check(flip.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var source), "Capture restriction source before removal");
        flipQueue.Enqueue(new DestroyCommand(0, source));
        Drain(flipQueue);
        check(flip.TryQueueTargetedAttack(0, 1, out _), "Destroying Dark-Eyes releases the designated monster");
        Drain(flipQueue);

        foreach (bool destroyed in new[] { false, true })
        {
            var battle = DemoDuel.FromPartialObservation(1, [], Slots(darkEyes), Slots(Monster(1336, destroyed ? 1500u : 1000u)), 8000);
            battle.MonsterFaceDown[0, 0] = battle.DefensePosition[0, 0] = true;
            var queue = battle.CreatePrimitiveActionQueue();
            check(battle.BeginBattle(out _) && battle.TryQueueTargetedAttack(0, 0, out _), "Battle Dark-Eyes reveal is admitted");
            Drain(queue);
            check(new CpuDuelController(0).Step(battle) is { Action: CpuAction.Selection, Status: CpuStepStatus.Acted },
                "CPU assigns Dark-Eyes restriction before battle finishes");
            Drain(queue);
            check((battle.Monsters[0, 0] is null) == destroyed && battle.CanAttack(0) == destroyed,
                "Dark-Eyes blocks Hayabusa's additional attack only while the source remains on the field");
        }

        var normal = DemoDuel.FromPartialObservation(1, [invitation], Slots(ordinary, ordinary), Slots(ordinary), 8000);
        var normalQueue = normal.CreatePrimitiveActionQueue();
        check(normal.TryQueueNormalPlacement(0, 1, false, out _), "Invitation Normal Summon is admitted");
        Drain(normalQueue);
        var summonChoice = Choice(normal);
        check(summonChoice.Trigger == DemoMonsterEffectTrigger.NormalOrFlipSummon &&
            normal.QueuedMonsterFlipCandidates().All(candidate => candidate.Target.Side == 0) &&
            !normal.TrySelectQueuedMonsterFlipTarget(summonChoice, 1, 0, out _) &&
            normal.TrySelectQueuedMonsterFlipTarget(summonChoice, 0, 0, out _),
            "Invitation allows only an opposing occurrence during Normal Summon selection");
        Drain(normalQueue);
        EndTurn(normal, normalQueue);
        check(normal.BeginBattle(out _) && !normal.CanAttack(0) && normal.CanAttack(1),
            "A face-up Invitation prevents the selected monster from attacking");
        normal.MonsterFaceDown[1, 1] = normal.DefensePosition[1, 1] = true;
        check(normal.CanAttack(0), "Invitation requires its source to remain face-up");

        ushort royalId = FlipEffectSuppressionRules.RoyalCommand;
        var royal = DemoDuel.FromPartialObservation(0, [], Slots(invitation), Slots(ordinary), 8000);
        royal.MonsterFaceDown[0, 0] = royal.DefensePosition[0, 0] = true;
        royal.Spells[1, 0] = new Card(royalId, royalId, "Royal Command", "", 21u << 20);
        var royalQueue = royal.CreatePrimitiveActionQueue();
        check(royal.TryQueueChangePosition(0, out _), "Invitation Flip Summon remains legal under Royal Command");
        Drain(royalQueue);
        check(royal.TrySelectQueuedMonsterFlipTarget(Choice(royal), 1, 0, out _),
            "Invitation's Flip Summon trigger is distinct from a FLIP effect");
        Drain(royalQueue);
        EndTurn(royal, royalQueue);
        check(royal.BeginBattle(out _) && !royal.CanAttack(0), "Invitation applies the binding through Royal Command");

        var special = DemoDuel.FromPartialObservation(1, [], Slots(ordinary), Slots(), 8000, side1Graveyard: [invitation]);
        var specialQueue = special.CreatePrimitiveActionQueue();
        check(special.TryCaptureCardTarget(1, DuelZone.Graveyard, 0, out var grave), "Capture Invitation in the Graveyard");
        specialQueue.Enqueue(new SpecialSummonCommand(1, grave, 0));
        Drain(specialQueue);
        check(special.Monsters[1, 0] == invitation && special.PendingFlipTarget is null,
            "Special Summon does not trigger Invitation's restriction");

        CheckSummonResponse(check, invitation, ordinary);
    }

    private static void CheckSummonResponse(Action<bool, string> check, Card invitation, Card ordinary)
    {
        ushort id = SummonTrapRules.TrapHole;
        var trap = new Card(id, id, "Trap Hole", "", 21u << 20);
        foreach (bool useTrap in new[] { false, true })
        {
            var duel = DemoDuel.FromPartialObservation(0, [trap], Slots(ordinary), Slots(), 8000, side1Deck: [invitation]);
            var queue = duel.CreatePrimitiveActionQueue();
            check(duel.TryQueueSpellTrapSet(0, out _), "Set Trap Hole for Invitation's Summon");
            Drain(queue);
            EndTurn(duel, queue);
            check(duel.TryQueueTurnDraw(out _), "Draw Invitation before Normal Summon");
            Drain(queue);
            check(duel.TryQueueNormalPlacement(0, 0, false, out _), "Invitation admits its Normal Summon");
            Drain(queue);
            var response = duel.PendingSummonResponse ?? throw new InvalidOperationException("Expected Trap Hole response.");
            check(duel.PendingFlipTarget is null && duel.TryQueueSummonResponse(response,
                useTrap ? response.EligibleTraps.Single() : null, out _), "Invitation waits for summon response");
            Drain(queue);
            check((duel.PendingFlipTarget is not null) == !useTrap && (duel.Monsters[1, 0] is null) == useTrap,
                "Invitation resumes selection on pass and drops it if the source was destroyed");
        }
    }
}
