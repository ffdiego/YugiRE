namespace Joey.Core;

/// <summary>Read-only candidate projection. The occurrence, not the display
/// coordinate or catalogue ID, is the value submitted to the queue.</summary>
public sealed record QueuedSpellTrapCandidate(CardTarget Target, int Zone, Card Card, bool FaceDown);

public sealed partial class DemoDuel
{
    public IReadOnlyList<QueuedSpellTrapCandidate> QueuedSpellTrapTargetCandidates(CardTarget source)
    {
        if (ActionsBlocked || PendingEndDiscards != 0 ||
            !TryResolveTargetedSpellTrapSource(source, out var spell, out int sourceIndex))
            return [];
        var candidates = new List<QueuedSpellTrapCandidate>();
        foreach (var item in SpellTrapTargets(spell.Id,
            source.Zone == DuelZone.SpellTrap ? sourceIndex : -1))
        {
            bool field = item.IsFieldZone;
            var kind = field ? DuelZone.FieldSpell : DuelZone.SpellTrap;
            int slot = field ? 0 : item.Zone;
            if (!TryCaptureCardTarget(item.Player, kind, slot, out var target) ||
                !TryPlanTargetedSpellTrap(source, target, out _, out _))
                continue;
            candidates.Add(new(target, item.Zone, item.Card, IsSpellTrapFaceDown(item.Player, item.Zone)));
        }
        return candidates.AsReadOnly();
    }

    /// <summary>Admit an already selected target; cancelling the read-only
    /// query requires no action. A06 owns the blocking interactive prompt.</summary>
    public bool TryQueueTargetedSpellTrap(CardTarget source, CardTarget target, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal Spell/Trap source, target occurrence and an idle Main Phase.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !TryPlanTargetedSpellTrap(source, target, out _, out _))
            return false;
        queue.Enqueue(new ActivateTargetedSpellTrapCommand(source.Side, source, target), origin);
        message = "Targeted Spell/Trap admitted.";
        return true;
    }

    private bool TryResolveTargetedSpellTrapSource(CardTarget source, out Card spell, out int index)
    {
        spell = null!;
        index = -1;
        if (ActivePlayer != source.Side || Phase != DemoPhase.Main ||
            !TryResolveCardTarget(source, out index, out spell) || spell.ClassCode != 22 ||
            !SpellTrapTargetRules.Supports(spell.Id))
            return false;
        if (source.Zone == DuelZone.Hand) return FirstFreeSpellZone() >= 0;
        int slot = index;
        return source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side, slot] &&
            !equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot) &&
            (spell.Id != SpellTrapTargetRules.MysticalSpaceTyphoon || spellSetTurn[source.Side, slot] < Turn);
    }

    private bool TryPlanTargetedSpellTrap(CardTarget source, CardTarget target,
        out Card spell, out CardTarget[] targets)
    {
        spell = null!;
        targets = [];
        if (source == target || target.Zone is not (DuelZone.SpellTrap or DuelZone.FieldSpell) ||
            !TryResolveTargetedSpellTrapSource(source, out spell, out _) ||
            !TryResolveCardTarget(target, out int slot, out var card) ||
            !SpellTrapRemovalLinkMatches(target.Side, target.Zone, slot, card))
            return false;
        int zone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
        bool hidden = IsSpellTrapFaceDown(target.Side, zone);
        if (!SpellTrapTargetRules.Accepts(spell.Id, card, hidden)) return false;

        var result = new List<CardTarget> { target };
        // Preserve the existing selective-destruction cleanup: removing any
        // face-up piece while a board is active also removes the other face-up
        // pieces on the same side, after the primary target.
        if (!hidden && IsDestinyBoardPiece(card) &&
            DestinyBoardRules.HasActiveBoard(target.Side, Spells, SpellFaceDown))
        {
            for (int z = 0; z < 5; z++)
            {
                if (z == zone || Spells[target.Side,z] is not Card piece ||
                    SpellFaceDown[target.Side,z] || !IsDestinyBoardPiece(piece)) continue;
                if (!TryCaptureCardTarget(target.Side, DuelZone.SpellTrap,z,out var linked) ||
                    !SpellTrapRemovalLinkMatches(target.Side, DuelZone.SpellTrap,z,piece))
                    return false;
                result.Add(linked);
            }
        }
        targets = result.ToArray();
        return true;
    }
}
