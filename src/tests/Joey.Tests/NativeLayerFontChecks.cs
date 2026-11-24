using Joey.LookAndFeel;

internal static class NativeLayerFontChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        var a = new NativeLayerFontA();
        var b = new NativeLayerFontB();
        int[] cosine = new int[512];
        for (int i=0;i<512;i++) cosine[i]=i*256;
        check(a.Intensity(cosine)==384 && a.Counter==0,"V27: sample does not mutate Draw clock");
        for (int i=1;i<=64;i++)
        {
            a.AdvanceNativeDraw();
            int t=i*4&255;
            int intensity=t<64 ? (2*t-128)&511 : t<=192 ? 256 : (2*t+128)&511;
            check(a.Counter==t && a.Intensity(cosine)==intensity,"V27: pulse with asymmetric table");
        }
        check(NativeLayerFontA.Bounds(233,30)==new CardBounds(555,558,233,30),
            "V27: bottom-right anchor");
        check(NativeLayerFontA.CreatedForMode(5) && NativeLayerFontA.CreatedForMode(7) &&
            !NativeLayerFontA.CreatedForMode(3) && NativeLayerFontB.CreatedForMode(3),
            "V27/V28: distinct mode gates");
        for(int i=1;i<=64;i++)
        {
            b.AdvanceNativeDraw(true);
            check(b.Value==i*4 && b.Visible==(i>56),"V28: enter only above 224");
        }
        b.AdvanceNativeDraw(true);
        check(b.Value==256,"V28: upper saturation");
        int[] realShape = new int[512];
        realShape[0]=65536;
        realShape[496]=32768;
        check(b.Top(34,realShape)==16,"V28: final vertical anchor");
        b.AdvanceNativeDraw(false);
        check(b.Top(34,realShape)==-9,"V28: signed interpolation");
        for(int i=0;i<63;i++) b.AdvanceNativeDraw(false);
        check(b.Value==0 && !b.Visible && b.Top(34,realShape) is null,"V28: fade away");
        check(NativeLayerFontB.NativeDecisionPredicate(0,0,0,1u<<21) &&
              !NativeLayerFontB.NativeDecisionPredicate(0x4000,0,0,1u<<21) &&
              NativeLayerFontB.NativeDecisionPredicate(0,0x11,0,0) &&
              !NativeLayerFontB.NativeDecisionPredicate(0,0x10,0,0) &&
              !NativeLayerFontB.NativeDecisionPredicate(0,0,0,15u<<21),
              "V28: decision predicate and signed bitfield");
        string[] paths=[NativeLayerFontResources.Prefix+"demo_font_e.bmp",NativeLayerFontResources.Prefix+"wait_e.bmp"];
        check(NativeLayerFontResources.Parse("demo_font_?.bmp\nwait_?.bmp\n",paths.Reverse()).SequenceEqual(paths),
            "V27/V28: resolve English slots in manifest order");
        reject(()=>NativeLayerFontResources.Parse("demo_font_?.bmp",paths),"V27/V28: missing slot");
        reject(()=>NativeLayerFontResources.Parse("demo_font_?.bmp\n../wait_?.bmp",paths),"V27/V28: invalid path");
    }
}
