using Joey.LookAndFeel;

internal static class NativeAlternativeActivationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var effect = new NativeAlternativeActivationTimeline();
        check(effect.Counter == -128 && effect.Active &&
              NativeAlternativeActivationTimeline.Center == new CardPoint(525,300),
              "V25: separate constructor counter and center");
        for (int i = 1; i <= NativeAlternativeActivationTimeline.Updates; i++)
        {
            var sound = effect.AdvanceNativeUpdate();
            check(effect.Counter == -128 + 16*i &&
                  effect.Active == (i < 33) &&
                  sound == (i == 9 ? (uint?)0x1000000D : null),
                  "V25: exact update and one sound request at input zero");
        }
        check(effect.AdvanceNativeUpdate() is null && effect.Counter == 400,
              "V25: inactive instance does not advance");
        check(NativeAlternativeActivationTimeline.UsesAlternative(-1,100,100) &&
              NativeAlternativeActivationTimeline.UsesAlternative(3,100,null) &&
              NativeAlternativeActivationTimeline.UsesAlternative(3,100,101) &&
              !NativeAlternativeActivationTimeline.UsesAlternative(3,100,100),
              "V25: native route based on resolved visual occurrence");
        check(NativeAlternativeActivationTimeline.AlwaysAlternative(0x49) &&
              !NativeAlternativeActivationTimeline.AlwaysAlternative(0x48) &&
              !NativeAlternativeActivationTimeline.AlwaysAlternative(0x18) &&
              NativeAlternativeActivationTimeline.UsesAlternativeForGroup(0) &&
              !NativeAlternativeActivationTimeline.UsesAlternativeForGroup(2),
              "V25: opcode routes and group fallback");
        foreach (ushort descriptor in new ushort[]{0x0080,0x4081,0x8080,0xc081})
        {
            var sample = NativeExclusionStart.FromConstructor(descriptor,123);
            bool visible = (descriptor & 0x8000) != 0;
            check(sample.Descriptor == descriptor && sample.FaceUp == visible &&
                  sample.VisibleCardArgument == (visible ? 123u : 0u),
                "V26: preserve descriptor and mask hidden constructor argument");
        }
        check(NativeExclusionStart.InitialCounter == 64 && NativeExclusionStart.Layer == 0x120,
              "V26: constructor only; no duration inferred");
    }
}
