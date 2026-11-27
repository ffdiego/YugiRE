namespace Joey.Cards;

/// <summary>Card-specific additive modifiers; state and link lifecycle belong to Core.
/// Null required type permits any face-up monster. A partial entry deliberately
/// models the additive portion only; the unimplemented rider is in the manifest.</summary>
public readonly record struct EquipmentEffect(int Attack, int Defense,
    byte? RequiredType = null, bool HasPendingRider = false);

public static class EquipmentEffects
{
    public const ushort LightningBlade = 1422;

    public static EquipmentEffect? For(ushort id) => id switch
    {
        303 => new(1000, 0, HasPendingRider: true), // Axe of Despair
        310 => new(500, 0, HasPendingRider: true), // Black Pendant
        312 => new(0, 800, HasPendingRider: true), // Horn of Light
        313 => new(700, 700, HasPendingRider: true), // Horn of the Unicorn
        320 => new(700, 0, HasPendingRider: true), // Malevolent Nuzzler
        1042 => new(500, 500, HasPendingRider: true), // Sword of Deep-Seated
        1058 => new(-500, -500, HasPendingRider: true), // Ring of Magnetism
        1182 => new(700, 0, RequiredType: 15, HasPendingRider: true), // Sword of Dragon's Soul
        1420 => new(1000, -1000, HasPendingRider: true), // Mask of Brutality
        LightningBlade => new(800, 0, RequiredType: 15),
        _ => null
    };

    // One live Lightning Blade affects every face-up WATER monster.
    public static int GlobalAttackDelta(ushort sourceId, byte targetAttribute) =>
        sourceId == LightningBlade && targetAttribute == 3 ? -500 : 0;
}
