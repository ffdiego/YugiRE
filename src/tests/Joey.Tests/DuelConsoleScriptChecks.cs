using Joey.Core;
using Joey.Lab;

internal static class DuelConsoleScriptChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release {get;set;}
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>Release;
    }
    public static void Run(Action<bool,string> check)
    {
        string[] lines=["# initial fixture","spawn 24 0 deck","draw 1 0","spawn 24 1 monster 0",
            "state","destroy 1 0","damage 300 1","heal 100 0","diff"];
        (string State,ActionTraceEntry[] Trace) Pilot(bool delayed)
        {
            var d=DuelConsoleSession.EmptyLabDuel();var gate=new Gate();
            var s=new DuelConsoleSession(d,delayed?gate:null,[DuelConsoleRunner.FixtureCard()]);
            var script=new DuelConsoleScript(s,lines);
            DuelScriptStep step=new(DuelScriptStatus.Waiting);
            for(int i=0;i<500;i++)
            {
                step=script.Advance();
                if(step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed) break;
                if(s.Queue.IsBusy)
                {
                    int before=script.AdmittedLines;
                    gate.Release=false;s.Queue.Tick();
                    if(delayed && s.Queue.IsBusy)
                        check(script.Advance().Status==DuelScriptStatus.Waiting && script.AdmittedLines==before,
                            "A04.4 script never advances while presentation is busy");
                    gate.Release=true;
                    if(delayed && s.Queue.IsBusy) s.Queue.Tick();
                }
            }
            check(step.Status==DuelScriptStatus.Completed && d.Life.SequenceEqual([8100,7700]) &&
                d.Hand.Count==1 && d.Monsters[1,0] is null,"A04.4 shared script completes");
            return (s.Execute("state").Message,s.Queue.Trace.ToArray());
        }
        var a=Pilot(false);var b=Pilot(true);
        check(a.State==b.State && a.Trace.SequenceEqual(b.Trace),"A04.4 script traces identical with/without gate");
        var d=DuelConsoleSession.EmptyLabDuel();var s=new DuelConsoleSession(d,cards:[DuelConsoleRunner.FixtureCard()]);
        var invalid=new DuelConsoleScript(s,["spawn 24 0 monster 0","spawn 24 0 monster 0","damage 500 1"]);
        DuelScriptStep step=new(DuelScriptStatus.Waiting);
        for(int i=0;i<100;i++)
        {
            step=invalid.Advance();
            if(step.Status==DuelScriptStatus.Failed)break;
            if(s.Queue.IsBusy)s.Queue.Tick();
        }
        check(step.Status==DuelScriptStatus.Failed && step.Line==2 && d.Life[1]==8000,
            "A04.4 rejected execution stops script before later operations");

        var replayDuel=DuelConsoleSession.EmptyLabDuel();
        var replaySession=new DuelConsoleSession(replayDuel,cards:[DuelConsoleRunner.FixtureCard()]);
        check(replaySession.Execute("spawn 24 1 hand").Enqueued,"A04 replay fixture");
        for(int i=0;i<100 && replaySession.Queue.IsBusy;i++) replaySession.Queue.Tick();
        check(replayDuel.TryCaptureCardTarget(1,DuelZone.Hand,0,out var original),"A04 replay captures physical occurrence");
        int traceCount=replaySession.Queue.Trace.Count;
        check(!replaySession.Execute("move-id 25 1 hand 0 monster 2 set").Success &&
            replaySession.Queue.Trace.Count==traceCount && replayDuel.HandFor(1).Count==1,
            "A04 replay mismatched source rejects before mutation");
        check(replaySession.Execute("move-id 24 1 hand 0 monster 2 set").Enqueued,
            "A04 matching replay placement admitted");
        for(int i=0;i<100 && replaySession.Queue.IsBusy;i++) replaySession.Queue.Tick();
        check(replayDuel.TryCaptureCardTarget(1,DuelZone.Monster,2,out var placed) &&
            placed.Occurrence==original.Occurrence && replayDuel.MonsterFaceDown[1,2] &&
            replayDuel.DefensePosition[1,2] && replayDuel.HandFor(1).Count==0,
            "A04 replay placement preserves occurrence and pose");
    }
}
