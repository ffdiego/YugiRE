namespace Joey.Cards;

/// <summary>Card-specific turn-phase rules by DAT resource ID.
/// Core owns occurrence eligibility, traversal order, command admission and execution.
/// The ordered field rules preserve the existing sandbox emission order.
/// </summary>
public static class TurnPhaseEffects
{
    public readonly record struct StandbyFieldLifeRule(ushort ResourceId, int Amount, bool? Defense);

    public const ushort MarieTheFallenOne = 1446;
    public const ushort DarkZebra = 1113;
    public const ushort WickedWormBeast = 22;
    public const ushort GoblinAttackForce = 1344;
    public const int GoblinPositionLockTurnOffset = 2;

    public static IReadOnlyList<StandbyFieldLifeRule> StandbyFieldLifeRules { get; } =
        Array.AsReadOnly<StandbyFieldLifeRule>(
        [
            new(1437, 1000, Defense: false), // Spirit of the Breeze
            new(1438, 1000, Defense: true),  // Dancing Fairy
            new(1441, 800, Defense: null),   // Cure Mermaid
        ]);

    public static int? StandbyGraveyardHeal(ushort id) =>
        id == MarieTheFallenOne ? 200 : null;

    public static bool IsStandbyLifeMonster(ushort id) =>
        StandbyGraveyardHeal(id) is not null ||
        StandbyFieldLifeRules.Any(rule => rule.ResourceId == id);
}
