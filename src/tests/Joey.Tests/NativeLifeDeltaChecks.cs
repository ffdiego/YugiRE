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
