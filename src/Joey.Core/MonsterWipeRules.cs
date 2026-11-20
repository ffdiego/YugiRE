namespace Joey.Core;

/// <summary>
/// Bounded non-targeting monster destruction for the C# sandbox.
/// The native callback families P037/P039 are catalogued, but effect
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
        _ => null
    };

    /// <summary>Snapshot actual source occurrences; no target selection is implied.</summary>
    public static IReadOnlyList<MonsterWipeTarget> Targets(
        ushort resourceId, int caster, Card?[,] monsters, bool[,] faceDown)
    {
        var scope = Scope(resourceId);
        if (scope is null) return [];
        return TargetsForScope(scope.Value, caster, monsters, faceDown);
    }

    public static IReadOnlyList<MonsterWipeTarget> TargetsForScope(
        MonsterWipeScope scope, int caster, Card?[,] monsters, bool[,] faceDown)
    {
        if (caster is < 0 or > 1) return [];
        var result = new List<MonsterWipeTarget>(10);
        for (int p = 0; p < 2; p++)
        {
            if (scope == MonsterWipeScope.OpponentOnly && p == caster)
                continue;
            for (int zone = 0; zone < 5; zone++)
            {
                if (monsters[p, zone] is Card card)
                    result.Add(new(p, zone, card, faceDown[p, zone]));
            }
        }
        return result.AsReadOnly();
    }
}
