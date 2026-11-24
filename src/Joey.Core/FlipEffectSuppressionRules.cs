namespace Joey.Core;

/// <summary>
/// Bounded continuous-effect evaluation for Royal Command (1530).
/// The native game distinguishes visible presence from effective presence:
/// 00598D70/00598C40 propagate suppression, while 0057EAC0 and
/// 005BB720 gate FLIP activation/resolution. Not a general chain engine.
/// </summary>
public static class FlipEffectSuppressionRules
{
    public const ushort RoyalCommand = 1530; // 0x5FA, Trap
    public const ushort RoyalDecree = 1033;  // 0x409, Trap
    public const ushort Jinzo = 751;         // 0x2EF, Monster

    public static bool JinzoFaceUp(Card?[,] monsters, bool[,] monsterFaceDown) =>
        Enumerable.Range(0, 2).Any(p => Enumerable.Range(0, 5).Any(z =>
            monsters[p, z] is Card monster && monster.Id == Jinzo &&
            monster.IsMonster && !monsterFaceDown[p, z]));

    public static bool RoyalDecreeFaceUp(Card?[,] spells, bool[,] spellFaceDown) =>
        FaceUpTrapPresent(RoyalDecree, spells, spellFaceDown);

    public static bool RoyalCommandFaceUp(Card?[,] spells, bool[,] spellFaceDown) =>
        FaceUpTrapPresent(RoyalCommand, spells, spellFaceDown);

    private static bool FaceUpTrapPresent(ushort id, Card?[,] spells, bool[,] spellFaceDown) =>
        Enumerable.Range(0, 2).Any(p => Enumerable.Range(0, 5).Any(z =>
            spells[p, z] is Card trap && trap.Id == id && trap.ClassCode == 21 &&
            !spellFaceDown[p, z]));

    /// <summary>
    /// Native 00598D70 suppresses other face-up Traps when an effective Jinzo
    /// or face-up Royal Decree is detected, so Royal Command cannot contribute
    /// its global FLIP mask. This covers those three cards only.
    /// </summary>
    public static bool RoyalCommandEffective(
        Card?[,] monsters, bool[,] monsterFaceDown,
        Card?[,] spells, bool[,] spellFaceDown) =>
        RoyalCommandFaceUp(spells, spellFaceDown) &&
        !JinzoFaceUp(monsters, monsterFaceDown) &&
        !RoyalDecreeFaceUp(spells, spellFaceDown);
}
