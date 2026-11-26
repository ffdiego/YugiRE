namespace Joey.Core;

public readonly record struct DrawSpellEffect(int Cards, int OpponentLifeGain);

/// <summary>Fixed self-draw Normal Spells with no target or selection. Amounts from the local DAT.</summary>
public static class DrawSpellRules
{
    public const ushort PotOfGreed = 1010;
    public const ushort UpstartGoblin = 1065;
    public const ushort CardDestruction = 1221;

    public static bool SupportsQueued(Card card) =>
        IsCardDestruction(card) || Effect(card) is not null;

    public static bool IsCardDestruction(Card card) => card is { ClassCode: 22, Id: CardDestruction };

    public static DrawSpellEffect? Effect(Card card) => card.ClassCode == 22 ? card.Id switch
    {
        PotOfGreed => new(2, 0),
        UpstartGoblin => new(1, 1000),
        _ => null
    } : null;
}
