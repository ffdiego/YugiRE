using Joey.Core;

internal static class PositionChangeEffectChecks
{
    private static Card Monster(ushort id, int index) =>
        new(index, id, id == 119 ? "Dream Clown" : id == 500 ? "Man-Eater Bug" : $"Monster {id}",
            "", (1u << 20) | (4u << 25) | (100u << 9) | 80u);

    private static Card?[] Slots(Card? a = null, Card? b = null) =>
        [a, b, null, null, null];

    private static void CheckQueued(Action<bool, string> check, Card clown, Card ally, Card enemy)
    {
        var royalCommand = new Card(205, FlipEffectSuppressionRules.RoyalCommand, "Royal Command", "", 21u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (bool cpu in new[] { false, true })
        {
            int other = 1 - side;
            var duel = DemoDuel.FromPartialObservation(side, [],
                side == 0 ? Slots(clown, ally) : Slots(enemy),
                side == 1 ? Slots(clown, ally) : Slots(enemy), 8000,
                equipmentContextResolved: true);
            // Royal Command is a FLIP negation. It must leave the position trigger active.
            duel.Spells[other, 0] = royalCommand;
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(duel.FlipEffectsNegated && duel.TryQueueChangePosition(0, out _), "Dream Clown queues the position change");
            for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();

            check(!queue.IsBusy && duel.DefensePosition[side, 0] &&
                duel.PendingFlipTarget is { Trigger: DemoMonsterEffectTrigger.PositionChange } pending &&
                duel.UsesQueuedMonsterFlipSelection(pending) &&
                duel.QueuedMonsterFlipCandidates().Select(c => c.Target.Side).SequenceEqual([other]),
                "Dream Clown exposes the opposing position target through the shared selector");
            if (duel.PendingFlipTarget is not { } request) continue;

            check(!duel.TrySelectQueuedMonsterFlipTarget(request, side, 1, out _) &&
                !duel.ResolveFlipTarget(other, 0, out _),
                "Dream Clown rejects allied targets and direct legacy resolution in queued mode");
            bool admitted = cpu
                ? new CpuDuelController(side).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Selection }
                : console.Execute($"select-flip {other} 0").Enqueued;
            check(admitted && duel.Monsters[other, 0] == enemy && duel.Graveyard(other).Count == 0,
                "Dream Clown target admission leaves the opposing occurrence in place");
            for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
            check(!queue.IsBusy && duel.PendingFlipTarget is null &&
                duel.Monsters[side, 1] == ally && duel.Monsters[other, 0] is null &&
                duel.Graveyard(other).SequenceEqual([enemy]) &&
                queue.Trace.Any(record => record.Entry.Command.GetType().Name == "ResolveMonsterPositionTargetCommand" &&
                    record.Entry.Origin == (cpu ? ActionOrigin.Cpu : ActionOrigin.Console)),
                "Dream Clown resolves exactly one opposing occurrence with a typed position effect");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        var clown = Monster(119, 201);
        var enemy = Monster(130, 202);
        var ally = Monster(131, 203);
        CheckQueued(check, clown, ally, enemy);
        var position = new DemoMonsterPositionChange(0, 0, clown,
            WasFaceDown: false, WasInDefense: false, IsFaceDown: false, IsInDefense: true);

        check(MonsterPositionEffectRules.Trigger(position) ==
              MonsterPositionTriggeredEffect.DestroyOpponentMonster &&
              position.FaceUpAttackToDefense && !position.FlipSummoned,
              "Dream Clown: face-up attack to defense triggers destruction");
        check(MonsterPositionEffectRules.Trigger(position with { WasFaceDown = true }) ==
              MonsterPositionTriggeredEffect.None &&
              MonsterPositionEffectRules.Trigger(position with { WasInDefense = true }) ==
              MonsterPositionTriggeredEffect.None &&
              MonsterPositionEffectRules.Trigger(position with { Card = ally }) ==
              MonsterPositionTriggeredEffect.None,
              "Position trigger: hidden start, reverse start and other IDs do not match");

        var duel = DemoDuel.FromPartialObservation(0, [], Slots(clown, ally), Slots(enemy), 8000);
        DemoMonsterPositionChange? observed = null;
        int notices = 0;
        duel.MonsterPositionChanged += change => { notices++; observed = change; };
        check(duel.ChangePosition(0, out string prompt) &&
              prompt.Contains("opposing monster", StringComparison.OrdinalIgnoreCase) &&
              duel.DefensePosition[0, 0] && !duel.MonsterFaceDown[0, 0] &&
              !duel.MonsterWasFlipped[0, 0] &&
              duel.PendingFlipTarget is { Owner: 0, Effect: DemoFlipEffect.DestroyOpponentMonster,
                  Trigger: DemoMonsterEffectTrigger.PositionChange, SourceCard.Id: 119 } &&
              observed == position && notices == 1,
              "Dream Clown: position event and pending target are not FLIP");
        check(!duel.ChangePosition(0, out _) && notices == 1 &&
              !duel.EndTurn(out _),
              "Position trigger: pending target prevents repeated actions");
        check(!duel.ResolveFlipTarget(0, 1, out _) &&
              duel.PendingFlipTarget is not null && duel.Monsters[0, 1] == ally &&
              duel.Monsters[1, 0] == enemy,
              "Dream Clown: cannot select own monster");
        check(duel.ResolveFlipTarget(1, 0, out string outcome) &&
              outcome.Contains("changing position", StringComparison.OrdinalIgnoreCase) &&
              duel.PendingFlipTarget is null && duel.Monsters[1, 0] is null &&
              duel.Graveyard(1).SequenceEqual([enemy]) &&
              duel.Monsters[0, 0] == clown && duel.Monsters[0, 1] == ally &&
              duel.Life.SequenceEqual([8000, 8000]) &&
              !duel.ResolveFlipTarget(1, 0, out _),
              "Dream Clown: destroys exactly one opposing occurrence and completes selection");

        var noEnemy = DemoDuel.FromPartialObservation(0, [], Slots(clown), Slots(), 8000);
        check(noEnemy.ChangePosition(0, out _) && noEnemy.DefensePosition[0, 0] &&
              noEnemy.PendingFlipTarget is null && !noEnemy.MonsterWasFlipped[0, 0],
              "Dream Clown: no opposing monster does not block position change");

        var reverse = DemoDuel.FromPartialObservation(0, [], Slots(clown), Slots(enemy), 8000);
        reverse.DefensePosition[0, 0] = true;
        check(reverse.ChangePosition(0, out _) && !reverse.DefensePosition[0, 0] &&
              reverse.PendingFlipTarget is null,
              "Dream Clown: defense to attack has no destruction trigger");

        var bug = Monster(500, 204);
        var flip = DemoDuel.FromPartialObservation(0, [], Slots(bug), Slots(enemy), 8000);
        flip.DefensePosition[0, 0] = flip.MonsterFaceDown[0, 0] = true;
        int flipNotices = 0;
        flip.MonsterPositionChanged += _ => flipNotices++;
        check(flip.ChangePosition(0, out _) && flipNotices == 1 &&
              flip.MonsterWasFlipped[0, 0] &&
              flip.PendingFlipTarget is { Effect: DemoFlipEffect.DestroyMonster,
                  Trigger: DemoMonsterEffectTrigger.Flip } &&
              flip.ResolveFlipTarget(1, 0, out _) && flip.Graveyard(1).SequenceEqual([enemy]),
              "Man-Eater Bug preserves existing Flip Summon and shared destruction resolver");

        var cpu = DemoDuel.FromPartialObservation(0, [], Slots(clown, ally), Slots(enemy), 8000);
        check(cpu.ChangePosition(0, out _) &&
              new CpuDuelController(0).Step(cpu) is
                  { Status: CpuStepStatus.Acted, Action: CpuAction.Selection } &&
              cpu.Monsters[1, 0] is null && cpu.Graveyard(1).SequenceEqual([enemy]) &&
              cpu.Monsters[0, 1] == ally,
              "CPU resolves position-triggered selection only against opposing monsters");
    }
}
