namespace Joey.Core;

public sealed record QueuedEquipmentCandidate(CardTarget Target, int Slot, Card Card);

public sealed partial class DemoDuel
{
    // A read-only projection for A06/UI/CPU. Hold the occurrence token,
    // not a slot or a catalogue ID, from selection through admission.
    public IReadOnlyList<QueuedEquipmentCandidate> QueuedEquipmentCandidates(CardTarget source)
    {
        if (ActionsBlocked || PendingEndDiscards != 0 ||
            !TryResolveEquipmentSourceSlot(source,out int slot))
            return [];
        var result = new List<QueuedEquipmentCandidate>();
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
            if (TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) &&
                CanResolveEquipment(source,target,slot,out _,out _,out _,out var monster))
                result.Add(new(target,z,monster));
        return result.AsReadOnly();
    }

    // Compatibility wrapper for callers choosing current coordinates.
    public bool TryQueueEquipment(DuelZone sourceZone, int index, int targetPlayer, int targetZone,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Equipment needs a tracked source and target.";
        if (sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer,sourceZone,index,out var source) ||
            !TryCaptureCardTarget(targetPlayer,DuelZone.Monster,targetZone,out var target))
            return false;
        return TryQueueEquipment(source,target,out message,origin);
    }

    // Use the exact source and target chosen by the operator. A cancelled
    // candidate query has no effect, and a replaced occurrence cannot equip.
    public bool TryQueueEquipment(CardTarget source, CardTarget target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Equipment needs an idle queue, a legal source and a compatible tracked target.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !TryResolveEquipmentSourceSlot(source,out int slot) ||
            !CanResolveEquipment(source,target,slot,out _,out _,out _,out _))
            return false;
        queue.Enqueue(new EquipCommand(source.Side,source,target,slot),origin);
        message = "Equipment admitted.";
        return true;
    }

    private bool TryResolveEquipmentSourceSlot(CardTarget source,out int slot)
    {
        slot = -1;
        if (source.Side != ActivePlayer || Phase != DemoPhase.Main ||
            source.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryResolveCardTarget(source,out int index,out var card) ||
            card.ClassCode != 22 || !EquipmentRules.SupportsQueued(card.Id)) return false;
        slot = source.Zone == DuelZone.Hand ? FirstFreeSpellZone() : index;
        return slot >= 0;
    }

    private bool CanResolveEquipment(CardTarget source, CardTarget target, int slot,
        out int sourceIndex, out Card card, out int targetIndex, out Card monster)
    {
        sourceIndex = targetIndex = -1;
        card = monster = null!;
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main || slot is < 0 or >= 5 ||
            source.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) || target.Zone != DuelZone.Monster ||
            !TryResolveCardTarget(source, out sourceIndex, out card) ||
            !TryResolveCardTarget(target, out targetIndex, out monster) ||
            card.ClassCode != 22 || !monster.IsMonster ||
            EquipmentRules.QueuedEligibility(card.Id, DemoEquipmentTarget(target.Side, targetIndex))
                is not { IsSupported: true, Value: true }) return false;

        // No duplicate source link, no overwriting unrelated cards.
        if (equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot)) return false;
        if (source.Zone == DuelZone.Hand) return Spells[source.Side, slot] is null;
        return sourceIndex == slot && SpellFaceDown[source.Side, slot];
    }

    private void CommitEquipmentActivation(int owner, int slot, int? handIndex,
        Card card, int targetSide, int targetIndex, Card monster)
    {
        if (handIndex is int index)
        {
            hands[owner].RemoveAt(index);
            Spells[owner, slot] = card;
        }
        SpellFaceDown[owner, slot] = false;
        equipment.Add(new(card, owner, slot, targetSide, targetIndex, monster));
    }

    private CommandResult ApplyQueuedEquipment(EquipCommand command)
    {
        if (!TryResolveCardTarget(command.Source, out _, out _) ||
            !TryResolveCardTarget(command.Target, out _, out _))
            return new(CommandOutcome.Skipped, "Equipment or monster occurrence no longer present.");
        if (!CanResolveEquipment(command.Source, command.Target, command.SpellSlot,
            out int index, out var card, out int targetIndex, out var monster))
            return new(CommandOutcome.Rejected, "Equipment no longer legal.");
        int owner = command.Side, slot = command.SpellSlot;
        // Placement/flip and its link constitute one logical equipment
        // activation, not separate externally visible half-equipped states.
        CommitEquipmentActivation(owner, slot,
            command.Source.Zone == DuelZone.Hand ? index : null,
            card, command.Target.Side, targetIndex, monster);
        spellSetTurn[owner, slot] = 0;
        return new(CommandOutcome.Applied);
    }
}
