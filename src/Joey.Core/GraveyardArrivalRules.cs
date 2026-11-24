namespace Joey.Core;

// Semantic cause, not an opcode. The action executor must retain it until
// the actual movement completes. A card effect is not the same as its cost.
public enum GraveyardArrivalCause
{
    DiscardByEffect, SendFromHandByEffect, DiscardAsCost, HandLimit,
    BattleDestruction, EffectDestruction, Tribute, FusionMaterial, Mill, Other
}

public enum GraveyardArrivalEffectKind { Draw, Damage, ReturnToDeckTop, ReviewDirectHandSend }

public readonly record struct GraveyardArrivalIntent(
    ushort ResourceId, GraveyardArrivalEffectKind Kind, int Recipient, int Amount)
{
    public bool IsResolvable => Kind != GraveyardArrivalEffectKind.ReviewDirectHandSend;
}

// Pure domain classification. No zone movement, RNG, or scheduler lives here.
// An adapter must capture the physical occurrence before emitting a followup.
public static class GraveyardArrivalRules
{
    public const ushort ElectricSnake = 462;
    public const ushort Minar = 533;
    public const ushort CockroachKnight = 478;
    public const ushort SwordOfDeepSeated = 1042;

    // sourceHandSide is null when movement did not originate in a hand.
    // effectController is the controller of the responsible card effect,
    // not the active player or the producer of the command.
    public static GraveyardArrivalIntent? AfterMovement(
        ushort resourceId, int owner, int? sourceHandSide, int? effectController,
        GraveyardArrivalCause cause, bool arrivedInOwnerGraveyard, bool gameOver)
    {
        if (owner is not (0 or 1) ||
            (sourceHandSide.HasValue && sourceHandSide.Value is not (0 or 1)) ||
            (effectController.HasValue && effectController.Value is not (0 or 1)))
            throw new ArgumentOutOfRangeException(nameof(owner), "Invalid side.");
        if (!arrivedInOwnerGraveyard || gameOver) return null;

        if (resourceId is CockroachKnight or SwordOfDeepSeated)
            return new(resourceId, GraveyardArrivalEffectKind.ReturnToDeckTop, owner, 1);

        if (resourceId is not (ElectricSnake or Minar) ||
            sourceHandSide != owner || effectController != 1 - owner)
            return null;

        // Historical printings use broader wording than later errata.
        // Joey's exact flag semantics are not established for direct sends.
        // This result is a diagnostic only, never an executable effect.
        if (cause == GraveyardArrivalCause.SendFromHandByEffect)
            return new(resourceId, GraveyardArrivalEffectKind.ReviewDirectHandSend, owner, 0);

        if (cause != GraveyardArrivalCause.DiscardByEffect) return null;
        return resourceId == ElectricSnake
            ? new(resourceId, GraveyardArrivalEffectKind.Draw, owner, 2)
            : new(resourceId, GraveyardArrivalEffectKind.Damage, 1 - owner, 1000);
    }
}
