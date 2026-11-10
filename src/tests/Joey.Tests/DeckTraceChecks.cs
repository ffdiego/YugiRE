using System.Text.Json;
using System.Text.Json.Nodes;
using Joey.Core;

// Synthetic observations exercise the validator; they are not native captures.
internal static class DeckTraceChecks
{
    public static void Run(Action<bool,string> check, Archive? archive = null)
    {
        var expected = new[] { new[] {1,2,3}, Array.Empty<int>(), Array.Empty<int>() };
        bool useArchive = false;
        var catalog = archive is null ? null : Catalog.Load(archive);
        JsonArray Trace(byte pool = 0, uint roll = uint.MaxValue, byte baseValue = 1)
        {
            string path = CpuDeckSelection.Select(baseValue,pool,roll);
            var result = JsonSerializer.SerializeToNode(new object[] {
                new {kind="deck_probe",phase="on",schema="joey-cpu-deck-v1",exeSha256=CpuDeckTraceVerifier.ExeSha256,captureMt=false},
                new {kind="deck_selection",phase="enter",selectionId="1:s:1",threadId=7,caller=0x48ad50,profile=new {baseDifficulty=baseValue,pool}},
                new {kind="deck_selection",phase="rng",selectionId="1:s:1",threadId=7,site=pool==0?0x44bda5:0x44bc84,rawRoll=roll},
                new {kind="deck_selection",phase="leave",selectionId="1:s:1",threadId=7,resourcePath=path,resourceDataPointer="0x1000",returnObjectMatches=true},
                new {kind="deck_scene",phase="enter",selectionId="1:s:1",threadId=7,sceneId="scene",caller=0x48ad71,requestedScene=0,
                    correlationProof="caller+thread+pointer+path",argumentPointer="0x1000",resourcePath=path},
                new {kind="deck_loader",phase="enter",selectionId="1:s:1",threadId=7,sceneId="scene",loadId="load",caller=0x44c0e8,loaderType=1,resourcePath=path},
                new {kind="deck_loader",phase="leave",selectionId="1:s:1",threadId=7,sceneId="scene",loadId="load",caller=0x44c0e8,loaderType=1,returnCode=0,
                    sourceSlot1=new {mainCount=expected[0].Length,main=expected[0],secondCount=expected[1].Length,second=expected[1],thirdCount=expected[2].Length,third=expected[2]}},
                new {kind="deck_scene",phase="leave",selectionId="1:s:1",threadId=7,sceneId="scene"},
                new {kind="deck_probe",phase="off"}
            })!.AsArray();
            for (int i=0;i<result.Count;i++) result[i]!["seq"]=i+1;
            return result;
        }
        CpuDeckTraceVerifier.Report Verify(JsonArray events, int overwritten=0, string? hash=null)
        {
            using var header=JsonDocument.Parse(JsonSerializer.Serialize(new {kind="metadata",exeSha256=hash??CpuDeckTraceVerifier.ExeSha256,
                seen=events.Count,retained=events.Count,overwritten}));
            using var body=JsonDocument.Parse(events.ToJsonString());
            return CpuDeckTraceVerifier.Validate(header.RootElement,body.RootElement.EnumerateArray().ToArray(),
                path => useArchive ? Resolve(path) : expected.Select(x=>(IReadOnlyList<int>)x).ToArray());
        }
        IReadOnlyList<IReadOnlyList<int>> Resolve(string path)
        {
            var deck = DeckFile.Read(archive!.Read(path[5..].Replace('/', '\\')));
            return new[] {0,2,1}.Select(group => (IReadOnlyList<int>)deck.Resolve(group,catalog!).Select(c=>c.Index).ToArray()).ToArray();
        }
        check(Verify(Trace()).ExitCode==0,"Deck trace uint32 CPU path and all source lists agree");
        check(Verify(Trace(1)).ExitCode==0,"Deck trace FUL path agrees");
        var data=Trace(); data[3]!["resourcePath"]="data/J/file/CPU_029.ydc";
        check(Verify(data).Issues.Any(x=>x.Category=="mismatch" && x.Detail.StartsWith("Path:")),"Deck trace reports precise selector path mismatch");
        data=Trace(); data[6]!["sourceSlot1"]!["main"]![1]=999;
        check(Verify(data).Issues.Any(x=>x.Detail.Contains("first differing index 1")),"Deck trace reports source order mismatch");
        data=Trace(); data[6]!["sourceSlot1"]!["secondCount"]=1;
        check(Verify(data).ExitCode==1,"Deck trace compares secondary lists and counts");
        data=Trace(); data[5]!["threadId"]=8;
        check(Verify(data).ExitCode==2,"Deck trace rejects cross-thread correlation");
        data=Trace(); data[4]!["argumentPointer"]="0x2000";
        check(Verify(data).ExitCode==2,"Deck trace rejects pointer mismatch");
        data=Trace(); data[6]!["returnCode"]=1;
        check(Verify(data).ExitCode==2,"Deck trace failed load is incomplete");
        data=Trace(); data[3]!["readError"]="unreadable";
        check(Verify(data).ExitCode==2,"Deck trace read failure cannot pass");
        check(Verify(Trace(),1).ExitCode==2,"Deck trace overflow cannot pass");
        check(Verify(Trace(),hash:"wrong").ExitCode==2,"Deck trace wrong fingerprint cannot pass");
        data=Trace(); data.RemoveAt(2);
        check(Verify(data).ExitCode==2,"Deck trace missing RNG cannot pass");
        data=Trace(); data.Insert(3,data[2]!.DeepClone());
        check(Verify(data).ExitCode==2,"Deck trace duplicate RNG cannot pass");
        data=Trace(); data[0]!["captureMt"]=true;
        check(Verify(data).ExitCode==2,"Deck trace requested missing MT cannot pass");
        var rng=NativeDeckRng.LegacySeeded(4357); uint roll=rng.Next32();
        rng=NativeDeckRng.LegacySeeded(4357); data=Trace(0,roll);
        data[0]!["captureMt"]=true;
        data[1]!["mtBefore"]=JsonSerializer.SerializeToNode(new {words=rng.SnapshotWords(),index=rng.Index});
        check(Verify(data).ExitCode==0 && Verify(data).NormalSelections[0].MtChecked,"Deck trace independently predicts MT output");
        data[1]!["mtBefore"]!["index"]=1;
        check(Verify(data).ExitCode==1,"Deck trace identifies MT mismatch");
        data=Trace(); data[4]!["requestedScene"]=7;
        check(Verify(data).ExitCode==2,"Deck trace Demo cannot validate normal selection");
        data=Trace(); data[5]!["selectionId"]=null;
        check(Verify(data).ExitCode==2,"Deck trace unbound normal loader cannot pass");
        data=Trace(); data.RemoveAt(data.Count-1);
        check(Verify(data).ExitCode==2,"Deck trace incomplete stop cannot pass");
        if (archive is not null)
        {
            useArchive = true;
            var resources = new HashSet<string>();
            for (byte pool=0;pool<=1;pool++)
                for (byte baseValue=1;baseValue<=7;baseValue++)
                    for (uint slot=0;slot<CpuDeckSelection.Options(baseValue,pool).Count;slot++)
                    {
                        string path=CpuDeckSelection.Select(baseValue,pool,slot);
                        expected=Resolve(path).Select(group=>group.ToArray()).ToArray();
                        check(Verify(Trace(pool,slot,baseValue)).ExitCode==0,
                            $"Synthetic deck trace resolves DAT source for base {baseValue} pool {pool} slot {slot}");
                        resources.Add(path);
                    }
            check(resources.Count==58,"Synthetic trace validation covers all 58 distinct DAT decks");
        }
    }
}
