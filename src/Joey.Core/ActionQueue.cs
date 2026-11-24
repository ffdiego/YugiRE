namespace Joey.Core;

public enum CommandOutcome { Applied, Skipped, Rejected }
// Domain-only facts captured by the executor at the successful mutation boundary.
public sealed record PlacementObservation(CardTarget Source, ushort ResourceId, int HandIndex,
    DuelZone Destination, int Slot, bool FaceDown, bool Defense, int PaidTributes);

public sealed record CommandResult(CommandOutcome Outcome, string Detail = "")
{
    public PlacementObservation? Placement { get; init; }
}
public sealed record QueuedDuelCommand(long Sequence, DuelCommand Command, ActionOrigin Origin);
public enum ActionTraceStage { Admitted, Started, Executed, Completed }
public sealed record ActionTraceEntry(QueuedDuelCommand Entry, ActionTraceStage Stage, CommandResult? Result = null);
public enum ActionTickResult { Idle, Blocked, Completed, Continued, WaitingForDecision }

/// <summary>Single-threaded rule executor; revalidate occurrence and conditions here.
/// Origin is deliberately not an input: equivalent admitted operations execute identically.
/// Throwing faults the queue; never retry a possibly partially applied command.</summary>
public interface IDuelCommandExecutor
{
    CommandResult Execute(DuelCommand command, IActionCommandSink followups);
}

public interface IActionCommandSink
{
    void Enqueue(DuelCommand command, ActionOrigin origin = ActionOrigin.Effect);
    void ScheduleContinuation(ActionContinuation continuation);
}

/// <summary>No clock, graphics library or frame count in the Core.</summary>
public interface IPresentationGate
{
    bool CanAdvance { get; }
    bool CanComplete(QueuedDuelCommand command, CommandResult result);
}

// Optional synchronous observer: capture immutable presentation input before the executor moves a card.
// The observer must not mutate the duel or enqueue commands.
public interface ICommandPresentationGate : IPresentationGate
{
    void OnStarting(QueuedDuelCommand command);
}

public sealed class ImmediatePresentationGate : IPresentationGate
{
    public static ImmediatePresentationGate Instance { get; } = new();
    private ImmediatePresentationGate() { }
    public bool CanAdvance => true;
    public bool CanComplete(QueuedDuelCommand command, CommandResult result) => true;
}

// Continuations inspect state / choice, and propose commands; they must not mutate
// the duel themselves. A decision step cannot also emit commands: first execute
// the preceding operations, then expose the prompt on another Tick.
public delegate ActionContinuationStep ActionContinuation(EffectDecisionAnswer? answer);
public sealed record ActionContinuationStep(
    IReadOnlyList<DuelCommand> Commands,
    ActionContinuation? Next = null,
    PendingEffectDecision? Decision = null);

/// <summary>Local scheduler, one command active and at most one new command per Tick.
/// This is infrastructure, not a player admission API or a replacement for DemoDuel yet.</summary>
public sealed class ActionQueue : IActionCommandSink
{
    private readonly Queue<QueuedDuelCommand> pending = new();
    private readonly LinkedList<ActionContinuation> continuations = new();
    private readonly List<ActionTraceEntry> trace = new();
    private readonly HashSet<long> decisionIds = new();
    private readonly IDuelCommandExecutor executor;
    private readonly IPresentationGate gate;
    private long sequence;
    private bool ticking;
    private bool startedBatch;
    private readonly Func<bool>? externalAdmission;
    private readonly bool allowLabEdits;
    private ActionContinuation? decisionContinuation;
    private EffectDecisionAnswer? decisionAnswer;

