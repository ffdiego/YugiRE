using Joey.Core;
using Raylib_cs;

static partial class Game
{
    static DuelConsoleSession? labSession;
    static DuelConsoleScript? labScript;
    static string[]? labOriginalLines;
    static DuelScriptStatus labStatus;
    static bool labConsoleOpen;
    static string labInput="";
    static readonly List<string> labLog=[];
    static int labTraceCursor;
    static string labLastWaiting="";

    static void StartConsoleLab(string[]? lines)
    {
        duel=DuelConsoleSession.EmptyLabDuel();
        // Reuse the fixed near/far board perspective and both hands; the CPU
        // controller is never stepped while labSession exists.
        alphaCpu=new(1);
        alphaBlocked=false;
        ResetDuelPresentation();
        labSession=new DuelConsoleSession(duel,new AppActionPresentationGate(),cards);
        queuedDuel=labSession.Queue;
        labOriginalLines=lines;
        labScript=lines is null ? null : new(labSession,lines);
        labStatus=lines is null ? DuelScriptStatus.Completed : DuelScriptStatus.Advanced;
        labConsoleOpen=lines is null;
        labInput="";
        labLog.Clear();
        labTraceCursor=0;
        labLastWaiting="";
        message="Laboratory: F10 console; no CPU.";
        LabLog(message);
    }

    static void LabLog(string text)
    {
        if(string.IsNullOrWhiteSpace(text)) return;
        Console.WriteLine(text);
        labLog.AddRange(text.Split('\n'));
        if(labLog.Count>100) labLog.RemoveRange(0,labLog.Count-100);
    }

    static void AdvanceConsoleLab()
    {
        if(labSession is not { } session) return;
        if(Raylib.IsKeyPressed(KeyboardKey.F10)) labConsoleOpen=!labConsoleOpen;
        var trace=session.Queue.Trace;
        for(int i=labTraceCursor;i<trace.Count;i++)
            if(trace[i].Stage==ActionTraceStage.Executed)
                LabLog($"{trace[i].Entry.Sequence}: {trace[i].Entry.Command.GetType().Name} {trace[i].Result?.Outcome} {trace[i].Result?.Detail}");
        labTraceCursor=trace.Count;
        if(labScript is not { } script || labStatus is DuelScriptStatus.Completed or DuelScriptStatus.Failed ||
            visualReviewPaused || MotionBusy || PhaseBusy) return;
        var step=script.Advance();
        labStatus=step.Status;
        if(step.Status==DuelScriptStatus.WaitingForDecision)
        {
            if(labLastWaiting!=step.Message) LabLog(step.Message);
            labLastWaiting=step.Message;
            return;
        }
        labLastWaiting="";
        if(step.Line is int n) LabLog($"[script line {n}]");
        LabLog(step.Message);
        if(step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed)
            message=step.Status==DuelScriptStatus.Completed ? "Laboratory script completed. F10 console." : "Script stopped. F10 for log.";
    }

    // Debug-only overlay, not a claim of a native Joey screen.
    static void DrawConsoleLab()
    {
        if(labSession is not { } session) return;
        if(!labConsoleOpen)
        {
            Label($"LAB | F10 console | {labStatus}", 264, 10, 12, Gold);
            return;
        }
        Raylib.DrawRectangle(245,80,545,420,new Color(7,15,28,245));
        Raylib.DrawRectangleLines(245,80,545,420,Gold);
        Label("LAB CONSOLE — F10 hide",255,90,16,Gold);
        int y=118;
        foreach(string line in labLog.TakeLast(17))
        {
            string display=line;
            while(display.Length>0 && MeasureLabel(display,12)>522) display=display[..^1];
            Label(display,255,y,12);
            y+=18;
        }
        bool manual=labScript is null || labStatus is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision;
        int ch;while((ch=Raylib.GetCharPressed())>0)
            if(manual && ch>=32 && ch<127 && labInput.Length<256) labInput+=(char)ch;
        if(manual && Raylib.IsKeyPressed(KeyboardKey.Backspace) && labInput.Length>0) labInput=labInput[..^1];
        if(manual && Raylib.IsKeyPressed(KeyboardKey.Enter) && labInput.Length>0)
        {
            var line=labInput.Trim();labInput="";
            LabLog("> "+line);
            if(labStatus==DuelScriptStatus.WaitingForDecision && !line.StartsWith("decision ",StringComparison.OrdinalIgnoreCase))
                LabLog("Answer the pending decision before resuming the script.");
            else if(MotionBusy || PhaseBusy || visualReviewPaused)
                LabLog("Presentation busy or paused; retry when ready.");
            else
            {
                var result=session.Execute(line);
                LabLog(result.Message);
            }
        }
        string prompt=manual ? "> "+labInput : "Script running; F6 pauses presentation.";
        while(prompt.Length>0 && MeasureLabel(prompt,14)>522) prompt=prompt[1..];
        Label(prompt,255,453,14,Gold);
        Label($"Queue: {session.Queue.PendingCount} | active: {session.Queue.Active?.Sequence.ToString() ?? "-"} | {labStatus}",255,476,12);
    }
}
