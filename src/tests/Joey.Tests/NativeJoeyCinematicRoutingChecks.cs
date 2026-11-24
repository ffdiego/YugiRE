using Joey.LookAndFeel;

internal static class NativeJoeyCinematicRoutingChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        var r=NativeJoeyCinematicRouting.Summon(true,0x51,2400,1);
        check(r.Kind==NativeJoeySummonKind.Signature&&
            r.SignatureManifest==@"j\bust_up\summon.txt"&&r.ImmediateSound==0x2000000b&&
            !r.UsesPresentationRandom,"V31: signature bypasses ordinary reaction/RNG");
        check(NativeJoeyCinematicRouting.Summon(true,0x2e8,0,5).SignatureManifest==
            @"j\bust_up\summon2.txt","V31: second signature ID, identity not assumed");
        check(NativeJoeyCinematicRouting.Summon(false,0x51,2400,1).ReactionNumber==1,
            "V31: signature path applies only to selected native side");
        check(NativeJoeyCinematicRouting.Summon(false,1,1990,1).Kind==NativeJoeySummonKind.SoundOnly&&
            NativeJoeyCinematicRouting.Summon(false,1,2000,2).ReactionNumber==1,
            "V31: opposite-side threshold");
        check(NativeJoeyCinematicRouting.Summon(false,1,100,4).ImmediateSound==0x10000058&&
            NativeJoeyCinematicRouting.Summon(false,1,100,5).PrimarySound==0x10000055,
            "V31: other native event types");
        check(NativeJoeyCinematicRouting.Summon(true,1,1840,1).Kind==NativeJoeySummonKind.None,
            "V31: printed ATK below threshold");
        reject(()=>NativeJoeyCinematicRouting.Summon(true,1,1850,1),"V31: RNG sample required only for strong branch");
        var strong=NativeJoeyCinematicRouting.Summon(true,1,1850,1,0);
        check(strong.ReactionNumber==13&&strong.PrimarySound==0x10000028&&
            strong.SecondarySound==0x1000002c&&strong.ImmediateSound==0x2000000c&&strong.UsesPresentationRandom,
            "V31: strong ordinary summon has two sound requests and immediate family-2 cue");
        strong=NativeJoeyCinematicRouting.Summon(true,1,1850,2,1);
        check(strong.PrimarySound==0x1000002a&&strong.SecondarySound==0x1000002b,
            "V31: event type and RNG bit choose different sounds");
        var jinzo=NativeJoeyCinematicRouting.Summon(true,0x2ef,2400,1,99);
        check(jinzo.ReactionNumber==8&&jinzo.PrimarySound==0x10000065&&
            jinzo.SecondarySound==0&&jinzo.UsesPresentationRandom,
            "V31: Jinzo override still consumes RNG, suppresses secondary sound");
        var alternate=NativeJoeyCinematicRouting.Summon(true,0x2f9,1900,2,0);
        check(alternate.ReactionNumber==8&&alternate.PrimarySound==0x10000029&&
            alternate.SecondarySound==0x1000002e,"V31: alternate strong reaction");
        check(NativeJoeyCinematicRouting.Summon(true,0x44,1000,3).ReactionNumber==2&&
            NativeJoeyCinematicRouting.Summon(true,0xf,1000,3).PrimarySound==0x10000061&&
            NativeJoeyCinematicRouting.Summon(true,0x52e,1000,3).PrimarySound==0x1000004d,
            "V31: explicit ID overrides versus type 3 fallback");
        check(NativeJoeyCinematicRouting.Result(1)==(@"j\bust_up\win.txt",0x1000005du)&&
            NativeJoeyCinematicRouting.Result(2)==(@"j\bust_up\lose.txt",0x10000059u)&&
            NativeJoeyCinematicRouting.Result(3)==(@"j\bust_up\draw.txt",0x1000005cu),
            "V31: outcome selects bust manifest and voice");
        check(NativeJoeyCinematicRouting.StartVariant(5)==2&&
            NativeJoeyCinematicRouting.ResultVariant(1000,null)==0&&
            NativeJoeyCinematicRouting.ResultVariant(1005,17)==2,
            "V31: start/result variants, no random for zero divisor");
        reject(()=>NativeJoeyCinematicRouting.ResultVariant(1005,null),
            "V31: result RNG sample required for nonzero divisor");
    }
}
