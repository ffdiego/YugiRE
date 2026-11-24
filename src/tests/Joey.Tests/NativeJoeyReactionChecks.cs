using Joey.LookAndFeel;

internal static class NativeJoeyReactionChecks
{
    public static void Run(Action<bool,string> check)
    {
        void Sample(int lp,int delta,int index) =>
            check(NativeJoeyReaction.Damage(true,lp,delta)==
                new NativeJoeyDamageReaction(index,0x10000045u+(uint)index),
                $"V31: damage LP={lp} delta={delta} index={index}");
        Sample(8000,-1,0); Sample(8000,-1000,0); Sample(8000,-1001,1);
        Sample(8000,-1500,1); Sample(8000,-1501,2); Sample(8000,-2001,3);
        Sample(8000,-7999,3); Sample(8000,-8000,5);
        Sample(4001,-1000,0); Sample(4000,-1000,3);
        Sample(4000,-1001,2); Sample(4000,-2000,2); Sample(4000,-2001,4);
        Sample(1000,-1000,5); Sample(1000,int.MinValue,5);
        check(NativeJoeyReaction.Damage(false,8000,-1000) is null &&
              NativeJoeyReaction.Damage(true,8000,1000) is null &&
              NativeJoeyReaction.Damage(true,8000,0) is null &&
              NativeJoeyReaction.Damage(true,0,-1) is null,
            "V31: side, healing, zero delta and zero prior LP gates");
        check(NativeJoeyReaction.Kind(3,0)==NativeJoeyReactionKind.None &&
              NativeJoeyReaction.Kind(0,0x10000036)==NativeJoeyReactionKind.SoundOnly &&
              NativeJoeyReaction.Kind(3,0x10000036)==NativeJoeyReactionKind.BustUp,
            "V31: distinguish no request, voice only and bust-up");
        check(NativeJoeyReaction.DurationParameter(-1)==32 &&
              NativeJoeyReaction.DurationParameter(0)==32 &&
              NativeJoeyReaction.DurationParameter(32)==32 &&
              NativeJoeyReaction.DurationParameter(33)==1 &&
              NativeJoeyReaction.DurationParameter(65)==1 &&
              NativeJoeyReaction.DurationParameter(66)==2 &&
              NativeJoeyReaction.DurationParameter(3300)==100,
            "V31: audio-derived duration parameter and fallback");
        check(NativeJoeyReaction.SoundDescriptorIndex(0x10000045,70)==0x45 &&
              NativeJoeyReaction.SoundDescriptorIndex(0x10000045,69) is null &&
              NativeJoeyReaction.SoundDescriptorIndex(0x20000045,70) is null &&
              NativeJoeyReaction.SoundDescriptorIndex(0x10001045,70)==0x45,
            "V18: family-one sound ID uses low 12 bits, not archive index");
    }
}
