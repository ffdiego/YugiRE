namespace Joey.Core;

/// <summary>
/// Playable Field Spell stat rules for the local duel sandbox.
/// Sogen's Warrior bonus is corroborated by the native 0x31 pilot. Six
/// attribute fields use documented card text and an independently observed
/// property-code mapping (equip eligibility); this is not full native parity.
/// Beast-Warrior for Sogen and other P036 spells remain unsupported.
/// </summary>
public static class FieldSpellRules
{
    public const ushort SogenId = 332;

    // From the original card-property attribute byte (Properties >> 29):
    // LIGHT=1 (Elf's Light), DARK=2 (Sword of Dark Destruction),
    // WATER=3 (Steel Shell), FIRE=4 (Burning Spear),
    // EARTH=5 (Invigoration), WIND=6 (Gust Fan).
    // IDs and effect text: P036 catalog, official descriptions for the
    // six +500 ATK/-400 DEF spells. These are resource IDs, not card indices.
    private static readonly IReadOnlyDictionary<ushort, (byte Code, string Label)> AttributeFields =
        new Dictionary<ushort, (byte, string)>
        {
            [1125] = (5, "EARTH"), // Gaia Power
            [1126] = (3, "WATER"), // Umiiruka
            [1127] = (4, "FIRE"),  // Molten Destruction
            [1128] = (6, "WIND"),  // Rising Air Current
            [1129] = (1, "LIGHT"), // Luminous Spark
            [1130] = (2, "DARK")   // Mystic Plasma Zone
        };

    public static bool Supports(ushort resourceId) =>
        resourceId == SogenId || AttributeFields.ContainsKey(resourceId);

    /// <summary>
    /// Sum a resolved field modifier for an actual face-up monster. The
    /// caller must not apply this to face-down monsters or spells/traps.
    /// An absent attributeCode cannot trigger an attribute-field bonus.
    /// </summary>
    public static NativeAiAttributeDelta Bonus(ushort resourceId, int monsterClassCode,
                                                byte attributeCode = 0)
    {
        if (monsterClassCode is < 1 or > 20) return default;
        if (resourceId == SogenId)
            return monsterClassCode == 15 ? new(200, 200) : default;
        return AttributeFields.TryGetValue(resourceId, out var rule) &&
               attributeCode == rule.Code ? new(500, -400) : default;
    }

    public static string EffectSummary(ushort resourceId)
    {
        if (resourceId == SogenId)
            return "Warriors gain +200 ATK and DEF (Beast-Warrior pending).";
        return AttributeFields.TryGetValue(resourceId, out var rule)
            ? $"{rule.Label} monsters gain 500 ATK and lose 400 DEF."
            : "This Field Spell is not yet implemented.";
    }
}
