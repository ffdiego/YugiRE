using Joey.LookAndFeel;

internal static class NativeLifeDeltaChecks
{
    public static void Run(Action<bool, string> check)
    {
        for (int attacker = 0; attacker <= 1; attacker++)
        for (int side = 0; side <= 1; side++)
        for (uint flags = 0; flags < 32; flags++)
        {
            var request = NativeLifeDeltaRequest.Battle(side, attacker, 2, 4, flags, 250);
            check(request.HasValue && request.Value.Delta == -250 &&
                  request.Value.HoldConsumption &&
                  request.Value.AnchorSlot == ((flags & 0x18) != 0 ? -1 : side == attacker ? 2 : 4),
                  "V06: affected side and raw battle flags select slot");
        }
        check(NativeLifeDeltaRequest.Battle(1, 0, 2, -1, 0, 500)?.AnchorSlot == -1 &&
              NativeLifeDeltaRequest.Battle(0, 0, 2, -1, 0, 500)?.AnchorSlot == -1 &&
              NativeLifeDeltaRequest.Battle(1, 0, 2, 4, 0, 0) is null,
              "V06: direct or no damage sentinel");
        // 004806F0: reference values from the captured 512-entry cosine table.
        var cosine=NativeCosineCapture.GenerateTable();
        IReadOnlyList<NativeFloatingLifeGlyph> Digits(int counter,int delta=-10,int columns=12,bool plus=true) =>
            NativeFloatingLifeDigits.Sample(delta,counter,16,40,columns,cosine,plus);
        check(Digits(0).Count==0 && Digits(31).Count==1,
            "V06: global zero and negative individual phases are skipped");
        check(Digits(32).SequenceEqual(new NativeFloatingLifeGlyph[] {
            new(0,-16,-28,32),new(1,-32,0,0) }),
            "V06: a delayed glyph at exact phase zero is visible");
        check(Digits(64).SequenceEqual(new NativeFloatingLifeGlyph[] {
            new(0,-16,-40,64),new(1,-32,-28,32),new(11,-48,0,0) }),
            "V06: minus enters at phase zero; first digit reaches full height");
        check(Digits(64,10)[2].GlyphIndex==10 && Digits(64,10,12,false).Count==2 &&
            Digits(64,-10,10).Count==2 && Digits(64,0).Count==0,
            "V06: native sign columns and zero-delta gate");
        check(Digits(128)[0].YOffsetPixels==0 && Digits(160)[0].YOffsetPixels==-10 &&
            Digits(192)[0].YOffsetPixels==0 && Digits(224)[2].YOffsetPixels==-10,
            "V06: two cosine excursions and staggered rest");
        check(Digits(255).All(g=>g.YOffsetPixels==0) && Digits(255)[2].Phase==191 &&
            Digits(256).All(g=>g.YOffsetPixels==0 && g.Phase==256) &&
            Digits(512).All(g=>g.YOffsetPixels==0 && g.Phase==512),
            "V06: static dispatch bypasses stagger and accepts full native ramp");
        for (int counter=16;counter<=512;counter+=16)
        {
            var glyphs=Digits(counter,-1234);
            check(glyphs.All(g=>g.YOffsetPixels is >= -40 and <= 0) &&
                glyphs.Select(g=>g.XOffsetPixels).SequenceEqual(
                    Enumerable.Range(1,glyphs.Count).Select(i=>-16*i)),
                "V06: native ramp progression uses right-to-left unscaled glyphs");
        }
        foreach(bool near in new[]{true,false})
        {
            var general=NativeLifeHud.FloatingNumberAnchor(near);
            var origin=NativeLifeHud.FloatingNumberOrigin(general,-10,16,40,12);
            check(general==new CardPoint(525,near?460:140) &&
                origin==new CardPoint(549,near?440:120),
                "V06: recipe 33 general delta centered on spell row");
            for(int slot=0;slot<5;slot++)
            {
                var anchor=NativeLifeHud.FloatingNumberAnchor(near,slot);
                check(anchor==new CardPoint(369+78*(near?slot:4-slot),near?352:248),
                    "V06: recipe 33 affected monster slot center");
            }
        }
        var lp = new NativeLifePointPresentation(8000);
        check(lp.RequestNativeChange(new(-250,4,true)) == 0x10000009u &&
              lp.AnchorSlot == 4 && lp.HoldConsumption && lp.RampCounter == 0,
              "V06: native setup preserves source and requests sound");
        for (int i=0;i<32;i++) lp.AdvanceNativeUpdate();
        for (int i=0;i<40;i++) lp.AdvanceNativeUpdate();
        check(lp.Displayed == 8000 && lp.PendingDelta == -250 && lp.RampCounter == 256,
              "V06: ramp runs but consumption waits indefinitely");
        check(lp.RequestNativeChange(new(900,-1,true)) is null &&
              !lp.HoldConsumption && lp.PendingDelta == -250 &&
              lp.AnchorSlot == 4 && lp.RampCounter == 256,
              "V06: second request clears hold without replacement");
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7900 && lp.PendingDelta == -150,
              "V06: released consumption starts at next update");
        lp.CancelNativeChange();
        check(lp.Displayed == 7900 && lp.PendingDelta == 0 &&
              lp.RampCounter == 256 && lp.AnchorSlot == 4 && !lp.HoldConsumption,
              "V06: native cancellation retains visible state");
        check(lp.RequestNativeChange(NativeLifeDeltaRequest.General(205,false)) == 0x1000001fu &&
              lp.AnchorSlot == -1 && lp.RampCounter == 0,
              "V06: general change resets ramp and uses sentinel");
        for(int i=0;i<32;i++) lp.AdvanceNativeUpdate();
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 8000 && lp.PendingDelta == 105,
              "V06: no hold on general stage three");
        lp.Reset(1000);
        check(lp.AnchorSlot == -1 && !lp.HoldConsumption && lp.PendingDelta == 0,
              "V06: reset clears native input state");
        lp.SetLogicalLife(900);
        for(int i=0;i<33;i++) lp.AdvanceNativeUpdate();
        check(lp.Displayed == 900 && lp.PendingDelta == 0,
              "V06: legacy adapter remains compatible");
    }
}
