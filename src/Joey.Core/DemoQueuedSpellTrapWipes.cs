namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Opt-in normal Spell activation; same queue for hand and Set.</summary>
    public bool TryQueueSpellTrapWipe(DuelZone sourceZone, int index, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported Spell/Trap wipe with tracked targets and an idle Main Phase.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || sourceZone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryCaptureCardTarget(ActivePlayer, sourceZone, index, out var source) ||
            !TryPlanSpellTrapWipe(source, out _, out _))
            return false;
        queue.Enqueue(new ActivateSpellTrapWipeCommand(source.Side, source), origin);
        message = "Spell/Trap wipe admitted.";
        return true;
    }

    private bool SpellTrapRemovalLinkMatches(int side, DuelZone zone, int slot, Card card) =>
        zone == DuelZone.FieldSpell ||
        equipment.Where(e => e.Owner == side && e.SourceZone == slot)
            .All(e => ReferenceEquals(e.Card, card));

    private bool TryPlanSpellTrapWipe(CardTarget source, out Card spell, out CardTarget[] targets)
    {
        spell = null!;
        targets = [];
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out int sourceIndex, out spell) ||
            spell.ClassCode != 22 || !SpellTrapWipeRules.Supports(spell.Id))
            return false;
        if (source.Zone == DuelZone.Hand)
        {
            if (FirstFreeSpellZone() < 0) return false;
        }
        else if (source.Zone == DuelZone.SpellTrap)
        {
            if (!SpellFaceDown[source.Side, sourceIndex] ||
                equipment.Any(e => e.Owner == source.Side && e.SourceZone == sourceIndex)) return false;
        }
        else return false;

        // Match the legacy sandbox: opposing side first, then own side
        // (Heavy Storm only); S/T slots 0..4 followed by the FIELD slot.
        var result = new List<CardTarget>();
        int[] sides = SpellTrapWipeRules.HitsOwnSide(spell.Id)
            ? [1 - source.Side, source.Side] : [1 - source.Side];
        foreach (int side in sides)
        for (int zone = 0; zone <= SpellTrapTargetRules.FieldZone; zone++)
        {
            if (source.Zone == DuelZone.SpellTrap && side == source.Side && zone == sourceIndex) continue;
            if (SpellTrapCardAt(side, zone) is not Card card) continue;
            bool field = zone == SpellTrapTargetRules.FieldZone;
            var kind = field ? DuelZone.FieldSpell : DuelZone.SpellTrap;
            int slot = field ? 0 : zone;
            if (!TryCaptureCardTarget(side, kind, slot, out var target) ||
                !SpellTrapRemovalLinkMatches(side, kind, slot, card))
                return false;
            result.Add(target);
        }
        targets = result.ToArray();
        return targets.Length > 0;
    }
}
