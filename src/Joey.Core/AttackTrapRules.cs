namespace Joey.Core;

/// <summary>
/// One-step declaration-of-attack response in the local C# sandbox.
/// This is an event/decision boundary, NOT a full Yugioh chain scheduler.
/// </summary>
public sealed record DemoAttackDeclaration(
    int AttackingPlayer, int AttackerZone, Card Attacker,
    int TargetZone, Card? Defender)
{
    public bool DirectAttack => TargetZone == -1;
}
public sealed record DemoAttackTrapOption(int Owner, int Zone, Card Trap);
public sealed record DemoAttackResponse(
    DemoAttackDeclaration Attack, IReadOnlyList<DemoAttackTrapOption> EligibleTraps);
public sealed record DemoAttackTrapResult(
    ushort TrapResourceId, int Responder, int AttackingPlayer,
    int DestroyedMonsters, bool AttackContinued,
    int ReflectedDamage = 0, bool BattlePhaseEnded = false);

public static class AttackTrapRules
{
    public const ushort MirrorForce = 1056;
    public const ushort MagicCylinder = 1214;
    public const ushort NegateAttack = 960;

    public static bool Supports(ushort resourceId) =>
        resourceId is MirrorForce or MagicCylinder or NegateAttack;

    /// <summary>
    /// Mirror Force, Magic Cylinder and Negate Attack respond when the other
    /// player declares an attack, before Damage Step. Their effects are
    /// deliberately distinct: board destruction, ATK reflection, or ending
    /// the Battle Phase. This does not model chain links or negation rules.
    /// </summary>
    public static bool IsEligible(Card trap, int responder, DemoAttackDeclaration attack) =>
        Supports(trap.Id) && trap.ClassCode == 21 &&
        responder is >= 0 and <= 1 && responder != attack.AttackingPlayer;
}
