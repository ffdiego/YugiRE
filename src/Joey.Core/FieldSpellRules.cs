namespace Joey.Core;

/// <summary>
/// Field spell effects supported by the local playable sandbox.
/// A native 0x31 pilot observed Sogen (+200/+200) for class 15 (Warrior).
/// Beast-Warrior eligibility is not identified by a reliable native class code,
/// and remains explicitly unsupported in this first slice.
/// </summary>
public static class FieldSpellRules
{
    public const ushort SogenId = 332;

    public static bool Supports(ushort resourceId) => resourceId == SogenId;

    public static NativeAiAttributeDelta Bonus(ushort resourceId, int monsterClassCode) =>
        resourceId == SogenId && monsterClassCode == 15
            ? new NativeAiAttributeDelta(200, 200)
            : default;
}
