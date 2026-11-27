namespace Joey.Core;

public readonly record struct DrawSpellEffect(int Cards, int OpponentLifeGain);

public static class DrawSpellRules
{
    public const ushort PotOfGreed = 1010;
    public const ushort UpstartGoblin = 1065;
    public const ushort CardDestruction = 1221;

    public static bool SupportsQueued(Card card) =>
        IsCardDestruction(card) || Effect(card) is not null;

    public static bool IsCardDestruction(Card card) => card is { ClassCode: 22, Id: CardDestruction };

    public static DrawSpellEffect? Effect(Card card) =>
        card.ClassCode == 22 && Joey.Cards.FixedSpellEffects.Draw(card.Id) is { } effect
            ? new DrawSpellEffect(effect.Cards, effect.OpponentLifeGain)
            : null;
}
