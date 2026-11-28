namespace Joey.Cards;

public static class DiceSpellEffects
{
    public const ushort GracefulDice = 1203;

    public static int GracefulDiceBonus(int face) => 100 * face;
}
