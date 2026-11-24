namespace Joey.Core;

// Replacement of the working base attack and additive bonuses are distinct
// stages. Do not change the catalogue Card or store a modified ATK on it.
public readonly record struct DynamicEquipmentContribution(int? BaseAttack, NativeAiAttributeDelta Delta);

public static class DynamicEquipmentRules
{
    public const ushort Megamorph = 656, UnitedWeStand = 1449, MagePower = 1450;
    public static bool Supports(ushort id) => id is Megamorph or UnitedWeStand or MagePower;

    // Counters supplied by the state adapter. The equipment controller, not
    // the monster controller, determines the counts and relative LP.
    public static DynamicEquipmentContribution Evaluate(ushort id, int originalAttack,
        int ownerLife, int opponentLife, int faceUpMonsters, int spellTrapsIncludingField)
    {
        if (originalAttack < 0 || ownerLife < 0 || opponentLife < 0 ||
            faceUpMonsters is < 0 or > 5 || spellTrapsIncludingField is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(faceUpMonsters), "Invalid resolved equipment context.");
        return id switch
        {
            Megamorph => new(ownerLife < opponentLife ? checked(originalAttack * 2) :
                ownerLife > opponentLife ? NativeAiAttributes.HalfNative(originalAttack) : (int?)null, default),
            UnitedWeStand => new(null, new(faceUpMonsters * 800, faceUpMonsters * 800)),
            MagePower => new(null, new(spellTrapsIncludingField * 500, spellTrapsIncludingField * 500)),
            _ => throw new ArgumentException("Unsupported dynamic equipment.", nameof(id))
        };
    }
}
