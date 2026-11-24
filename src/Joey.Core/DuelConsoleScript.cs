namespace Joey.Core;

public enum DuelScriptStatus { Advanced, Waiting, WaitingForDecision, Completed, Failed }
public sealed record DuelScriptStep(DuelScriptStatus Status, int? Line = null, string Message = "");

/// <summary>Same script admission driver for headless and graphical hosts.
/// The host alone advances the scheduler; one line per passage, no direct mutation.
/// A rejected/skipped recorded operation stops the script, never silently skips.
/// </summary>
public sealed class DuelConsoleScript
{
    private readonly DuelConsoleSession session;
    private readonly (int Line,string Text)[] lines;
    private int cursor, traceCursor;
    private DuelScriptStep? terminal;
    public int Count => lines.Length;
    public int AdmittedLines => cursor;

    public DuelConsoleScript(DuelConsoleSession session, IEnumerable<string> script)
    {
        this.session = session;
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
        if (session.HasPendingChoice &&
            (cursor == lines.Length || !session.AllowsPendingChoiceInput(lines[cursor].Text)))
        {
            string prompt = queue.PendingDecision is { } pending
                ? $"Decision {pending.Id} requires explicit answer."
                : session.HasPendingEndDiscard ? "End Phase requires discard-end INDEX."
                : "Pending hand selection requires select-hand.";
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
