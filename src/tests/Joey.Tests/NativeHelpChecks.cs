using Joey.LookAndFeel;

internal static class NativeHelpChecks
{
    public static void Run(Action<bool,string> check)
    {
        var manifest = string.Join("\n", Enumerable.Range(1,24)
            .Select(i => $"help{i:00}_?.yga\t// comment"));
        const string invalidTarget = @"j\duel\help\help10_e.yga";
        check(NativeHelpResources.ResolveInvalidTarget(manifest, [invalidTarget]) == invalidTarget,
            "V29: invalid target uses original key 9, not generic hover");
        bool rejected = false;
        try { NativeHelpResources.ResolveInvalidTarget(manifest.Replace("help10_?", "help09_?"), [invalidTarget]); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "V29: reject unexpected manifest position");
        rejected = false;
        try { NativeHelpResources.ResolveInvalidTarget(manifest, []); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "V29: no placeholder for missing original asset");

        const string tribute = @"j\duel\help\help01_e.yga";
        check(NativeHelpResources.ResolveTributeRequired(manifest, [tribute]) == tribute,
            "V29: tribute key 0 resolves first original help image");
        rejected = false;
        try { NativeHelpResources.ResolveTributeRequired(manifest.Replace("help01_?", "help02_?"), [tribute]); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "V29: reject wrong tribute asset at manifest position");

        var help = new NativeHelpTimeline();
        var local = new NativeHelpTarget((ushort)((3 << 6) | (5 << 1)));
        var opponent = new NativeHelpTarget((ushort)(local.Word | 1));
        int[] c = new int[512];
        c[384] = 0; c[416] = 16384; c[448] = 32768; c[480] = 49152;
        void Step(int key, NativeHelpTarget target, bool enabled = true,
            bool suppressed = false, bool gate = true, bool alternate = false) =>
            help.AdvanceUpdate(key, target, 0, enabled, suppressed, gate, alternate);

        check(!help.Visible && help.Sample(0,0,80,80,101,41,c) is null,
            "V29: hidden initially");
        Step(3,opponent);
        check(!help.Visible,"V29: ordinary requests require local side");
        Step(3,local,gate:false);
        check(!help.Visible,"V29: both native predicates false");
        Step(3,local,gate:false,alternate:true);
        check(help.Visible && help.Counter==0 && help.Target.Index==3 &&
            help.Target.Zone==5,"V29: alternate predicate opens with raw target");
        check(help.Sample(790,100,80,80,101,41,c)==new CardBounds(700,40,101,0),
            "V29: right clamp, opening height zero");
        for(int i=1;i<=4;i++)
        {
            Step(3,local);
            int h=41*i/4;
            check(help.Counter==i*4 &&
                help.Sample(790,100,80,80,101,41,c)==new CardBounds(700,40-h/2,101,h),
                "V29: native vertical opening without Draw mutation");
        }
        check(help.Sample(0,10,80,80,101,41,c)==new CardBounds(-50,1,101,41),
            "V29: top clamp and no left clamp in native Draw");
        Step(3,new NativeHelpTarget((ushort)(local.Word|0xc000)));
        check(help.Visible,"V29: top two descriptor bits ignored");
        Step(3,new NativeHelpTarget((ushort)(local.Word ^ 0x40)));
        check(!help.Visible,"V29: identity change closes");
        Step(9,opponent);
        check(help.Visible && help.MessageKey==9,"V29: special key 9 accepts opponent");
        Step(4,local);
        check(!help.Visible,"V29: changed key closes without same-update reopen");
        Step(4,local);
        for(int i=0;i<70;i++) Step(4,local);
        check(help.Counter==256 && help.Visible,"V29: counter saturates");
        Step(-1,local);
        check(!help.Visible,"V29: request must be renewed");
        Step(4,local,enabled:false);
        check(!help.Visible,"V29: native enable gate");
        var hand = new NativeHelpTarget(11 << 1);
        Step(4,hand);
        for(int i=0;i<4;i++) Step(4,hand);
        check(help.Sample(200,150,80,100,100,40,c)==new CardBounds(150,90,100,40),
            "V29: hand source is halved before anchor subtraction");
        Step(4,hand,suppressed:true);
        check(!help.Visible,"V29: native suppression closes");

        // The Proton capture used 0x8117 (side 1, region 11, index 4).
        // The client's descriptor may omit the top flag without changing identity.
        var tributeHand = new NativeHelpTarget(0x8117);
        help.AdvanceUpdate(0, tributeHand, 1, true, false, true, false);
        check(help.Visible && help.MessageKey == 0 && help.Target.Zone == 11 &&
            help.Target.Index == 4 && help.Target.Identity == 0x117,
            "V29: tribute key zero is a real request, not the sentinel");
        for (int i=0; i<4; i++)
            help.AdvanceUpdate(0, new NativeHelpTarget(0x117), 1, true, false, true, false);
        check(help.Sample(525,568,290,72,179,38,c) == new CardBounds(436,458,179,38),
            "V29: tribute uses native large card height and hand anchor");
        help.AdvanceUpdate(-1, tributeHand, 1, true, false, true, false);
        check(!help.Visible, "V29: leaving hand stops the tribute request");
    }
}
