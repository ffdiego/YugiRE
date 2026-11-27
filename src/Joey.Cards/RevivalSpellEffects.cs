namespace Joey.Cards;

/// <summary>Card identity and revival policy. Core owns the tracked zone movement.
/// Only the explicitly supported scope is admitted; opponent ownership and
/// prior proper Special Summon history are not inferred.</summary>
public static class RevivalSpellEffects
{
    public const ushort MonsterReborn = 1008;
    public static bool Supports(ushort id) => id == MonsterReborn;
}
