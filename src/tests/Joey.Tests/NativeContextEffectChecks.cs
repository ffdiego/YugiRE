using Joey.LookAndFeel;

internal static class NativeContextEffectChecks
{
    public static void Run(Action<bool, string> check)
    {
        var chain = new NativeChainTimeline(3);
        check(chain.Active && chain.BlocksRelease && chain.SpriteSelector is null &&
            chain.Layer == 0x123, "V23: creation and layer");
        for (int i = 1; i <= 32; i++)
        {
            chain.AdvanceNativeUpdate();
            check(chain.Counter == i * 4 && chain.SpriteSelector == Math.Min(i,25),
                "V23: opening selector");
        }
        check(chain.Active && !chain.BlocksRelease && chain.HasNumberPass,
            "V23: held object visible but predicate false");
        for (int i=0; i<40; i++) chain.AdvanceNativeUpdate();
        check(chain.Counter == 128 && chain.SpriteSelector == 25, "V23: hold indefinitely");
        chain.Release();
        check(chain.BlocksRelease, "V23: release makes predicate true before update");
        for (int i = 1; i < 16; i++)
        {
            chain.AdvanceNativeUpdate();
            check(chain.Counter == 128 + 4*i && chain.SpriteSelector == Math.Max(1,15-i),
                "V23: closing selector");
        }
        chain.AdvanceNativeUpdate();
        check(chain.Counter == 192 && !chain.Active && !chain.BlocksRelease &&
            chain.SpriteSelector is null, "V23: deactivation");
        var early = new NativeChainTimeline(0);
        early.Release();
        for (int i=0;i<48;i++) early.AdvanceNativeUpdate();
        check(!early.Active && !early.HasNumberPass, "V23: early release skips wait; no zero digits");
        for(int side=0;side<2;side++)
            for(int zone=0;zone<32;zone++)
                check(NativeChainTimeline.Descriptor(side,zone) == (0x8000 | zone<<1 | side),
                    "V23: side and combined zone");
        ushort target = NativeChainTimeline.Descriptor(1,9);
        check(chain.MatchesRelease(target,0,3,3) && !chain.MatchesRelease(target,1,9,2) &&
              chain.MatchesRelease(target,1,9,0) && !chain.MatchesRelease(target,0,9,0) &&
              early.MatchesRelease(target,0,9,0), "V23: release lookup alternatives");

        var affect = new NativeAffectTimeline();
        affect.AdvanceNativeUpdate(0);
        check(!affect.HasFocus && affect.Counter == 8, "V24: no focus still advances");
        affect.AdvanceNativeUpdate(17);
        check(affect.HasFocus && affect.Counter == 0, "V24: new identity resets");
        int[] cosine = new int[512];
        cosine[384] = -257; // SAR 8 = -2, signed (-14)/8 = -1
        cosine[392] = 65536;
        check(affect.Intensity(cosine) == -1, "V24: signed truncation preserved");
        affect.AdvanceNativeUpdate(17);
        check(affect.Counter == 8 && affect.Intensity(cosine) == 224, "V24: same focus pulse");
        for(int i=0;i<31;i++) affect.AdvanceNativeUpdate(17);
        check(affect.Counter == 0, "V24: exact 32-update periodic clock");
        affect.AdvanceNativeUpdate(0);
        check(!affect.HasFocus && affect.Counter == 8, "V24: clear focus without reset");
        affect.AdvanceNativeUpdate(17);
        check(affect.Counter == 0, "V24: reacquire same object resets");
        affect.AdvanceNativeUpdate(17);
        affect.AdvanceNativeUpdate(18);
        check(affect.Counter == 0, "V24: new object resets even if same resource ID");
        var draw = new NativeAffectDraw(50, 50, 69);
        for (int i = 0; i < 18; i++) affect.AdvanceNativeUpdate(18);
        check(draw.Sample(affect, 1, new(369,352), cosine) is
                { Center: { X: 369, Y: 352 }, Rebuild: { IconIndex: 1 } unwrapped } &&
              unwrapped.BackgroundCopies.SequenceEqual(new NativeAffectCopy[] {
                  new(new(0,18,50,50), new(0,0))
              }), "V24: copy a vertical rectangle into a scratch sized from icon 1");
        affect.AdvanceNativeUpdate(18);
        check(draw.Sample(affect, 1, new(681,248), cosine).Rebuild is null,
            "V24: anchor and phase changes reuse the scratch for the same selector");
        affect.AdvanceNativeUpdate(18);
        check(draw.Sample(affect, 2, new(681,248), cosine).Rebuild is
                { IconIndex: 2 } wrapped &&
              wrapped.BackgroundCopies.SequenceEqual(new NativeAffectCopy[] {
                  new(new(0,0,50,1), new(0,49)),
                  new(new(0,20,50,49), new(0,0))
              }), "V24: copy the wrapped head before the tail on selector change");
        check(draw.Sample(affect, 1, new(369,352), cosine).Rebuild?.IconIndex == 1,
            "V24: one scratch is shared by successive selector draws in the same update");

        string[] names = ["loop_blue.bmp", "ico_equip.bmp", "ico_conti.bmp", "ico_field.bmp",
            "ico_kousoku.bmp", "ico_power.bmp"];
        string[] paths = names.Select(name => @"j\duel\effect\" + name).ToArray();
        check(NativeAffectDraw.Resources(string.Join("\r\n", names.Select(name => name + "\t// comment")),
            paths).SequenceEqual(paths), "V24: manifest preserves loop and icon selector order");
        foreach(int kind in new[]{1,2,4,5,6})
        {
            check(NativeAffectTimeline.IncomingLinkSelector(0,0x52,kind)==5 &&
                  NativeAffectTimeline.IncomingLinkSelector(4,0x2d9,kind)==4 &&
                  NativeAffectTimeline.IncomingLinkSelector(4,0x534,kind)==4 &&
                  NativeAffectTimeline.IncomingLinkSelector(3,123,kind)==2,
                "V24: inbound monster link selectors");
            check(NativeAffectTimeline.IncomingLinkSelector(5,0x52,kind)==(kind is 1 or 4 or 5 ? 1 : 2),
                "V24: nonmonster focus depends on link kind");
        }
        check(NativeAffectTimeline.IncomingLinkSelector(0,123,3) is null &&
            NativeAffectTimeline.IncomingLinkSelector(0,123,7) is null,
            "V24: unproven link kinds do not produce a selector");
    }
}
