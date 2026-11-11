using System.Security.Cryptography;
using System.Text.Json;
using Joey.Core;

if(args.Length!=5 || args[4]!="--declared-neutral" || args[3] is not("attack" or "defense") ||
    !int.TryParse(args[1],out int attackerIndex) || !int.TryParse(args[2],out int defenderIndex))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/tools/Joey.Combat -- data.dat attackerInternalIndex defenderInternalIndex attack|defense --declared-neutral");
    Console.Error.WriteLine("Declared scenario: two face-up plain monsters, LP8000, no omitted effects/modifiers, clear rule flags. Not a runtime capture.");
    return 2;
}
try
{
    using(var stream=File.OpenRead(args[0]))
        if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(CpuDeckTraceVerifier.DatSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsupported DAT fingerprint");
    using var archive=new Archive(args[0]);var cards=Catalog.Load(archive);
    var attacker=cards.SingleOrDefault(c=>c.Index==attackerIndex);var defender=cards.SingleOrDefault(c=>c.Index==defenderIndex);
    if(attacker is null || defender is null)throw new InvalidDataException("Internal card index absent from catalog");
    var a=NativeAiAttributes.FromPlainCard(attacker,NativeAiAttributeContext.Resolved());
    var d=NativeAiAttributes.FromPlainCard(defender,NativeAiAttributeContext.Resolved());
    if(!a.IsSupported || !d.IsSupported)
    {
        Console.WriteLine(JsonSerializer.Serialize(new {IsSupported=false,UnsupportedReasons=a.UnsupportedReasons.Concat(d.UnsupportedReasons)},new JsonSerializerOptions{WriteIndented=true}));
        return 2;
    }
    var own=new NativeAiMonster(a.Value,attacker.Id,Level:attacker.Level,RuleFlags:0);
    var rival=new NativeAiMonster(d.Value,defender.Id,DefensePosition:args[3]=="defense",Level:defender.Level,RuleFlags:0);
    var outcome=NativeAiCombat.Evaluate(own,rival,0,0,NativeAiCombatContext.ResolvedNoExternalModifiers).Value!;
    var sequence=NativeAiSequence.Evaluate(NativeAiField.Empty.With(0,0,own).With(1,0,rival),0,
        NativeAiFieldContext.ResolvedPlain(8000,8000)).Value!;
    Console.WriteLine(JsonSerializer.Serialize(new {
        Scope="C# calculation from pinned DAT in an explicitly declared neutral scenario; not a native runtime replay",
        Attacker=new{attacker.Index,ResourceId=attacker.Id,attacker.Name,Stats=a.Value},
        Defender=new{defender.Index,ResourceId=defender.Id,defender.Name,Stats=d.Value,Position=args[3]},
        Event=new{Hex=Convert.ToHexString(outcome.ToBytes()),outcome.Score,outcome.Useful,outcome.AttackerRemoved,outcome.DefenderRemoved},
        Sequence=new{sequence.Score,sequence.ActionMask,sequence.OwnLosses,sequence.OpponentLosses,
            sequence.OwnSurvivors,sequence.OpponentSurvivors,sequence.RankingSummary,
            Events=sequence.Events.Select(e=>Convert.ToHexString(e.ToBytes()))}
    },new JsonSerializerOptions{WriteIndented=true}));
    return 0;
}
catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException)
{
    Console.Error.WriteLine("Unsupported input: "+error.Message);return 2;
}
