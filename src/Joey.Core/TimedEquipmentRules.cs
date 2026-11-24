namespace Joey.Core;

/// <summary>Static arithmetic only, from 0056DD30. No claim about the
/// phase, owner, initial value or frequency of native counter updates.</summary>
public static class TimedEquipmentRules
{
    public const ushort GermInfection = 1046, StimPack = 1060;

    public static int DecodeCounter(ushort occurrenceFlags) => (occurrenceFlags & 0x3c) >> 2;

    public static NativeAiResult<NativeAiAttributeDelta> Contribution(
        ushort resourceId, byte dynamicMonsterType, int counter)
    {
        if (counter is < 0 or > 15)
            return NativeAiResult<NativeAiAttributeDelta>.Unsupported("Counter outside observed four-bit field");
        return resourceId switch
        {
            StimPack => NativeAiResult<NativeAiAttributeDelta>.Supported(new(700 - 200 * counter, 0)),
            GermInfection => NativeAiResult<NativeAiAttributeDelta>.Supported(
                new(dynamicMonsterType == 7 ? 0 : -300 * counter, 0)),
            _ => NativeAiResult<NativeAiAttributeDelta>.Unsupported("Unmapped timed equipment")
        };
    }

    // Native 0040B270 saturates this field at 15 on its add path.
    // This helper does NOT prove that either P025 card uses that producer.
    public static int SaturatingAdd(int counter, int amount)
    {
        if (counter is < 0 or > 15 || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(counter));
        return (int)Math.Min(15L, (long)counter + amount);
    }
}
