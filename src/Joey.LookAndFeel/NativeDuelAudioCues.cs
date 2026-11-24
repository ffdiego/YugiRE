namespace Joey.LookAndFeel;

/// <summary>Native audio requests at already identified presentation boundaries.
/// No descriptor filename assumptions, player, clock, or separate action queue.
/// Caller supplies the original Update counter (before increment), not a frame index.</summary>
public static class NativeDuelAudioCues
{
    public const uint SpecialSummonConstruct = 0x1000000e; // 0044FCC0
    public const uint BreakConstruct = 0x10000008; // 00450C30
    public const uint ExplosionConstruct = 0x10000007; // 004511D0

    public static uint? ActivationUpdate(int previousCounter) =>
        previousCounter == 0 ? 0x1000000du : null; // 00450590 / 00450A90

    public static uint? DuelStartUpdate(int previousCounter) =>
        previousCounter == 0 ? 0x10000004u : null; // 00451950

    public static uint VijaConstruct(bool nonzeroMode) =>
        0x10000022u + (nonzeroMode ? 1u : 0u); // 004580F0

    // 00481230 delegates to 00480B40 only if side is unchanged.
    // Phase code 3 is the raw native code, not a C# enum value.
    public static uint? TurnNotice(int previousSide,int side,int nativePhase) =>
        previousSide != side ? 0x10000005u :
        nativePhase == 3 ? 0x10000011u : null;

    public static uint? ChangedDisplayValue(int previous,int next) =>
        previous != next ? 0x10000012u : null; // 00449740

    // 0044EF40 dispatches raw presentation codes from multiple call sites.
    public static uint? RawEvent(int code) => code switch
    {
        6 => 0x10000010u,
        7 => 0x1000000fu,
        9 => 0x10000009u,
        0x19 => 0x1000000au,
        _ => null
    };

    // 0044C320: LP difference has precedence over global state.
    // Inputs are relative to (~00A54E5C)&1 and 00A54E5C&1 respectively.
    // The native global state is not the phase enum.
    public static NativeAudioRequest DuelMusic(ushort selectedSideLife,
        ushort otherSideLife,int nativeState)
    {
        int delta=selectedSideLife-otherSideLife;
        int index=delta>=3000?8:delta<=-3000?7:
            nativeState==3?6:nativeState==1?5:4;
        return new(NativeAudioBank.Stream,index,15);
    }
}
