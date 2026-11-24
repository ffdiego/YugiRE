namespace Joey.Core;

public static class DeckSearchSpellRules
{
    public const ushort FusionSage = 1040;
    public static CardSelectionFilter? Filter(ushort resourceId) =>
        resourceId == FusionSage ? CardSelectionFilter.Polymerization : null;
}

public sealed partial class DemoDuel
{
    public bool TryQueueDeckSearchSpell(DuelZone sourceZone, int index, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported search spell with an eligible card in your deck.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !CanResolveDeckSearchSpell(source, out _)) return false;
        queue.Enqueue(new ActivateDeckSearchSpellCommand(source.Side, source), origin);
        message = "Deck search spell admitted.";
        return true;
    }

    private bool CanResolveDeckSearchSpell(CardTarget source, out CardSelectionFilter filter)
    {
        filter = default;
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out int slot, out var card) || card.ClassCode != 22 ||
            DeckSearchSpellRules.Filter(card.Id) is not { } required) return false;
        if (source.Zone == DuelZone.Hand)
        {
            if (FirstFreeSpellZone() < 0) return false;
        }
        else if (source.Zone == DuelZone.SpellTrap)
        {
            if (!SpellFaceDown[source.Side, slot] ||
                equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot)) return false;
        }
        else return false;
        filter = required;
        return decks[source.Side].Any(c => CardSelectionRules.IsEligible(c, required));
    }
}
