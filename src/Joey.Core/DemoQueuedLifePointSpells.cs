namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Opt-in queue admission for the ten existing fixed LP spells.
    /// Source identity and legality are rechecked at execution.</summary>
    public bool TryQueueLifePointSpell(DuelZone sourceZone, int index, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported LP spell in Main Phase and an idle queue.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !CanResolveQueuedLifePointSpell(source, out _)) return false;
        queue.Enqueue(new ActivateLifePointSpellCommand(ActivePlayer, source), origin);
        message = "LP spell admitted.";
        return true;
    }

    private bool CanResolveQueuedLifePointSpell(CardTarget source, out LifePointSpellChange change)
    {
        change = default;
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out int index, out var card) || card.ClassCode != 22 ||
            LifePointSpellRules.ResolveDemo(card.Id, source.Side) is not { } plan)
            return false;
        if (source.Zone == DuelZone.Hand)
        {
            if (FirstFreeSpellZone() < 0) return false;
        }
        else if (source.Zone == DuelZone.SpellTrap)
        {
            if (!SpellFaceDown[source.Side, index] ||
                equipment.Any(e => e.Owner == source.Side && e.SourceZone == index)) return false;
        }
        else return false;
        change = plan;
        return true;
    }
}
