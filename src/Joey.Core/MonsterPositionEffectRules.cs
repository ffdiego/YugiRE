namespace Joey.Core;

/// <summary>
/// Manual position transition. Preserves previous face/defense flags so rules
/// do not confuse Flip Summon, battle revelation and ordinary repositioning.
/// </summary>
public sealed record DemoMonsterPositionChange(
    int Player, int Zone, Card Card,
    bool WasFaceDown, bool WasInDefense, bool IsFaceDown, bool IsInDefense)
{
    public bool FlipSummoned => WasFaceDown && !IsFaceDown;
    public bool FaceUpAttackToDefense => !WasFaceDown && !WasInDefense &&
        !IsFaceDown && IsInDefense;
    public bool FaceUpDefenseToAttack => !WasFaceDown && WasInDefense &&
        !IsFaceDown && !IsInDefense;
}

public enum MonsterPositionTriggeredEffect { None, DestroyOpponentMonster }

/// <summary>
/// Native 00575210 produces position-triggered requests for several monsters.
/// This first sandbox implementation covers Dream Clown (P071) only.
/// </summary>
public static class MonsterPositionEffectRules
{
    public const ushort DreamClown = 119;

    public static bool TriggersOnAttackToDefense(ushort resourceId) =>
        resourceId == DreamClown;

    public static MonsterPositionTriggeredEffect Trigger(DemoMonsterPositionChange change) =>
        change.FaceUpAttackToDefense && TriggersOnAttackToDefense(change.Card.Id)
            ? MonsterPositionTriggeredEffect.DestroyOpponentMonster
            : MonsterPositionTriggeredEffect.None;
}
