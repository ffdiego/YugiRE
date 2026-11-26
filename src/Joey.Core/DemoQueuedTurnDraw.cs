namespace Joey.Core;

public sealed partial class DemoDuel
{
    private readonly record struct StandbyFieldLifeRule(ushort ResourceId, int Amount, bool? Defense);

    private const ushort MarieTheFallenOne = 1446;
    private const ushort DarkZebra = 1113;
    private static readonly StandbyFieldLifeRule[] StandbyFieldLifeRules =
    [
        new(1437, 1000, Defense: false), // Spirit of the Breeze
        new(1438, 1000, Defense: true),  // Dancing Fairy
        new(1441, 800, Defense: null),   // Cure Mermaid
    ];

    private static bool IsStandbyLifeMonster(Card card) => card.IsMonster &&
        (card.Id == MarieTheFallenOne || StandbyFieldLifeRules.Any(rule => rule.ResourceId == card.Id));

    private IEnumerable<DuelCommand> StandbyLifeEffects(int side)
    {
        foreach (var rule in StandbyFieldLifeRules)
        for (int slot = 0; slot < 5; slot++)
        {
            if (Monsters[side, slot] is { IsMonster: true } card && card.Id == rule.ResourceId &&
                !MonsterFaceDown[side, slot] && (rule.Defense is null || rule.Defense == DefensePosition[side, slot]))
                yield return new HealCommand(side, rule.Amount);
        }
        foreach (var card in graves[side])
        {
            if (card.IsMonster && card.Id == MarieTheFallenOne)
                yield return new HealCommand(side, 200);
        }
    }

    public bool TryQueueTurnDraw(out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Turn draw requires an idle duel and an unused draw for the active player.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanDraw)
            return false;

        queue.Enqueue(new TurnDrawCommand(ActivePlayer, Turn), origin);
        message = "Turn draw admitted.";
        return true;
    }

    private CommandResult ExecuteQueuedTurnDraw(TurnDrawCommand command, IActionCommandSink followups)
    {
        if (primitiveActionQueue is not { PendingCount: 0 } || command.Side != ActivePlayer ||
            command.ExpectedTurn != Turn || !AllowsTurnDraw)
            return new(CommandOutcome.Rejected, "Turn draw is no longer legal for this side and turn.");

        if (!TakeTurnDraw(out bool skipped))
            return new(CommandOutcome.Applied, "Empty deck: active player loses (demo policy).");

        if (!GameOver)
        {
            if (FindDueStandbyRevival() >= 0)
                followups.Enqueue(new ResolveStandbyRevivalCommand(command.Side, command.ExpectedTurn));
            if (StandbyLifeEffects(command.Side).Any() ||
                Enumerable.Range(0, 5).Any(slot => Monsters[command.Side, slot]?.Id == DarkZebra))
                followups.ScheduleContinuation(_ => new(GameOver ? [] : PlanStandbyEffects(command.Side, command.ExpectedTurn)));
        }
        return new(CommandOutcome.Applied, skipped ? "Draw Phase skipped." : "");
    }

    private int FindDueStandbyRevival() =>
        delayedRevivals.FindLastIndex(effect => effect.Owner == ActivePlayer && effect.DueTurn <= Turn);

    private DuelCommand[] PlanStandbyEffects(int side, int turn)
    {
        int slot = DarkZebraStandbySlot(side);
        if (slot >= 0 && TryCaptureCardTarget(side, DuelZone.Monster, slot, out var source))
            return [new StandbyDefenseCommand(side, turn, source), .. StandbyLifeEffects(side)];
        return [.. StandbyLifeEffects(side)];
    }

    private int DarkZebraStandbySlot(int side)
    {
        if (Enumerable.Range(0, 5).Count(slot => Monsters[side, slot] is not null) != 1)
            return -1;
        return Enumerable.Range(0, 5).FirstOrDefault(slot =>
            Monsters[side, slot] is { IsMonster: true, Id: DarkZebra } && !MonsterFaceDown[side, slot], -1);
    }

    private CommandResult ExecuteStandbyDefense(StandbyDefenseCommand command)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || Phase != DemoPhase.Main || !drawn ||
            !TryResolveCardTarget(command.Source, out int slot, out _) || slot != DarkZebraStandbySlot(command.Side))
            return new(CommandOutcome.Skipped, "The Standby monster or its field is no longer eligible.");

        if (!DefensePosition[command.Side, slot]) CommitManualPosition(command.Side, slot);
        else changedPosition[command.Side, slot] = true;
        return new(CommandOutcome.Applied);
    }

    private CommandResult ExecuteQueuedStandbyRevival(ResolveStandbyRevivalCommand command, IActionCommandSink followups)
    {
        if (command.Side != ActivePlayer || command.ExpectedTurn != Turn || Phase != DemoPhase.Main || !drawn)
            return new(CommandOutcome.Rejected, "Standby revival is no longer due for this turn.");

        int index = FindDueStandbyRevival();
        if (index < 0)
            return new(CommandOutcome.Skipped, "No delayed revival remains.");

        var revival = delayedRevivals[index];
        delayedRevivals.RemoveAt(index);
        int graveIndex = graves[command.Side].FindIndex(card => ReferenceEquals(card, revival.Source));
        int slot = Enumerable.Range(0, 5).FirstOrDefault(z => Monsters[command.Side, z] is null, -1);
        if (slot >= 0 && graveIndex >= 0 && TryCaptureCardTarget(command.Side, DuelZone.Graveyard, graveIndex, out var target))
            followups.Enqueue(new SpecialSummonCommand(command.Side, target, slot, FaceUpDefense: true));
        if (FindDueStandbyRevival() >= 0)
            followups.Enqueue(new ResolveStandbyRevivalCommand(command.Side, command.ExpectedTurn));
        return new(CommandOutcome.Applied);
    }
}
