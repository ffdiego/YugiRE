namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Legal admission for the queue-backed sandbox, from the hand or a Set
    // Equip Spell. Do not call the legacy Equip() while this queue is active.
    public bool TryQueueEquipment(DuelZone sourceZone, int index, int targetPlayer, int targetZone,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Equipment needs an idle queue, a legal source and a compatible tracked target.";
        int side = ActivePlayer;
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || Phase != DemoPhase.Main ||
            sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(side, sourceZone, index, out var source) ||
            !TryCaptureCardTarget(targetPlayer, DuelZone.Monster, targetZone, out var target))
            return false;
        int slot = sourceZone == DuelZone.Hand ? FirstFreeSpellZone() : index;
        if (!CanResolveEquipment(source, target, slot, out _, out _, out _, out _)) return false;
        queue.Enqueue(new EquipCommand(side, source, target, slot), origin);
        message = "Equipment admitted.";
        return true;
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
        if (command.Source.Zone == DuelZone.Hand)
        {
            hands[owner].RemoveAt(index);
            Spells[owner, slot] = card;
        }
        SpellFaceDown[owner, slot] = false;
        equipment.Add(new(card, owner, slot, command.Target.Side, targetIndex, monster));
        return new(CommandOutcome.Applied);
    }
}
