namespace Joey.Core;

public sealed partial class DemoDuel
{
    private DemoSummonResponse? queuedSummonResponse;
    private bool admittingQueuedSummonResponse;

    public bool UsesQueuedSummonResponse(DemoSummonResponse response) =>
        queuedSummonResponse is not null && ReferenceEquals(response, PendingSummonResponse) &&
        ReferenceEquals(response, queuedSummonResponse);

    private bool CanAdmitQueuedSummonResponse() =>
        !GameOver && Phase == DemoPhase.Main && PendingSummonResponse is { } response &&
        UsesQueuedSummonResponse(response) && response.Summon.Player == ActivePlayer;

    public bool TryQueueSummonResponse(DemoSummonResponse response, DemoSummonTrapOption? option,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible Set Trap or pass the current queued summon response.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanAdmitQueuedSummonResponse() ||
            !UsesQueuedSummonResponse(response)) return false;

        CardTarget? source = null;
        if (option is not null)
        {
            if (!TryCaptureCardTarget(option.Owner, DuelZone.SpellTrap, option.Zone, out var target) ||
                !ReferenceEquals(Spells[option.Owner, option.Zone], option.Trap) ||
                !TryPlanSummonTrap(response, target, out _, out _)) return false;
            source = target;
        }

        int responder = option?.Owner ?? response.EligibleTraps[0].Owner;
        admittingQueuedSummonResponse = true;
        try { queue.Enqueue(new ResolveSummonResponseCommand(responder, response, source), origin); }
        finally { admittingQueuedSummonResponse = false; }
        message = source is null ? "Summon response pass admitted." : "Summon Trap activation admitted.";
        return true;
    }

    private bool TryPlanSummonTrap(DemoSummonResponse response, CardTarget source, out Card trap,
        out IReadOnlyList<MonsterWipeTarget> targets)
    {
        trap = null!;
        targets = [];
        var summon = response.Summon;
        if (source.Zone != DuelZone.SpellTrap || !TryResolveCardTarget(source, out int slot, out trap) ||
            !TryCaptureCardTarget(summon.Player, DuelZone.Monster, summon.Zone, out _) ||
            !IsEligibleSummonTrap(response, source.Side, slot, trap)) return false;
        targets = trap.Id == SummonTrapRules.TrapHole
            ? [new(summon.Player, summon.Zone, summon.Monster, MonsterFaceDown[summon.Player, summon.Zone])]
            : MonsterWipeRules.TargetsForScope(MonsterWipeScope.BothPlayers, source.Side, Monsters, MonsterFaceDown);
        return targets.Count > 0;
    }

    private bool IsEligibleSummonTrap(DemoSummonResponse response, int owner, int zone, Card trap)
    {
        var summon = response.Summon;
        return response.EligibleTraps.Any(option => option.Owner == owner && option.Zone == zone &&
                   ReferenceEquals(option.Trap, trap)) &&
            SpellFaceDown[owner, zone] && spellSetTurn[owner, zone] < Turn &&
            ReferenceEquals(Monsters[summon.Player, summon.Zone], summon.Monster) &&
            SummonTrapRules.IsEligible(trap, owner, summon, AttackValue(summon.Player, summon.Zone));
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private PendingEffectDecision? flipDrawAfterSummonResponse;

        private void FinishQueuedManualPosition(DemoMonsterPositionChange change, bool resolveFlip, out string message)
        {
            duel.FinishManualPosition(change, resolveFlip, out message);
            // Intersecting target/card selections retain the existing priority path.
            if (change.FlipSummoned && duel.PendingSummonResponse is { Summon.Kind: DemoSummonKind.Flip } response &&
                duel.PendingFlipTarget is null && duel.PendingFlipSpellTrapTargets is null &&
                duel.PendingCardSelection is null && duel.PendingEffectDecision is null)
                duel.queuedSummonResponse = response;
        }

        private void ScheduleManualFlipDraw(PendingEffectDecision request, IActionCommandSink followups,
            bool resumeSummonTriggers = false)
        {
            queueDrawDecision = request;
            followups.ScheduleContinuation(_ => new([], answer =>
            {
                var selected = answer ?? throw new InvalidOperationException("Missing draw answer.");
                return new([new RecordFlipDrawDecisionCommand(request.Owner, request.Id, selected)],
                    resumeSummonTriggers ? _ => new([new FinishSummonFlipDrawCommand(request.Owner)]) : null);
            }, request));
        }

        private void ContinueAfterSummonResponse(IActionCommandSink followups)
        {
            var draw = flipDrawAfterSummonResponse;
            flipDrawAfterSummonResponse = null;
            if (draw is not null && !duel.GameOver && !duel.FlipEffectsNegated)
                ScheduleManualFlipDraw(draw, followups, resumeSummonTriggers: true);
            else
                duel.BeginNextQueuedDeckSearch();
        }

        private CommandResult ResolveSummonResponse(ResolveSummonResponseCommand command, IActionCommandSink followups)
        {
            if (!duel.CanAdmitQueuedSummonResponse() || !duel.UsesQueuedSummonResponse(command.Request) ||
                wipe is not null || finishScheduled || followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Summon response changed before execution.");

            if (command.Source is { } source)
            {
                if (!duel.TryPlanSummonTrap(command.Request, source, out var trap, out var targets))
                    return new(CommandOutcome.Rejected, "Summon Trap or its target is no longer eligible.");
                if (!TryReserveMonsterWipe(source, trap.Id, targets, queue, command.Request.Summon))
                    return new(CommandOutcome.Rejected, "Untracked occurrence or linked equipment.");
            }

            duel.queuedSummonResponse = null;
            duel.PendingSummonResponse = null;
            if (command.Source is null) ContinueAfterSummonResponse(followups);
            return new(CommandOutcome.Applied);
        }
    }
}
