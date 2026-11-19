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
    int DestroyedMonsters, bool AttackContinued);

public static class AttackTrapRules
{
    public const ushort MirrorForce = 1056;
    public static bool Supports(ushort resourceId) => resourceId == MirrorForce;

    /// <summary>
    /// Mirror Force activates only when the *other* player declares an attack.
    /// Never during damage calculation or after the attack's FLIP trigger.
    /// The result destroys opposing Attack Position monsters, not targets.
    /// </summary>
    public static bool IsEligible(Card trap, int responder, DemoAttackDeclaration attack) =>
        trap.Id == MirrorForce && trap.ClassCode == 21 &&
        responder is >= 0 and <= 1 && responder != attack.AttackingPlayer;
}
