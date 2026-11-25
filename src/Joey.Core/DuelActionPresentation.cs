namespace Joey.Core;

public enum ActionPresentationKind { Activate, Summon, Set, Flip, DirectAttack, TargetedAttack }

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
            case DeclareDirectAttackCommand attack:
                if (!TryResolveCardTarget(attack.Attacker, out int attackerSlot, out var attacker))
                    return null;
                return new(ActionPresentationKind.DirectAttack, attacker, attack.Attacker,
                    attackerSlot, DuelZone.Monster, -1);
            case DeclareTargetedAttackCommand attack:
                if (!TryResolveCardTarget(attack.Attacker, out int sourceSlot, out var source) ||
                    !TryResolveCardTarget(attack.Defender, out int defenderSlot, out _))
                    return null;
                return new(ActionPresentationKind.TargetedAttack, source, attack.Attacker,
                    sourceSlot, DuelZone.Monster, defenderSlot);
            case ActivateFieldSpellCommand field:
                target = field.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.FieldSpell; slot = 0; break;
            case ActivateInfiniteCardsCommand infinite:
                target = infinite.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = infinite.Slot; break;
            case ActivateDrawSpellCommand draw:
                target = draw.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateLifePointSpellCommand lp:
                target = lp.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateMonsterWipeCommand wipe:
                target = wipe.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateSpellTrapWipeCommand spellWipe:
                target = spellWipe.Source; kind = ActionPresentationKind.Activate;
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
