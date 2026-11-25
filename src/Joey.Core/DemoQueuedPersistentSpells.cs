namespace Joey.Core;

public sealed partial class DemoDuel
{
    private const ushort InfiniteCardsId = 0x593;

    private bool TryPlanFieldActivation(CardTarget source,out CardTarget[] replaced)
    {
        replaced=[];
        if (LegacyActionsBlocked || PendingEndDiscards!=0 || source.Side!=ActivePlayer ||
            Phase!=DemoPhase.Main || source.Zone is not (DuelZone.Hand or DuelZone.FieldSpell) ||
            !TryResolveCardTarget(source,out _,out var card) || !FieldSpellRules.Supports(card.Id) ||
            source.Zone==DuelZone.FieldSpell && !FieldSpellFaceDown[source.Side]) return false;
        var targets=new List<CardTarget>();
        // Same one-active-field rule and side order as the legacy sandbox.
        // Hand activation also replaces an own Set field; opponent Set stays.
        for(int side=0;side<2;side++)
        {
            if(FieldSpells[side] is null || FieldSpellFaceDown[side] &&
                !(side==source.Side && source.Zone==DuelZone.Hand)) continue;
            if(!TryCaptureCardTarget(side,DuelZone.FieldSpell,0,out var target)) return false;
            if(target!=source) targets.Add(target);
        }
        replaced=targets.ToArray();
        return true;
    }

    public bool TryQueueFieldSpellActivation(DuelZone zone,int index,out string message,
        ActionOrigin origin=ActionOrigin.Player)
    {
        message="Choose a legal Field Spell in hand or in the own Set FIELD zone.";
        if(primitiveActionQueue is not {IsBusy:false} queue || ActionsBlocked ||
            zone is not (DuelZone.Hand or DuelZone.FieldSpell) ||
            !TryCaptureCardTarget(ActivePlayer,zone,index,out var source) ||
            !TryPlanFieldActivation(source,out _)) return false;
        queue.Enqueue(new PrepareFieldSpellActivationCommand(source.Side,source),origin);
        message="Field Spell activation admitted.";
        return true;
    }

    private bool CanActivateInfiniteCards(CardTarget source,out int slot)
    {
        slot=-1;
        if(LegacyActionsBlocked || PendingEndDiscards!=0 || source.Side!=ActivePlayer ||
            Phase!=DemoPhase.Main || source.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryResolveCardTarget(source,out int index,out var card) || card.Id!=InfiniteCardsId ||
            card.ClassCode is not (21 or 22 or 23)) return false;
        slot=source.Zone==DuelZone.Hand?FirstFreeSpellZone():index;
        return slot>=0 && (source.Zone==DuelZone.Hand || SpellFaceDown[source.Side,slot]) &&
            !equipment.Any(e=>e.Owner==source.Side&&e.SourceZone==slot);
    }

    private bool TryQueueInfiniteCards(CardTarget source,out string message,ActionOrigin origin)
    {
        message="Choose a legal Infinite Cards activation.";
        if(primitiveActionQueue is not {IsBusy:false} queue || ActionsBlocked ||
            !CanActivateInfiniteCards(source,out int slot)) return false;
        queue.Enqueue(new ActivateInfiniteCardsCommand(source.Side,source,slot),origin);
        message="Infinite Cards admitted.";
        return true;
    }

    private CommandResult ApplyInfiniteCards(ActivateInfiniteCardsCommand command)
    {
        if(!CanActivateInfiniteCards(command.Source,out int slot)||slot!=command.Slot ||
            !TryResolveCardTarget(command.Source,out int index,out var card))
            return new(CommandOutcome.Rejected,"Infinite Cards source or destination changed.");
        if(command.Source.Zone==DuelZone.Hand)
        {
            hands[command.Side].RemoveAt(index);
            Spells[command.Side,slot]=card;
        }
        SpellFaceDown[command.Side,slot]=false;
        spellSetTurn[command.Side,slot]=0;
        var result=new CommandResult(CommandOutcome.Applied);
        return command.Source.Zone==DuelZone.Hand?result with
        {Placement=new(command.Source,card.Id,index,DuelZone.SpellTrap,slot,false,false,0)}:result;
    }
}
