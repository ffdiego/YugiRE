namespace Joey.Core;

public static class DirectAttackRules
{
    public const ushort AlligatorsSwordDragon = 812;

    // Resource IDs in 0056BFD0; Alligator's Sword Dragon uses 0056A810.
    public static bool AttacksThroughAnyMonster(ushort resourceId) =>
        resourceId is 386 or 396 or 397 or 421 or 487 or 634;

    public static bool AllowsAlligatorDefender(byte attribute) => attribute is 3 or 4 or 5;
}
