using Joey.LookAndFeel;

internal static class NativeReactionPlaybackChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(NativeReaction07.IsGenericSpellActivation(1429, 22, 4u << 17) &&
            NativeReaction07.IsGenericSpellActivation(342, 22, 0) &&
            !NativeReaction07.IsGenericSpellActivation(342, 22, 2u << 17) &&
            !NativeReaction07.IsGenericSpellActivation(342, 21, 0) &&
            !NativeReaction07.IsGenericSpellActivation(0x420, 22, 0),
            "V31 activation reaction selects the generic Spell branch only");
        var single=new NativeReactionPlayback(0x1000003a);
        var first=single.Step(true);
        check(first.Counter==8 && first.RequestedSound==0x1000003a && first.Active,
            "V31 primary request at first Draw even with audio disabled");
        for(int i=2;i<32;i++) single.Step(true);
        check(single.Counter==248 && single.Active && single.Step(true).Counter==256 &&
            !single.Active && single.Step(true).Counter==256,"V31 disabled audio ends at 256");
        single.Reset(0x1000003a);
        for(int i=0;i<62;i++) single.Step(false);
        check(single.Counter==496 && single.Active && !single.Step(true).Active && single.Counter==504,
            "V31 single voice holds beyond minimum until predicate");
        var dual=new NativeReactionPlayback(0x10000028,0x1000002b);
        check(dual.Step(false).RequestedSound==0x10000028,"V31 two-voice primary");
        for(int i=0;i<108;i++) dual.Step(false);
        check(dual.Counter==872 && dual.SecondarySound==0x1000002b && dual.Active,
            "V31 observed 108 unsuccessful polls");
        var promote=dual.Step(true);
        check(promote.Counter==880 && promote.PrimarySound==0x1000002b &&
            promote.SecondarySound==0 && promote.RequestedSound==0x1000002b && promote.Active,
            "V31 second request promotion without same-Draw termination");
        while(dual.Counter<1512) dual.Step(false);
        check(!dual.Step(true).Active && dual.Counter==1520,"V31 observed second voice end");
        dual.Reset(0x10000028,0x1000002b);
        dual.Step(true);
        check(dual.Step(true).RequestedSound==0x1000002b && dual.Counter==16 && dual.Active,
            "V31 immediate predicate promotes second voice (model, not observed disabled two-voice case)");

        const string text="reaction/base.bmp 450040\n- 0\n- 326000\n- 508\n- 326508\n- 1\n"+
            "reaction/mouth1.bmp 122224\nreaction/mouth2.bmp 122224\n- 2\n"+
            "reaction/eyes1.bmp 91179\nreaction/eyes2.bmp 91179\n";
        var names=new[]{"base","mouth1","mouth2","eyes1","eyes2"}.Select(n=>
            NativeJoeyCinematicRouting.Prefix+"reaction\\"+n+".bmp").ToArray();
        var reaction=NativeReaction07.Parse(text,names);
        for(uint state=0;state<32;state++)
        {
            var actual=reaction.Sample(state);
            int offset=((state>>1)&3) switch {0=>-1,2=>1,_=>0};
            int[] expected=offset<0?[]:(state&0x18)!=0?[6+offset]:[6+offset,9+offset];
            check(actual.Select(x=>x.Key).SequenceEqual(expected) &&
                actual.All(x=>x.Position==(x.Key<9?new CardPoint(572,264):new CardPoint(541,219))),
                $"V31 layer selection from scene state {state}");
        }
        bool rejected=false;
        try { NativeReaction07.Parse(text.Replace("- 2\n","- 3\n"),names); }
        catch(InvalidDataException) { rejected=true; }
        check(rejected,"V31 unknown selector rejected, not guessed");
    }
}