    public ActionQueue(IDuelCommandExecutor executor, IPresentationGate? gate = null, Func<bool>? externalAdmission = null, bool allowLabEdits = false)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.gate = gate ?? ImmediatePresentationGate.Instance;
        this.externalAdmission = externalAdmission;
        this.allowLabEdits = allowLabEdits;
    }

    public QueuedDuelCommand? Active { get; private set; }
    public CommandResult? ActiveResult { get; private set; }
    public PendingEffectDecision? PendingDecision { get; private set; }
    public Exception? Fault { get; private set; }
    public int PendingCount => pending.Count;
    public bool IsBusy => Fault is not null || Active is not null || pending.Count != 0 ||
        continuations.Count != 0 || PendingDecision is not null;
    public IReadOnlyList<ActionTraceEntry> Trace => trace.AsReadOnly();

    public void Enqueue(DuelCommand command, ActionOrigin origin = ActionOrigin.Effect) =>
        EnqueueBatch([command], origin);

    /// <summary>Validate the whole batch before appending any entry. No state mutation here.</summary>
    public void EnqueueBatch(IEnumerable<DuelCommand> commands, ActionOrigin origin = ActionOrigin.Effect)
    {
        EnsureHealthy();
        CheckExternalAdmission();
        if (PendingDecision is not null)
            throw new InvalidOperationException("Resolve the pending decision before admitting more work.");
        ArgumentNullException.ThrowIfNull(commands);
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        var batch = commands.ToArray();
        foreach (var command in batch)
        {
            DuelCommandValidation.Validate(command);
            if ((command is LabSpawnCommand or LabMoveCommand) && (!allowLabEdits || origin is not (ActionOrigin.Console or ActionOrigin.Test)))
                throw new InvalidOperationException("Laboratory edit requires an explicitly enabled lab queue and privileged origin.");
        }
        if (sequence > long.MaxValue - batch.Length) throw new OverflowException("Command sequence exhausted.");
        foreach (var command in batch)
        {
            var entry = new QueuedDuelCommand(++sequence, command, origin);
            pending.Enqueue(entry);
            trace.Add(new(entry, ActionTraceStage.Admitted));
        }
    }

    public void ScheduleContinuation(ActionContinuation continuation)
    {
        EnsureHealthy();
        CheckExternalAdmission();
        ArgumentNullException.ThrowIfNull(continuation);
        if (PendingDecision is not null)
            throw new InvalidOperationException("Resolve the pending decision before scheduling work.");
        continuations.AddLast(continuation);
    }

    // Submitting a choice does not resume the effect or change the duel.
    public bool SubmitDecision(long id, EffectDecisionAnswer answer)
    {
        EnsureHealthy();
        if (ticking || PendingDecision?.Id != id || decisionAnswer.HasValue || !Enum.IsDefined(answer))
            return false;
        decisionAnswer = answer;
        return true;
    }

    public ActionTickResult Tick()
    {
        EnsureHealthy();
        if (ticking) throw new InvalidOperationException("Recursive ActionQueue.Tick is forbidden.");
        ticking = true;
        try
        {
            if (!gate.CanAdvance) return ActionTickResult.Blocked;
            if (IsBusy) startedBatch = true;

            // Do not execute the same active command again when the gate stalls.
            if (Active is not null) return TryComplete();

            if (pending.TryDequeue(out var next))
            {
                Active = next;
                trace.Add(new(next, ActionTraceStage.Started));
                if (gate is ICommandPresentationGate observer) observer.OnStarting(next);
                var result = executor.Execute(next.Command, this)
                    ?? throw new InvalidOperationException("Executor returned no result.");
                if (!Enum.IsDefined(result.Outcome))
                    throw new InvalidOperationException("Executor returned an unknown outcome.");
                ActiveResult = result;
                trace.Add(new(next, ActionTraceStage.Executed, result));
                return TryComplete(); // even immediate completion does not dequeue again
            }

            if (PendingDecision is not null)
            {
                if (decisionAnswer is not { } answer) return ActionTickResult.WaitingForDecision;
                var resume = decisionContinuation!;
                PendingDecision = null;
                decisionContinuation = null;
                decisionAnswer = null;
                Resume(resume, answer);
                return ActionTickResult.Continued;
            }

            if (continuations.First is { } first)
            {
                continuations.RemoveFirst();
                Resume(first.Value, null);
                return ActionTickResult.Continued; // no recursive execution
            }

            return ActionTickResult.Idle;
        }
        catch (Exception ex)
        {
            Fault = ex;
            throw;
        }
        finally { ticking = false; }
    }

    private ActionTickResult TryComplete()
    {
        var current = Active!;
        var result = ActiveResult!;
        if (!gate.CanComplete(current, result)) return ActionTickResult.Blocked;
        trace.Add(new(current, ActionTraceStage.Completed, result));
        Active = null;
        ActiveResult = null;
        return ActionTickResult.Completed;
    }

    private void Resume(ActionContinuation continuation, EffectDecisionAnswer? answer)
    {
        var step = continuation(answer) ?? throw new InvalidOperationException("Missing continuation step.");
        var commands = step.Commands?.ToArray() ?? throw new InvalidOperationException("Missing continuation commands.");
        if (step.Decision is { } decision)
        {
            if (commands.Length != 0 || pending.Count != 0 || step.Next is null ||
                decision.Id <= 0 || decision.Owner is not (0 or 1) || !decisionIds.Add(decision.Id))
                throw new InvalidOperationException("Invalid or duplicate decision step.");
            PendingDecision = decision;
            decisionContinuation = step.Next;
            return;
        }
        EnqueueBatch(commands, ActionOrigin.Effect);
        if (step.Next is not null) continuations.AddFirst(step.Next);
    }

    // DemoDuel opts into a closed admission window once a batch starts.
    // Generic scheduler tests and consumers can still use the original FIFO.
    // Executor/continuation emissions are synchronous inside Tick.
    private void CheckExternalAdmission()
    {
        if (ticking || externalAdmission is null) return;
        if (!externalAdmission() || (IsBusy && startedBatch))
            throw new InvalidOperationException("Duel has a pending decision or a running action batch.");
        if (!IsBusy) startedBatch = false;
    }

    private void EnsureHealthy()
    {
        if (Fault is not null) throw new InvalidOperationException("Action queue is faulted; do not retry.", Fault);
    }
}
