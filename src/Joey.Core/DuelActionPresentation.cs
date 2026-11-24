namespace Joey.Core;

public enum ActionPresentationKind { Activate, Summon, Set, Flip }

/// <summary>Read-only projection of the command's source BEFORE mutation.
/// Contains domain data, not graphics coordinates or duration.</summary>
public sealed record DuelActionPresentation(ActionPresentationKind Kind, Card Card, CardTarget Source,
    int SourceIndex, DuelZone Destination, int DestinationSlot);

public sealed partial class DemoDuel
{
    public DuelActionPresentation? DescribeActionPresentation(DuelCommand command)
    {
        CardTarget target;
        ActionPresentationKind kind;
        DuelZone destination;
        int slot;
        switch (command)
        {
            case ActivateDrawSpellCommand draw:
                target = draw.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateLifePointSpellCommand lp:
                target = lp.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateRaigekiCommand activation:
                target = activation.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case SummonCommand summon:
                target = summon.Source; kind = ActionPresentationKind.Summon;
                destination = DuelZone.Monster; slot = summon.Slot; break;
            case SetCommand set:
                target = set.Source; kind = ActionPresentationKind.Set;
                destination = set.Destination; slot = set.Slot; break;
            case FlipCommand flip:
                target = flip.Target; kind = ActionPresentationKind.Flip;
                destination = DuelZone.Monster; slot = -1; break;
            default: return null;
        }
        if (!TryResolveCardTarget(target, out int index, out var card)) return null;
        if (kind == ActionPresentationKind.Flip) slot = index;
        if (kind == ActionPresentationKind.Activate && target.Zone == DuelZone.SpellTrap) slot = index;
        return slot < 0 ? null : new(kind, card, target, index, destination, slot);
    }
}
