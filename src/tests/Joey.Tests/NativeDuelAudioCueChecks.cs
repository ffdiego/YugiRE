using Joey.LookAndFeel;

internal static class NativeDuelAudioCueChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(NativeDuelAudioCues.ActivationUpdate(0)==0x1000000d &&
            NativeDuelAudioCues.ActivationUpdate(16) is null &&
            NativeDuelAudioCues.ActivationUpdate(256) is null,
            "V18: activation sound on first Update only");
        check(NativeDuelAudioCues.DuelStartUpdate(0)==0x10000004 &&
            NativeDuelAudioCues.DuelStartUpdate(8) is null,
            "V18: duel start sound on first Update only");
        check(NativeDuelAudioCues.SpecialSummonConstruct==0x1000000e &&
            NativeDuelAudioCues.BreakConstruct==0x10000008 &&
            NativeDuelAudioCues.ExplosionConstruct==0x10000007,
            "V18: distinct constructor sound requests");
        check(NativeDuelAudioCues.VijaConstruct(false)==0x10000022 &&
            NativeDuelAudioCues.VijaConstruct(true)==0x10000023,
            "V18: Vija mode selects sound");
        check(NativeDuelAudioCues.TurnNotice(0,1,3)==0x10000005 &&
            NativeDuelAudioCues.TurnNotice(1,1,3)==0x10000011 &&
            NativeDuelAudioCues.TurnNotice(1,1,2) is null,
            "V18: side-change has precedence over phase-specific notice");
        check(NativeDuelAudioCues.ChangedDisplayValue(1,1) is null &&
            NativeDuelAudioCues.ChangedDisplayValue(1,2)==0x10000012,
            "V18: unchanged display value does not request sound");
        check(NativeDuelAudioCues.RawEvent(6)==0x10000010 &&
            NativeDuelAudioCues.RawEvent(7)==0x1000000f &&
            NativeDuelAudioCues.RawEvent(9)==0x10000009 &&
            NativeDuelAudioCues.RawEvent(25)==0x1000000a &&
            NativeDuelAudioCues.RawEvent(8) is null,
            "V18: raw event dispatch");
        void Music(ushort a,ushort b,int state,int index) =>
            check(NativeDuelAudioCues.DuelMusic(a,b,state)==
                new NativeAudioRequest(NativeAudioBank.Stream,index,15),
                $"V18: music {a}/{b} state={state} -> {index}");
        Music(7000,4000,3,8); Music(6999,4000,3,6);
        Music(4000,7000,1,7); Music(4000,6999,1,5);
        Music(8000,8000,0,4); Music(8000,8000,3,6);
        Music(8000,8000,1,5); Music(0,65535,3,7);
    }
}
