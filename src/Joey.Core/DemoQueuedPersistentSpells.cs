namespace Joey.Core;

public sealed partial class DemoDuel
{
    private const ushort InfiniteCardsId = 0x593;
    private const ushort InsectBarrierId = 1352;
    private const int InsectType = 10;

    private static bool SupportsQueuedContinuousSpell(Card card) =>
        card is { Id: InfiniteCardsId, ClassCode: 21 or 22 or 23 } || IsInsectBarrier(card);

    private static bool IsInsectBarrier(Card? card) => card is { Id: InsectBarrierId, ClassCode: 22 };

    private bool HasFaceUpInsectBarrier(int side) =>
        Enumerable.Range(0, 5).Any(slot => !SpellFaceDown[side, slot] && IsInsectBarrier(Spells[side, slot]));

    private bool AttackRestrictedByInsectBarrier(int side, Card attacker) =>
        attacker.ClassCode == InsectType && HasFaceUpInsectBarrier(1 - side);

    internal bool ShouldPlayInsectBarrier(int side, Card card) =>
        IsInsectBarrier(card) && !HasFaceUpInsectBarrier(side) &&
        Enumerable.Range(0, 5).Any(slot => !MonsterFaceDown[1 - side, slot] &&
            Monsters[1 - side, slot] is { ClassCode: InsectType });

    // Shared commit primitives; only admission and scheduling differ.
    private void CommitFieldActivation(int side, int? handIndex)
    {
        if (handIndex is int index)
        {
            FieldSpells[side] = hands[side][index];
            hands[side].RemoveAt(index);
        }
        FieldSpellFaceDown[side] = false;
    }

    private void CommitContinuousSpellActivation(int side, int slot, int? handIndex)
    {
        if (handIndex is int index)
        {
            Spells[side, slot] = hands[side][index];
            hands[side].RemoveAt(index);
        }
        SpellFaceDown[side, slot] = false;
    }

    private void CommitSpellTrapSet(int side, int handIndex, DuelZone destination, int slot)
    {
        Card card = hands[side][handIndex];
        if (destination == DuelZone.FieldSpell)
        {
            FieldSpells[side] = card;
            FieldSpellFaceDown[side] = true;
        }
        else
        {
            Spells[side, slot] = card;
            SpellFaceDown[side, slot] = true;
            spellSetTurn[side, slot] = Turn;
        }
        hands[side].RemoveAt(handIndex);
    }

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

    private bool CanActivateContinuousSpell(CardTarget source,out int slot)
    {
        slot=-1;
        if(LegacyActionsBlocked || PendingEndDiscards!=0 || source.Side!=ActivePlayer ||
            Phase!=DemoPhase.Main || source.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryResolveCardTarget(source,out int index,out var card) || !SupportsQueuedContinuousSpell(card)) return false;
        slot=source.Zone==DuelZone.Hand?FirstFreeSpellZone():index;
        // An out parameter cannot be captured by a lambda (CS1628).
        int destination = slot;
        return destination>=0 && (source.Zone==DuelZone.Hand || SpellFaceDown[source.Side,destination]) &&
            !equipment.Any(e=>e.Owner==source.Side&&e.SourceZone==destination);
    }

    private bool TryQueueContinuousSpell(CardTarget source,out string message,ActionOrigin origin)
    {
        message="Choose a legal Continuous Spell activation.";
        if(primitiveActionQueue is not {IsBusy:false} queue || ActionsBlocked ||
            !CanActivateContinuousSpell(source,out int slot)) return false;
        queue.Enqueue(new ActivateContinuousSpellCommand(source.Side,source,slot),origin);
        message="Continuous Spell activation admitted.";
        return true;
    }

    private CommandResult ApplyContinuousSpell(ActivateContinuousSpellCommand command)
    {
        if(!CanActivateContinuousSpell(command.Source,out int slot)||slot!=command.Slot ||
            !TryResolveCardTarget(command.Source,out int index,out var card))
            return new(CommandOutcome.Rejected,"Continuous Spell source or destination changed.");
        CommitContinuousSpellActivation(command.Side, slot,
            command.Source.Zone == DuelZone.Hand ? index : null);
        spellSetTurn[command.Side,slot]=0;
        var result=new CommandResult(CommandOutcome.Applied);
        return command.Source.Zone==DuelZone.Hand?result with
        {Placement=new(command.Source,card.Id,index,DuelZone.SpellTrap,slot,false,false,0)}:result;
    }
}
