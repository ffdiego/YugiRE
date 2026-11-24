namespace Joey.Core;

/// <summary>
/// Text-backed playable Field Spells for the C# sandbox, not full native parity.
/// Sogen's +200/+200 for Warrior (type 15) was also observed in native 0x31.
/// Beast-Warrior (type 12) and the remaining P036 rules follow card text.
/// </summary>
public static class FieldSpellRules
{
    public const ushort SogenId = 332;

    // Native class codes grounded by examples in card-callback-catalog.json:
    // Dragon=1 (Red-Eyes), Zombie=2 (Red-Moon Baby), Fiend=3 (Sangan),
    // Pyro=4 (Spirit of Flames), Rock=6 (Morphing Jar),
    // Machine=7 (Jinzo), Fish=8 (Cure Mermaid), Dinosaur=9 (Gilasaurus),
    // Insect=10 (Man-Eater Bug), Beast=11 (Giant Rat),
    // Beast-Warrior=12 (Mother Grizzly), Plant=13 (Mystic Tomato),
    // Aqua=14 (Penguin Soldier), Warrior=15 (The Legendary Fisherman),
    // Winged Beast=16 (Sonic Bird), Fairy=17 (Shining Angel),
    // Spellcaster=18 (Magician of Faith), Thunder=19 (Thunder Dragon).
    // Sea Serpent=5 is confirmed by resources 352 and 447 in the pinned DAT.
    private static readonly IReadOnlyDictionary<ushort, (int[] Boost, int[] Weaken)> TypeFields =
        new Dictionary<ushort, (int[], int[])>
        {
            [329] = (new[] { 10, 11, 12, 13 }, Array.Empty<int>()), // Forest
            [330] = (new[] { 2, 6, 9 }, Array.Empty<int>()),       // Wasteland
            [331] = (new[] { 1, 16, 19 }, Array.Empty<int>()),     // Mountain
            [332] = (new[] { 12, 15 }, Array.Empty<int>()),        // Sogen
            [333] = (new[] { 5, 8, 14, 19 }, new[] { 4, 7 }),     // Umi
            [334] = (new[] { 3, 18 }, new[] { 17 })                // Yami
        };

    // From Properties >> 29: LIGHT=1, DARK=2, WATER=3,
    // FIRE=4, EARTH=5, WIND=6 (see equipment attribute predicates).
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
        TypeFields.ContainsKey(resourceId) ||
        AttributeFields.ContainsKey(resourceId) ||
        resourceId is 1069 or FusionRules.FusionGate;

    /// <summary>
    /// Evaluate a face-up monster's field contribution from live type,
    /// attribute, and battle position. The caller handles face-down suppression.
    /// No class/attribute-changing effects or chains exist in this sandbox.
    /// </summary>
    public static NativeAiAttributeDelta Bonus(ushort resourceId,
        int monsterClassCode, byte attributeCode = 0, bool defensePosition = false)
    {
        if (monsterClassCode is < 1 or > 20) return default;
        if (resourceId == 1069)
            return defensePosition ? new(0, 500) : default;

        if (TypeFields.TryGetValue(resourceId, out var type))
        {
            if (type.Boost.Contains(monsterClassCode)) return new(200, 200);
            if (type.Weaken.Contains(monsterClassCode)) return new(-200, -200);
            return default;
        }

        return AttributeFields.TryGetValue(resourceId, out var attribute) &&
               attributeCode == attribute.Code ? new(500, -400) : default;
    }

    public static string EffectSummary(ushort resourceId) => resourceId switch
    {
        329 => "Forest boosts Insect, Beast, Beast-Warrior and Plant by 200 ATK/DEF.",
        330 => "Wasteland boosts Dinosaur, Zombie and Rock by 200 ATK/DEF.",
        331 => "Mountain boosts Dragon, Winged Beast and Thunder by 200 ATK/DEF.",
        332 => "Sogen boosts Warrior and Beast-Warrior by 200 ATK/DEF.",
        333 => "Umi boosts Fish, Sea Serpent, Thunder and Aqua; weakens Machine and Pyro.",
        334 => "Yami boosts Fiend and Spellcaster; weakens Fairy.",
        1069 => "Chorus of Sanctuary gives Defense Position monsters 500 DEF.",
        FusionRules.FusionGate => "Either player can use Fusion Gate during their Main Phase.",
        _ => AttributeFields.TryGetValue(resourceId, out var rule)
            ? $"{rule.Label} monsters gain 500 ATK and lose 400 DEF."
            : "This Field Spell is not yet implemented."
    };
}
