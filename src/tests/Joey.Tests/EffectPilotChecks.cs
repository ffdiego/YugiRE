using System.Text.Json.Nodes;
using Joey.Lab;

internal static class EffectPilotChecks
{
    public static void Run(Action<bool,string> check)
    {
        string fixture=Path.Combine(AppContext.BaseDirectory,"Fixtures","effect-pilot-synthetic.recipe.json");
        string folder=Path.Combine(Path.GetTempPath(),"yugire-effect-pilot-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.Copy(Path.Combine(Path.GetDirectoryName(fixture)!,"effect-pilot-synthetic-observations.json"),Path.Combine(folder,"effect-pilot-synthetic-observations.json"));
            var original=JsonNode.Parse(File.ReadAllText(fixture))!;
            var report=EffectPilotComparison.Compare(fixture);
            check(report.Cases.Count==2 && report.Cases.All(c=>c.Status=="Match" && c.EvidenceKind=="Synthetic") &&
                report.Cases.Sum(c=>c.Fields.Count)==21,"Effect pilot: 21 synthetic scalar fields exercise Sparks and Sword; not native evidence");
            check(EffectPilotComparison.Serialize(report)==EffectPilotComparison.Serialize(EffectPilotComparison.Compare(fixture)),
                "Effect pilot: byte-deterministic report for same recipe and pinned sources");
            string path=Path.Combine(folder,"recipe.json");
            EffectPilotComparison.Report Run(Action<JsonNode> change)
            {
                var recipe=original.DeepClone();change(recipe);File.WriteAllText(path,recipe.ToJsonString());
                return EffectPilotComparison.Compare(path);
            }
            var mismatch=Run(r=>r["cases"]![0]!["observed"]!["damageAmount"]!["pointer"]="/mismatchAmount");
            check(mismatch.Cases[0].Status=="Mismatch" && mismatch.Cases[0].Fields.Single(f=>f.Field=="damageAmount").Observed!.GetValue<int>()==201 &&
                mismatch.Cases[0].Fields.Single(f=>f.Field=="lifeAfter0").Status=="Match","Effect pilot: mismatch by field without contaminating independent LP comparison");
            var defense=Run(r=>r["cases"]![1]!["observed"]!["defenseAfter"]!["pointer"]="/mismatchDefense");
            check(defense.Cases[1].Status=="Mismatch" && defense.Cases[1].Fields.Single(f=>f.Field=="attackAfter").Status=="Match",
                "Effect pilot: ATK and DEF are compared independently");
            var unknown=Run(r=>r["cases"]![1]!["inputs"]!.AsObject().Remove("dynamicType"));
            check(unknown.Cases[1].Fields.All(f=>f.Status=="Inconclusive"),"Effect pilot: missing dynamic type never defaults to Warrior");
            var assumed=Run(r=>r["cases"]![1]!["inputs"]!["dynamicType"]=new JsonObject { ["origin"]="Assumed",["value"]=15,["reason"]="static class substituted by sandbox" });
            check(assumed.Cases[1].Fields.Single(f=>f.Field=="attackAfter") is { Status:"Inconclusive",Expected:not null },
                "Effect pilot: conditional prediction from assumption is visible but cannot Match");
            var provenance=Run(r=>r["sources"]![0]!["sha256"]=new string('0',64));
            check(provenance.Cases.All(c=>c.Status=="Inconclusive"),"Effect pilot: tampered/missing source fingerprint fails closed");
            var native=Run(r=>r["cases"]![0]!["evidenceKind"]="Native");
            check(native.Cases[0].Status=="Inconclusive","Effect pilot: synthetic source cannot become native evidence by relabeling case");
            var badPointer=Run(r=>r["cases"]![0]!["observed"]!["sourceInGraveyardAfter"]!["pointer"]="/notObserved");
            check(badPointer.Cases[0].Status=="Inconclusive" && badPointer.Cases[0].Fields.Single(f=>f.Field=="sourceInGraveyardAfter").Observed is null,
                "Effect pilot: missing graveyard remains null, not false or inferred from hand disappearance");
            var noHash=Run(r=>r["cases"]![0]!["inputs"]!.AsObject().Remove("exeSha256"));
            check(noHash.Cases[0].Fields.All(f=>f.Status=="Inconclusive"),"Effect pilot: local EXE identity cannot replace missing capture attestation");
            string commands=Path.Combine(folder,"unattested.jsonl");
            File.WriteAllText(commands,"{\"kind\":\"metadata\",\"exeSha256\":null,\"seen\":1,\"retained\":1,\"overwritten\":0}\n{\"kind\":\"command\",\"seq\":1,\"args\":[67,200,0,1]}\n");
            var unattested=Run(r=>
            {
                r["sources"]![0]!["path"]="unattested.jsonl";r["sources"]![0]!["format"]="commands";
                r["sources"]![0]!["sha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(commands))).ToLowerInvariant();
            });
            check(unattested.Cases.All(c=>c.Status=="Inconclusive") && unattested.Cases[0].Fields[0].Reason.Contains("fingerprint/integrity"),
                "Effect pilot: existing CaptureReader/Analyzer gate command sources without EXE attestation");
            var output=new StringWriter();var error=new StringWriter();
            check(Joey.Lab.Program.Run(["effect-pilot",fixture],output,error)==0 && error.ToString()=="", "Effect pilot CLI: synthetic Match exit0");
            File.WriteAllText(path,original.ToJsonString());
            var m=original.DeepClone();m["cases"]![0]!["observed"]!["damageAmount"]!["pointer"]="/mismatchAmount";File.WriteAllText(path,m.ToJsonString());
            check(Joey.Lab.Program.Run(["effect-pilot",path],new StringWriter(),new StringWriter())==1,"Effect pilot CLI: Mismatch exit1");
            m=original.DeepClone();m["cases"]![0]!["inputs"]!.AsObject().Remove("windowIsolated");File.WriteAllText(path,m.ToJsonString());
            check(Joey.Lab.Program.Run(["effect-pilot",path],new StringWriter(),new StringWriter())==3,"Effect pilot CLI: Inconclusive exit3");
            m=original.DeepClone();m["cases"]![0]!["scenario"]="BeastFangs";File.WriteAllText(path,m.ToJsonString());
            check(Joey.Lab.Program.Run(["effect-pilot",path],new StringWriter(),new StringWriter())==2,"Effect pilot CLI: no expansion to other cards");
            check(Joey.Lab.Program.Run(["effect-pilot"],new StringWriter(),new StringWriter())==2,"Effect pilot CLI: malformed request exit2");
        }
        finally { Directory.Delete(folder,true); }
    }
}
