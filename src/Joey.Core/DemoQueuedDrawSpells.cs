namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Opt-in legal admission; no draw or movement until Tick.</summary>
    public bool TryQueueDrawSpell(DuelZone sourceZone, int index, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported draw spell, sufficient deck cards and an idle Main Phase.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !CanResolveQueuedDrawSpell(source, out _)) return false;
        queue.Enqueue(new ActivateDrawSpellCommand(ActivePlayer, source), origin);
        message = "Draw spell admitted.";
        return true;
    }

    private bool CanResolveQueuedDrawSpell(CardTarget source, out Card card)
    {
        card = null!;
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out int index, out card) ||
            !DrawSpellRules.SupportsQueued(card) ||
            card.ClassCode == 21 && !CanActivateOwnMainTrap(source, index)) return false;
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
        if (DrawSpellRules.IsCardDestruction(card))
        {
            int ownCount = hands[source.Side].Count - (source.Zone == DuelZone.Hand ? 1 : 0);
            return (ownCount > 0 || hands[1 - source.Side].Count > 0) && DeckCount(source.Side) >= ownCount;
        }
        return DrawSpellRules.QueuedEffect(card) is { } plan && DeckCount(source.Side) >= plan.Cards;
    }
}
