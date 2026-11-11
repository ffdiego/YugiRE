using System.Text.Json;
using Joey.Core;

internal static class AiAttributeChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-attribute-reference.json")));
        int count=0;
        foreach (var row in reference.RootElement.GetProperty("equipment").EnumerateArray())
        {
            ushort resource=row.GetProperty("resource").GetUInt16();
            var stats=new NativeAiStats(550,row.GetProperty("typeCode").GetByte(),row.GetProperty("attributeByte").GetByte(),1900,900);
            var result=NativeAiAttributes.EquipmentDelta(resource,stats,row.GetProperty("monsterResource").GetUInt16());
            var expected=row.GetProperty("expected");
            if (expected.ValueKind==JsonValueKind.Null)
            {
                if (result.IsSupported || result.UnsupportedReasons.Count==0) throw new Exception("Unknown equipment was silently accepted");
            }
            else if (!result.IsSupported || result.Value.Attack!=expected[0].GetInt32() || result.Value.Defense!=expected[1].GetInt32())
                throw new Exception($"Equipment delta differs from Python reference for resource {resource:X4}");
            count++;
        }
        check(count==70,"70 equipment vectors agree with the pre-existing Python subset, including unknown rejection and byte/mask attribute conversion");
        foreach (var row in reference.RootElement.GetProperty("nonnegativeHalving").EnumerateArray())
        {
            int value=row.GetProperty("attack").GetInt32();
            for (int i=0;i<row.GetProperty("count").GetInt32();i++) value=NativeAiAttributes.HalfNative(value);
            if (value!=row.GetProperty("expected").GetInt32()) throw new Exception("Native half differs from Python nonnegative reference");
        }
        check(true,"28 nonnegative repeated-half vectors agree with the independent existing Python reference");
        check(NativeAiAttributes.HalfNative(-3)==-1 && NativeAiAttributes.HalfNative(-2)==0,
            "Native half uses signed truncation toward zero for negative synthetic inputs");
        check(NativeAiAttributes.HalfNative(int.MaxValue)==-214748364 && NativeAiAttributes.HalfNative(int.MinValue)==-214748364,
            "Native half retains 32-bit intermediate wrap for synthetic overflow limits");
        check(NativeAiAttributes.NormalizeResource(1999)==1999 && NativeAiAttributes.NormalizeResource(2000)==0 &&
            NativeAiAttributes.NormalizeResource(65535)==63535,"Resource normalization subtracts 2000 once only");
        check(NativeAiAttributes.EquivalentResources(0x3eb,0x40a) && NativeAiAttributes.EquivalentResources(0x3eb+2000,0x40a) &&
            !NativeAiAttributes.EquivalentResources(0x22,0x39),"Narrow aliases are preserved after resource normalization");
        var raw=new NativeAiStats(550,18,5,1900,900);
        check(!NativeAiAttributes.FromResolvedContext(0x213,raw).IsSupported &&
            !NativeAiAttributes.FromResolvedContext(0x213,raw,NativeAiAttributeContext.Unknown).IsSupported,
            "Unresolved stat/effect context is explicitly unsupported by default");
        check(!NativeAiAttributes.FromResolvedContext(0x540,new(307,15,5,2300,0),NativeAiAttributeContext.Resolved()).IsSupported,
            "Unimplemented intrinsic monster stat policy is not treated as vanilla");
        check(!NativeAiAttributes.FromResolvedContext(0x213,raw,NativeAiAttributeContext.Resolved([0xffff])).IsSupported,
            "Unknown equipment rejects the whole stat evaluation instead of becoming delta zero");
        var tail=NativeAiAttributes.ApplyResolvedTail(raw,new(300,300),1,0);
        check(tail.Attack==1200 && tail.Defense==2200,
            "0056DD30 swaps working ATK/DEF before applying accumulated deltas");
        var halved=NativeAiAttributes.ApplyResolvedTail(new(550,18,5,2001,900),default,0,2);
        check(halved.Attack==501 && halved.Defense==900,"Mirror Wall rounds individually on each application");
        var clamped=NativeAiAttributes.ApplyResolvedTail(new(550,18,5,100,100),new(500,-500),0,0,true);
        check(clamped.Attack==0 && clamped.Defense==600,"Reverse sign applies before the final nonnegative clamp");
        var over=NativeAiAttributes.ApplyResolvedTail(new(550,18,5,int.MaxValue,100),new(1,0),0,0);
        check(over.Attack==0,"Stat addition wraps at 32 bits before native signed clamp");
        var halfOver=NativeAiAttributes.ApplyResolvedTail(new(550,18,5,int.MaxValue,100),default,0,1);
        check(halfOver.Attack==-214748364,"Native tail does not invent a second clamp after halving overflow");
        var source=new ushort[]{0x291};
        var context=NativeAiAttributeContext.Resolved(source); source[0]=0xffff;
        check(NativeAiAttributes.FromResolvedContext(0x213,raw,context).Value.Attack==2200,
            "Resolved modifier manifests isolate caller-owned equipment arrays");
        check(NativeAiAttributes.HiddenOpponent(4)==new NativeAiStats(0,0,0,1000,1000) &&
            NativeAiAttributes.HiddenOpponent(5)==new NativeAiStats(0,0,0,1900,1900),
            "005ADDC0 hidden estimates erase identity/type/attribute and depend on level threshold");
        using var live=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-live-combat-events.json")));
        var observed=live.RootElement.GetProperty("event25PrecedingComputedStats").EnumerateArray().ToArray();
        var plain=NativeAiAttributes.FromResolvedContext(0x213,raw,NativeAiAttributeContext.Resolved());
        check(plain.IsSupported && plain.Value.ToBytes().SequenceEqual(Convert.FromHexString(observed[0].GetProperty("outputHex").GetString()!)),
            "Closed plain Gemini Elf stat context agrees byte-for-byte with observed native event23 buffer");
        var axe=NativeAiAttributes.FromResolvedContext(0x4d,new(24,15,5,1700,1150),NativeAiAttributeContext.Resolved());
        check(axe.IsSupported && axe.Value.ToBytes().SequenceEqual(Convert.FromHexString(observed[1].GetProperty("outputHex").GetString()!)),
            "Closed plain Axe Raider stat context agrees byte-for-byte with observed native event24 buffer");
    }
    public static void RunCatalog(Action<bool,string> check, Archive archive)
    {
        var cards=Catalog.Load(archive);
        using var live=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-live-combat-events.json")));
        var observed=live.RootElement.GetProperty("event25PrecedingComputedStats").EnumerateArray().ToArray();
        foreach (var (index,bufferIndex) in new[] {(550,0),(24,1)})
        {
            var card=cards.Single(c=>c.Index==index);
            var actual=NativeAiAttributes.FromPlainCard(card,NativeAiAttributeContext.Resolved());
            check(actual.IsSupported && actual.Value.ToBytes().SequenceEqual(Convert.FromHexString(observed[bufferIndex].GetProperty("outputHex").GetString()!)),
                $"DAT base ATK/DEF for {card.Name} independently reproduce the native buffer under a declared empty modifier context");
        }
        check(!NativeAiAttributes.FromPlainCard(cards.Single(c=>c.Index==307),5,NativeAiAttributeContext.Resolved()).IsSupported,
            "Catalog effect-monster input remains unsupported by the bounded plain-stat evaluator");
    }

}
