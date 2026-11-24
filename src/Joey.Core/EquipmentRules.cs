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

/// <summary>Audited fixed-bonus predicates of 00579CE0 only. Does not discover dynamic
/// properties, implement 0056C510, or decide full native activation legality.</summary>
public static class EquipmentRules
{
    // Restrict activation to the reviewed fixed-bonus spells. The shared
    // callback profile alone is not sufficient (e.g. Metalmorph, Megamorph).
    private static readonly Dictionary<ushort, byte> types = new()
    {
        [300] = 15, [307] = 11,
        [302] = 3,
        [304] = 10,
        [309] = 13,
        [311] = 17,
        [314] = 1,
        [315] = 19,
        [318] = 12,
        [321] = 2,
        [322] = 18,
        [324] = 7,
        [325] = 9,
        [326] = 16,
        [327] = 14,
    };
    private static readonly Dictionary<ushort, byte> attributes = new()
    {
        [301] = 2,
        [306] = 1,
        [308] = 3,
        [653] = 4,
        [1012] = 4,
        [1013] = 6,
    };

    public static bool Supports(ushort resourceId) =>
        types.ContainsKey(resourceId) || attributes.ContainsKey(resourceId);

    public static NativeAiResult<bool> Eligibility(ushort resourceId, EquipmentTargetContext? target)
    {
        if (!Supports(resourceId))
            return NativeAiResult<bool>.Unsupported("Equipment outside the audited fixed-bonus IDs");
        if (target is null || target.Player is null || target.Zone is null ||
            target.InternalIndex is null || target.ResourceId is null || target.FaceUp is null ||
            target.DynamicType is null || target.DynamicAttribute is null || target.GenericGuardsSatisfied is null)
            return NativeAiResult<bool>.Unsupported("Equipment target context is incomplete");
        bool common = target.Player is >= 0 and <= 1 && target.Zone is >= 0 and < 5 &&
            target.InternalIndex is > 0 and <= 0xfff && target.ResourceId != ushort.MaxValue &&
            target.FaceUp.Value && target.GenericGuardsSatisfied.Value;
        bool specific = types.TryGetValue(resourceId, out byte type)
            ? target.DynamicType == type
            : attributes.TryGetValue(resourceId, out byte attribute) && target.DynamicAttribute == attribute;
        return NativeAiResult<bool>.Supported(common && specific);
    }
}
