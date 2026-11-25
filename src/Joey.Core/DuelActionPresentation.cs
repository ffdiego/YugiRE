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
            case DeclareDirectAttackCommand attack when HasQueuedDirectResponse(attack.Side):
                return null;
            case ResolveDirectAttackResponseCommand { Source: null } response:
                if (queuedDirectResponse is not { } pending || !UsesQueuedAttackResponse(response.Request) ||
                    !TryResolveCardTarget(pending.Attacker, out int directSlot, out var directAttacker)) return null;
                return new(ActionPresentationKind.DirectAttack, directAttacker, pending.Attacker,
                    directSlot, DuelZone.Monster, -1);
            case ResolveDirectAttackResponseCommand { Source: { } attackTrap }:
                target = attackTrap; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = -1; break;
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
            case PrepareFusionSummonCommand fusion:
                target = fusion.Source; kind = ActionPresentationKind.Activate;
                destination = target.Zone == DuelZone.FieldSpell ? DuelZone.FieldSpell : DuelZone.SpellTrap;
                slot = target.Zone == DuelZone.FieldSpell ? 0 : FirstFreeSpellZone(); break;
            case EquipCommand equip:
                target = equip.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = equip.SpellSlot; break;
            case ActivateFieldSpellCommand field:
                target = field.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.FieldSpell; slot = 0; break;
            case ActivateInfiniteCardsCommand infinite:
                target = infinite.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = infinite.Slot; break;
            case ActivateTargetedSpellTrapCommand targeted:
                target = targeted.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateDeckSearchSpellCommand search:
                target = search.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
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
            case ActivateConditionalMonsterSpellCommand conditional:
                target = conditional.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ActivateRaigekiCommand activation:
                target = activation.Source; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = FirstFreeSpellZone(); break;
            case ResolveSummonResponseCommand { Source: { } trap }:
                target = trap; kind = ActionPresentationKind.Activate;
                destination = DuelZone.SpellTrap; slot = -1; break;
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
