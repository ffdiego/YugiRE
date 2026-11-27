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

        var number = new NativeChainTimeline(23);
        check(NativeChainDraw.Digits(number, new(369,352), 100, 22).Length == 0,
            "V23: initial frame has no digit passes");
        number.AdvanceNativeUpdate();
        check(NativeChainDraw.Digits(number, new(369,352), 100, 22).SequenceEqual(
            new NativeChainDigit[] {
                new(new(30,0,10,22), new(359,352,20,44), 0),
                new(new(20,0,10,22), new(349,352,20,44), 0)
            }), "V23: range, opening scale and right-to-left spacing");
        for (int i = 1; i < 32; i++) number.AdvanceNativeUpdate();
        check(NativeChainDraw.Sprite(new(369,352),75,42) == new CardBounds(332,315,75,42) &&
              NativeChainDraw.Sprite(new(681,248),75,42) == new CardBounds(644,211,75,42),
            "V23: whole sprite is centered above the anchor on either side");
        check(NativeChainDraw.Digits(number, new(681,248), 100, 22)[0] ==
              new NativeChainDigit(new(30,0,10,22), new(676,259,10,22), 256),
            "V23: held frame retains the full unnormalized intensity");
        number.Release();
        number.AdvanceNativeUpdate();
        check(NativeChainDraw.Digits(number, new(681,248), 100, 22)[0] ==
              new NativeChainDigit(new(30,0,10,22), new(676,259,10,23), 240),
            "V23: release grows and fades the digit");
        string[] files = ["chain_font.bmp", .. Enumerable.Range(1,25).Select(i => $"frame_?{i}.bmp")];
        string[] archive = files.Select(file => NativeDisabledChainResources.Prefix + file.Replace('?', 'e')).ToArray();
        archive[^1] = archive[15];
        files[^1] = files[15];
        var resources = NativeChainDraw.Resources(string.Join("\r\n", files), archive, 'e');
        check(resources.SequenceEqual(archive) && resources[25] == resources[15],
            "V23: manifest preserves order, localization, and repeated last frame");

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
            "V24: repeated selector reuses scratch within a Draw");
        draw.BeginDraw();
        check(draw.Sample(affect, 1, new(681,248), cosine).Rebuild is
            { IconIndex: 1 } nextDraw && nextDraw.BackgroundCopies[0].Source.Y == 19,
            "V24: 0040E288 resets selector per focused Draw");
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
