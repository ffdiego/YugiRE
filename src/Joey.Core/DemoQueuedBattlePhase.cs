namespace Joey.Core;

public sealed partial class DemoDuel
{
    private const ushort GoblinAttackForce = Joey.Cards.TurnPhaseEffects.GoblinAttackForce;
    private readonly Dictionary<Card, int> positionLockedThroughTurn = new(ReferenceEqualityComparer.Instance);

    public bool TryQueueBattlePhaseChange(bool enterBattle, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = enterBattle ? "Battle Phase is unavailable." : "Battle Phase must be active and all decisions resolved.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked || !AllowsBattlePhaseChange(enterBattle))
            return false;

        queue.Enqueue(new ChangeBattlePhaseCommand(ActivePlayer, Turn, enterBattle), origin);
        message = "Battle Phase change admitted.";
        return true;
    }

    private CommandResult ExecuteQueuedBattlePhaseChange(ChangeBattlePhaseCommand command, IActionCommandSink followups)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || !AllowsBattlePhaseChange(command.EnterBattle))
            return new(CommandOutcome.Rejected, "Battle Phase change no longer legal for this side and turn.");

        if (!command.EnterBattle)
        {
            var defenses = QueuedBattleEndDefenses(command.Side).ToArray();
            if (defenses.Length > 0)
            {
                if (followups is not IActionBatchSink { PendingCount: 0 } queue)
                    return new(CommandOutcome.Rejected, "Battle Phase effects need isolated resolution.");
                queue.EnqueueBatch([.. defenses, new FinishBattlePhaseCommand(command.Side, command.ExpectedTurn)]);
                return new(CommandOutcome.Applied);
            }
        }
        ApplyBattlePhaseChange(command.EnterBattle);
        return new(CommandOutcome.Applied, command.EnterBattle ? "Battle Phase." : "Main Phase 2.");
    }

    private IEnumerable<GoblinAttackForceDefenseCommand> QueuedBattleEndDefenses(int side)
    {
        for (int slot = 0; slot < 5; slot++)
            if (IsGoblinForcedDefenseSource(side, slot) &&
                TryCaptureCardTarget(side, DuelZone.Monster, slot, out var target))
                yield return new(side, Turn, target);
    }

    private bool IsGoblinForcedDefenseSource(int side, int slot) =>
        Monsters[side, slot] is { IsMonster: true, Id: GoblinAttackForce } &&
        !MonsterFaceDown[side, slot] && HasDeclaredAttack(side, slot);

    private bool IsManualPositionLocked(int side, int slot, Card card) =>
        changedPosition[side, slot] || positionLockedThroughTurn.GetValueOrDefault(card) >= Turn;

    private CommandResult ExecuteGoblinAttackForceDefense(GoblinAttackForceDefenseCommand command)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || Phase != DemoPhase.Battle ||
            !TryResolveCardTarget(command.Source, out int slot, out var card) || !IsGoblinForcedDefenseSource(command.Side, slot))
            return new(CommandOutcome.Skipped, "Goblin Attack Force is no longer eligible.");

        positionLockedThroughTurn[card] = Turn + Joey.Cards.TurnPhaseEffects.GoblinPositionLockTurnOffset;
        if (!DefensePosition[command.Side, slot])
            CommitManualPosition(command.Side, slot);
        else
            changedPosition[command.Side, slot] = true;
        return new(CommandOutcome.Applied);
    }

    private CommandResult FinishQueuedBattlePhase(FinishBattlePhaseCommand command)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || !AllowsBattlePhaseChange(enterBattle: false))
            return new(CommandOutcome.Rejected, "Battle Phase can no longer finish.");

        ApplyBattlePhaseChange(enterBattle: false);
        return new(CommandOutcome.Applied, "Main Phase 2.");
    }
}
