using Joey.LookAndFeel;

internal static class NativeSelectionOriginChecks
{
    public static void Run(Action<bool,string> check)
    {
        for (int perspective=0;perspective<2;perspective++)
        {
            int own=perspective, other=1-perspective;
            check(NativeSelectionOrigin.Resolve(1,own,perspective)==new CardPoint(284,460) &&
                  NativeSelectionOrigin.Resolve(1,other,perspective)==new CardPoint(766,140),
                "V11: descriptor C native table");
            check(NativeSelectionOrigin.Resolve(2,own,perspective,7)==new CardPoint(767,460) &&
                  NativeSelectionOrigin.Resolve(2,other,perspective,7)==new CardPoint(283,140),
                "V11: descriptor D requires signed live adjustment");
            check(NativeSelectionOrigin.Resolve(3,own,perspective)==new CardPoint(760,366) &&
                  NativeSelectionOrigin.Resolve(3,other,perspective)==new CardPoint(290,234) &&
                  NativeSelectionOrigin.Resolve(4,own,perspective)==new CardPoint(762,308) &&
                  NativeSelectionOrigin.Resolve(4,other,perspective)==new CardPoint(292,292),
                "V11: descriptors E/F native table");
        }
        bool rejected=false;
        try { _=NativeSelectionOrigin.Resolve(2,0,0); }
        catch (ArgumentException) { rejected=true; }
        check(rejected,"V11: no invented live origin adjustment");
    }
}
