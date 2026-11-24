namespace Joey.Core;

public enum DirectDamageStatus { Unsupported, SkippedByRequestFlag, Command }

// A plan for the LP command only, not native queue/presentation or activation legality.
public sealed record DirectDamageCommand(int TargetPlayer, int Amount)
{
    public ushort CommandWord => (ushort)(0x43 | (TargetPlayer << 15));
    public int ThirdField => 0;
    public int FourthField => 1;
}

public sealed record DirectDamageResult(DirectDamageStatus Status, DirectDamageCommand? Command, string? Limitation);

/// <summary>Five individually checked ID branches of 00584E60. See docs/cards/p045-direct-damage.md.</summary>
public static class DirectDamageRules
{
    // Resource IDs, never internal indices. No alias/+2000 normalization.
    public static int? Amount(ushort resourceId) => resourceId switch
    {
        342 => 200, 343 => 500, 344 => 600, 345 => 800, 1007 => 300,
        _ => null
    };

    // 00593CC0 scalar value appended by 00592A50; does not model buffer counters.
    public static int? PrepareTarget(byte? sourceByte) => sourceByte.HasValue ? 1 - (sourceByte.Value & 1) : null;

    public static DirectDamageResult Resolve(ushort resourceId, byte? requestFlags, byte? preparedTarget)
    {
        int? amount = Amount(resourceId);
        if (!amount.HasValue)
            return new(DirectDamageStatus.Unsupported, null, "Resource ID outside the five audited P045 branches");
        if (!requestFlags.HasValue)
            return new(DirectDamageStatus.Unsupported, null, "Request +4 flags are unknown");
        // 00584E76..00584E7B skips the helper before reading the target.
        if ((requestFlags.Value & 4) != 0)
            return new(DirectDamageStatus.SkippedByRequestFlag, null, "Meaning of request flag mask 4 remains unknown");
        if (!preparedTarget.HasValue)
            return new(DirectDamageStatus.Unsupported, null, "Prepared request +6 target is unknown");
        return new(DirectDamageStatus.Command, new(preparedTarget.Value & 1, amount.Value), null);
    }
}
