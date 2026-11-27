namespace Joey.Core;

public enum DuelScriptStatus { Advanced, Waiting, WaitingForDecision, Completed, Failed }
public sealed record DuelScriptStep(DuelScriptStatus Status, int? Line = null, string Message = "");
public sealed record DuelScriptBoundary(int? Line, string? Input, bool HasPendingChoice, long? LastExecutedSequence);

/// <summary>Same script admission driver for headless and graphical hosts.
/// The host alone advances the scheduler; one line per passage, no direct mutation.
/// A rejected/skipped recorded operation stops the script, never silently skips.
/// </summary>
public sealed class DuelConsoleScript
{
    private readonly DuelConsoleSession session;
    private readonly (int Line,string Text)[] lines;
    private readonly Action<DuelScriptBoundary>? observeBoundary;
    private int cursor, traceCursor;
    private int observedCursor = -1;
    private DuelScriptStep? terminal;
    public int Count => lines.Length;
    public int AdmittedLines => cursor;

    public DuelConsoleScript(DuelConsoleSession session, IEnumerable<string> script,
        Action<DuelScriptBoundary>? observeBoundary = null)
    {
        this.session = session;
        this.observeBoundary = observeBoundary;
        lines = script.Select((text,index)=>(Line:index+1,Text:text.Trim()))
            .Where(line=>line.Text.Length>0 && !line.Text.StartsWith('#')).ToArray();
        traceCursor=session.Queue.Trace.Count;
    }

    public DuelScriptStep Advance()
    {
        if (terminal is not null) return terminal;
        var queue=session.Queue;
        if (queue.Fault is not null)
            return terminal=new(DuelScriptStatus.Failed,Message:"Queue fault: "+queue.Fault.Message);
        // A decision may be answered without advancing the other pending work.
        bool decision=queue.PendingDecision is not null;
        if (queue.IsBusy && !decision) return new(DuelScriptStatus.Waiting);
        var trace=queue.Trace;
        for(int i=traceCursor;i<trace.Count;i++)
        {
            var t=trace[i];
            if(t.Stage==ActionTraceStage.Executed && t.Result?.Outcome != CommandOutcome.Applied)
                return terminal=new(DuelScriptStatus.Failed, cursor==0?null:lines[cursor-1].Line,
                    $"Command {t.Entry.Sequence} {t.Result?.Outcome}: {t.Result?.Detail}");
        }
        traceCursor=trace.Count;
        if (observeBoundary is not null && observedCursor != cursor)
        {
            observedCursor = cursor;
            var lastLine = cursor == 0 ? ((int? Line, string? Text))(null, null) : lines[cursor - 1];
            long? lastSequence = trace.LastOrDefault(entry => entry.Stage == ActionTraceStage.Executed)?.Entry.Sequence;
            observeBoundary(new(lastLine.Line, lastLine.Text, session.HasPendingChoice, lastSequence));
        }
        if (session.HasPendingChoice &&
            (cursor == lines.Length || !session.AllowsPendingChoiceInput(lines[cursor].Text)))
        {
            string prompt = session.PendingChoicePrompt;
            return new(DuelScriptStatus.WaitingForDecision, Message: prompt);
        }
        if(cursor==lines.Length) return terminal=new(DuelScriptStatus.Completed,Message:"Script completed.");
        var next=lines[cursor++];
        if(next.Text is "quit" or "exit")
            return terminal=new(DuelScriptStatus.Completed,next.Line,"Script stopped.");
        var result=session.Execute(next.Text);
        if(!result.Success)
            return terminal=new(DuelScriptStatus.Failed,next.Line,result.Message);
        return new(DuelScriptStatus.Advanced,next.Line,result.Message);
    }
}
