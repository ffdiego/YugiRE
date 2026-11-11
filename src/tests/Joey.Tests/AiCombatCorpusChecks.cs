using Joey.Core;
using System.Text.Json;

internal static class AiCombatCorpusChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-combat-differential.json")));
        var root=fixture.RootElement;
        check(root.GetProperty("coverage").GetProperty("fullyObservedContexts").GetInt32()==0,
            "Native corpus does not fabricate any fully observed combat context");
        var matches=new List<int>();var differences=new List<int>();
        var declaredOutputs=new Dictionary<int,string>();int missing=0;
        foreach(var row in root.GetProperty("events").EnumerateArray())
        {
            if(row.GetProperty("defender").ValueKind==JsonValueKind.Null){missing++;continue;}
            NativeAiMonster FromObserved(JsonElement item) => new(
                NativeAiStats.Read(Convert.FromHexString(item.GetProperty("raw_hex").GetString()!)),
                item.GetProperty("resource_id").GetUInt16(),DefensePosition:false,FaceUp:true);
            // ONE uniform declared scenario for EVERY complete numeric pair:
            // face-up attack, no external battle modifiers, canonical seed0.
            // Never select a different position/effect based on the expected output.
            var result=NativeAiCombat.Evaluate(FromObserved(row.GetProperty("attacker")),FromObserved(row.GetProperty("defender")),
                row.GetProperty("attackerSlot").GetInt32(),row.GetProperty("targetSlot").GetInt32(),
                NativeAiCombatContext.ResolvedNoExternalModifiers).Value!;
            var native=NativeAiCombatEvent.Read(Convert.FromHexString(row.GetProperty("expectedBufferHex").GetString()!));
            int seq=row.GetProperty("eventSeq").GetInt32();
            declaredOutputs[seq]=Convert.ToHexString(result.ToBytes());
            if(((result.Flags^native.Flags)&0xffa)==0 && result.Score==native.Score) matches.Add(seq);else differences.Add(seq);
        }
        check(matches.SequenceEqual(new[]{25,111,123,131,157,162,179,184,193,310,822,1029}),
            "12 native buffers agree under one declared neutral attack-position scenario; this is conditional, not full-context replay");
        check(differences.SequenceEqual(new[]{304,319}) && missing==8,
            "Two scenario divergences and eight missing numeric contexts remain explicit; no output-fitted context");
        check(declaredOutputs[304]=="00040CFE" && declaredOutputs[319]=="2004D4FE",
            "Declared neutral attack scenario predicts -500/-300 own losses where native buffers contain zero-score rival removals; missing inputs remain unresolved");

        // Separate counterfactual experiment motivated by the user's observation:
        // Goblin Attack Force may have switched to defense after attacking.
        // This does NOT amend the fixture or the uniform-scenario coverage above.
        foreach(var row in root.GetProperty("events").EnumerateArray())
        {
            int seq=row.GetProperty("eventSeq").GetInt32();
            if(seq is not(304 or 319))continue;
            NativeAiMonster Observed(JsonElement item,bool defense) => new(
                NativeAiStats.Read(Convert.FromHexString(item.GetProperty("raw_hex").GetString()!)),
                item.GetProperty("resource_id").GetUInt16(),DefensePosition:defense,FaceUp:true);
            var defender=row.GetProperty("defender");
            var result=NativeAiCombat.Evaluate(Observed(row.GetProperty("attacker"),false),Observed(defender,true),
                row.GetProperty("attackerSlot").GetInt32(),row.GetProperty("targetSlot").GetInt32(),
                NativeAiCombatContext.ResolvedNoExternalModifiers).Value!;
            check(defender.GetProperty("internal_card_index").GetInt32()==307 &&
                Convert.ToHexString(result.ToBytes())==row.GetProperty("expectedBufferHex").GetString(),
                $"Hypothesis only: event{seq} exactly agrees if observed Goblin Attack Force stats are evaluated in defense; posture remains uncaptured");
        }
    }
}
