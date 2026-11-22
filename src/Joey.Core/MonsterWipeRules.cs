namespace Joey.Core;

/// <summary>
/// Bounded non-targeting monster destruction for the C# sandbox.
/// The native callback families P037/P039/P092 are catalogued, but effect
/// immunity, chains and original resolution timing are not yet modeled.
/// </summary>
public enum MonsterWipeScope { OpponentOnly, BothPlayers }

public sealed record MonsterWipeTarget(int Player, int Zone, Card Card, bool FaceDown);

public sealed record MonsterWipeResult(ushort EffectResourceId, int Caster,
    int DestroyedPlayer0, int DestroyedPlayer1, int DestroyedFaceDown)
{
    public int TotalDestroyed => DestroyedPlayer0 + DestroyedPlayer1;
}

public static class MonsterWipeRules
{
    public const ushort DarkHole = 335;
    public const ushort Raigeki = 336;

    public static MonsterWipeScope? Scope(ushort resourceId) => resourceId switch
    {
        DarkHole => MonsterWipeScope.BothPlayers,
        Raigeki => MonsterWipeScope.OpponentOnly,
        _ => RequiredFaceUpType(resourceId) is null ? null : MonsterWipeScope.BothPlayers
    };

    // P092 candidate filter at 0057AAA0. The sandbox supplies the DAT type
    // because no dynamic type changes are modeled here.
    private static int? RequiredFaceUpType(ushort resourceId) => resourceId switch
    {
        652 => 15,  // Warrior Elimination
        659 => 7,   // Stain Storm
        661 => 10,  // Eradicating Aerosol
        662 => 6,   // Breath of Light
        663 => 8,   // Eternal Draught
        1036 => 18, // Last Day of Witch
        1037 => 3,  // Exile of the Wicked
        _ => null
    };

    /// <summary>Snapshot actual source occurrences; no target selection is implied.</summary>
    public static IReadOnlyList<MonsterWipeTarget> Targets(
        ushort resourceId, int caster, Card?[,] monsters, bool[,] faceDown)
    {
        var scope = Scope(resourceId);
        if (scope is null) return [];
        return CollectTargets(scope.Value, caster, monsters, faceDown, RequiredFaceUpType(resourceId));
    }

    /// <summary>
    /// Mirror Force snapshot: every face-up Attack Position monster on the
    /// attacking side. Position is checked at effect resolution, not when
    /// selecting the attack target, because Mirror Force is non-targeting.
    /// </summary>
    public static IReadOnlyList<MonsterWipeTarget> AttackPositionTargets(
        int player, Card?[,] monsters, bool[,] faceDown, bool[,] defensePosition)
    {
        if (player is < 0 or > 1) return [];
        var result = new List<MonsterWipeTarget>(5);
        for (int zone = 0; zone < 5; zone++)
        {
            if (monsters[player, zone] is Card card &&
                !faceDown[player, zone] && !defensePosition[player, zone])
                result.Add(new(player, zone, card, false));
        }
        return result.AsReadOnly();
    }

    public static IReadOnlyList<MonsterWipeTarget> TargetsForScope(
        MonsterWipeScope scope, int caster, Card?[,] monsters, bool[,] faceDown) =>
        CollectTargets(scope, caster, monsters, faceDown, null);

    private static IReadOnlyList<MonsterWipeTarget> CollectTargets(
        MonsterWipeScope scope, int caster, Card?[,] monsters, bool[,] faceDown, int? requiredFaceUpType)
    {
        if (caster is < 0 or > 1) return [];
        var result = new List<MonsterWipeTarget>(10);
        for (int p = 0; p < 2; p++)
        {
            if (scope == MonsterWipeScope.OpponentOnly && p == caster)
                continue;
            for (int zone = 0; zone < 5; zone++)
            {
                if (monsters[p, zone] is not Card card) continue;
                bool hidden = faceDown[p, zone];
                if (requiredFaceUpType is int type && (hidden || card.ClassCode != type)) continue;
                result.Add(new(p, zone, card, hidden));
            }
        }
        return result.AsReadOnly();
    }
}
