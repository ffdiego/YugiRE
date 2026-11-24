using Joey.Core;
using Joey.Lab;
using System.Text.Json;

internal static class DuelConsoleTraceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var duel=DuelConsoleSession.EmptyLabDuel();
        var session=new DuelConsoleSession(duel,cards:[DuelConsoleRunner.FixtureCard()]);
        check(session.Execute("spawn 24 0 hand").Enqueued,"A04/A05 trace setup");
        for(int i=0;i<100 && session.Queue.IsBusy;i++)session.Queue.Tick();
        check(session.Execute("summon 0 3").Enqueued,"A04/A05 real summon admission");
        var before=session.Execute("trace");
        using(var json=JsonDocument.Parse(before.Message))
            check(!json.RootElement.GetProperty("complete").GetBoolean(),"A04/A05 busy trace incomplete");
        for(int i=0;i<100 && session.Queue.IsBusy;i++)session.Queue.Tick();
        int count=session.Queue.Trace.Count;
        var after=session.Execute("trace");
        using(var json=JsonDocument.Parse(after.Message))
        {
            var root=json.RootElement;
            check(root.GetProperty("complete").GetBoolean() &&
                root.GetProperty("rows").EnumerateArray().Any(r=>r.GetProperty("command").GetString()=="SummonCommand" &&
                    r.GetProperty("args").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual([0xCE,24,0x300,1])) &&
                root.GetProperty("rows").EnumerateArray().Any(r=>r.GetProperty("command").GetString()=="LabSpawnCommand" &&
                    r.GetProperty("mapping").GetString()=="unmapped"),
                "A04/A05 trace captures real summon and distinguishes setup");
        }
        check(session.Queue.Trace.Count==count,"A04/A05 trace query read only");
        string rootPath=Path.Combine(Path.GetTempPath(),"yugire-trace-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            string script=Path.Combine(rootPath,"script.txt"),trace=Path.Combine(rootPath,"trace.json");
            File.WriteAllText(script,"draw 1\ndamage 200 1\n");
            using var output=new StringWriter();using var error=new StringWriter();
            check(DuelConsoleRunner.Run(["--fixture",script,"--trace-output="+trace],TextReader.Null,output,error)==0,
                "A04/A05 runner trace export");
            string first=File.ReadAllText(trace);
            using(var json=JsonDocument.Parse(first))
                check(json.RootElement.GetProperty("complete").GetBoolean() &&
                    json.RootElement.GetProperty("rows").GetArrayLength()==2,"A04/A05 exported two executed rows");
            check(DuelConsoleRunner.Run(["--fixture",script,"--trace-output="+trace],TextReader.Null,output,error)==2 &&
                File.ReadAllText(trace)==first,"A04/A05 never overwrites trace");
            File.WriteAllText(script,"draw 1\ninvalid-command\n");
            string failed=Path.Combine(rootPath,"failed.json");
            check(DuelConsoleRunner.Run(["--fixture",script,"--trace-output="+failed],TextReader.Null,output,error)==1,
                "A04/A05 invalid script fails");
            using var failedJson=JsonDocument.Parse(File.ReadAllText(failed));
            check(!failedJson.RootElement.GetProperty("complete").GetBoolean(),"A04/A05 failed script never labelled complete");
        }
        finally { Directory.Delete(rootPath,true); }
    }
}
