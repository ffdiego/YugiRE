namespace Joey.Core;

/// <summary>Resolved target inputs, not a native snapshot or an activation request.
/// Identity, coordinates and dynamic properties must be supplied independently.
/// GenericGuardsSatisfied is the caller's result for the unmodeled generic filter.
/// Null never means empty, face-down, type zero, or a satisfied guard.</summary>
public sealed record EquipmentTargetContext(
    int? Player = null, int? Zone = null, int? InternalIndex = null,
    ushort? ResourceId = null, bool? FaceUp = null,
    byte? DynamicType = null, byte? DynamicAttribute = null,
    bool? GenericGuardsSatisfied = null);

/// <summary>Three audited predicates of 00579CE0 only. Does not discover dynamic
/// properties, implement 0056C510, or decide full native activation legality.</summary>
public static class EquipmentRules
{
    public static bool Supports(ushort resourceId) => resourceId is 300 or 301 or 307;

    public static NativeAiResult<bool> Eligibility(ushort resourceId, EquipmentTargetContext? target)
    {
        if (!Supports(resourceId))
            return NativeAiResult<bool>.Unsupported("Equipment outside the three audited IDs");
        if (target is null || target.Player is null || target.Zone is null ||
            target.InternalIndex is null || target.ResourceId is null || target.FaceUp is null ||
            target.DynamicType is null || target.DynamicAttribute is null || target.GenericGuardsSatisfied is null)
            return NativeAiResult<bool>.Unsupported("Equipment target context is incomplete");
        bool common = target.Player is >= 0 and <= 1 && target.Zone is >= 0 and < 5 &&
            target.InternalIndex is > 0 and <= 0xfff && target.ResourceId != ushort.MaxValue &&
            target.FaceUp.Value && target.GenericGuardsSatisfied.Value;
        bool specific = resourceId switch
        {
            300 => target.DynamicType == 15,
            307 => target.DynamicType == 11,
            301 => target.DynamicAttribute == 2,
            _ => false
        };
        return NativeAiResult<bool>.Supported(common && specific);
    }
}
