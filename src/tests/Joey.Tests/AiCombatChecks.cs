using Joey.Core;
using System.Text.Json;

internal static class AiCombatChecks
{
    public static NativeAiMonster Monster(int attack, int defense=0, ushort resource=0x213,
        bool defensePosition=false, bool faceUp=true, int level=4, byte type=0, byte attribute=0, ushort index=1) =>
        new(new(index,type,attribute,attack,defense),resource,defensePosition,faceUp,level);

    public static void Run(Action<bool,string> check)
    {
        var neutral=NativeAiCombatContext.ResolvedNoExternalModifiers;
        void Event(string label, NativeAiMonster a, NativeAiMonster d, string expected,
            NativeAiCombatContext? context=null, int aSlot=0, int dSlot=0)
        {
            var result=NativeAiCombat.Evaluate(a,d,aSlot,dSlot,context??neutral);
            check(result.IsSupported && Convert.ToHexString(result.Value!.ToBytes())==expected,"Native combat " + label);
        }
        check(!NativeAiCombat.Evaluate(Monster(1900),Monster(1700),0,0,new()).IsSupported,
            "Unknown combat predicates cannot silently become no effects");
        Event("ATK>ATK, native event25 numeric inputs under declared neutral context",Monster(1900,index:550),Monster(1700,1150,resource:0x4d,index:24),"0808C800");
        Event("ATK<ATK signed negative",Monster(1700),Monster(1900),"000438FF");
        Event("positive ATK tie removes both, not useful",Monster(1700),Monster(1700),"000C0000");
        Event("zero ATK tie removes neither",Monster(0),Monster(0),"00000000");
        Event("ATK>DEF kills for score zero",Monster(2000),Monster(0,1500,defensePosition:true),"08080000");
        Event("ATK<DEF loss without destruction",Monster(1500),Monster(0,2000,defensePosition:true),"00000CFE");
        Event("ATK=DEF no destruction",Monster(1500),Monster(0,1500,defensePosition:true),"00000000");
        Event("int16 positive difference truncates to negative but free kill useful",Monster(40000),Monster(0),"0808409C");
        Event("scratch high bits and selected bit preserved",Monster(1900),Monster(1700),"0DF8C800",neutral with{InitialFlags=0xf005});
        Event("both slots encoded",Monster(1900),Monster(1700),"280AC800",aSlot:2,dSlot:4);
        Event("piercing score",Monster(1900),Monster(0,1000,defensePosition:true),"08088403",neutral with{PierceToOpponent=true});
        Event("reverse piercing negative score with free kill still useful",Monster(1900),Monster(0,1000,defensePosition:true),"08087CFC",neutral with{PierceToAttacker=true});
        Event("native intrinsic piercing 053D",Monster(1900,resource:0x53d),Monster(0,1000,defensePosition:true),"08088403");
        Event("Metalmorph adds ceil-half of enemy ATK",Monster(1000),Monster(1701),"08089600",neutral with{MetalmorphCount=1});
        Event("enemy 011F halves DEF against type1",Monster(1000,type:1),Monster(100,1001,resource:0x11f,defensePosition:true),"08080000");
        Event("own side protection",Monster(1000),Monster(1900),"00000000",neutral with{AttackerSideProtected=true});
        Event("enemy protection clears destruction and gain",Monster(1900),Monster(1000),"00000000",neutral with{DefenderSideProtected=true});
        Event("damage suppression preserves useful kill",Monster(1900),Monster(1000),"08080000",neutral with{SuppressDamageToDefender=true});
        Event("0199 overrides earlier enemy protection",Monster(1900,resource:0x199),Monster(1000,attribute:2),"08080000",neutral with{DefenderSideProtected=true});
        Event("04CF forces 500 despite weaker attacker",Monster(100,resource:0x4cf),Monster(1000),"0800F401");
        Event("04CF against defense forces score0",Monster(100,resource:0x4cf),Monster(100,1000,defensePosition:true),"00000000");
        Event("04E3 clears own destruction at enemyATK1900",Monster(1000,resource:0x4e3),Monster(1900),"00007CFC");
        Event("01DD +1000 against type6",Monster(1000,resource:0x1dd),Monster(1700,type:6),"08082C01");
        Event("04E5 own +2000 against type15",Monster(1000,resource:0x4e5),Monster(1700,type:15),"08081405");
        var hidden=Monster(9999,9999,resource:0x11f,faceUp:false,defensePosition:true,index:24,type:1,attribute:2);
        check(hidden.OpponentStats==new NativeAiStats(0,0,0,1000,1000),"Hidden low-level opponent projection erases identity and uses1000");
        check((hidden with{Level=5}).OpponentStats==new NativeAiStats(0,0,0,1900,1900),"Hidden high-level opponent projection uses1900");
        Event("hidden card does not leak intrinsic 011F",Monster(900,type:1),hidden,"00009CFF");
        var stats=NativeAiStats.Read(Convert.FromHexString("260212056C07000084030000"));
        check(stats==new NativeAiStats(550,18,5,1900,900) && Convert.ToHexString(stats.ToBytes())=="260212056C07000084030000","Native stats12byte round trip");
        var special=NativeAiCombat.SpecialTarget(1000,0) with {Flags=(ushort)(NativeAiCombat.SpecialTarget(1000,0).Flags|4)};
        check(Convert.ToHexString(special.ToBytes())=="0600E803" && special.SelectionReturn && !special.Useful,
            "Observed selected special event0600E803 generated without recomputing utility");
        check(!NativeAiCombat.SpecialTarget(32768,0).SelectionReturn,"Special branch checks int16 ATK positivity");
        check(Convert.ToHexString(NativeAiCombat.SpecialTarget(1000,4,0xffff).ToBytes())=="CFF3E803",
            "Special descriptor preserves native opaque target/scratch bits");
        // Format/utility corpus: expected buffers were observed, never generated by this evaluator.
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-live-combat-events.json")));
        int observed=0;
        foreach(var row in fixture.RootElement.GetProperty("events").EnumerateArray())
        {
            string hex=row.GetProperty("outputHex").GetString()!;
            var value=NativeAiCombatEvent.Read(Convert.FromHexString(hex));
            check(Convert.ToHexString(value.ToBytes())==hex && value.Score==row.GetProperty("expectedSignedScore").GetInt32(),
                $"Observed native event {row.GetProperty("seq").GetInt32()} format/signed oracle");
            check(value.Useful==(value.Score>0 || (value.DefenderRemoved&&!value.AttackerRemoved)),
                $"Observed native event {row.GetProperty("seq").GetInt32()} utility oracle");
            observed++;
        }
        check(observed==22,"All22 recorded combat proposals checked; this is not a full-context replay");
    }
}
