namespace Joey.Core;

public static class TargetedMonsterSpellRules
{
    public const ushort TributeToTheDoomed = 1023;
    public static bool RequiresDiscard(Card card) =>
        card.ClassCode == 22 && card.Id == TributeToTheDoomed;
}

public sealed record QueuedDiscardSpellCost(CardTarget Target, int Index, Card Card);
public sealed record QueuedTargetedMonsterSpellCandidate(CardTarget Target, int Zone, Card Card, bool FaceDown);

public sealed partial class DemoDuel
{
    private bool CanResolveDiscardMonsterSpellSource(CardTarget source)
    {
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || ActivePlayer != source.Side ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source, out int slot, out var card) ||
            !TargetedMonsterSpellRules.RequiresDiscard(card)) return false;
        if (source.Zone == DuelZone.Hand) return FirstFreeSpellZone() >= 0;
        return source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side, slot] &&
            !equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot);
    }

    private bool IsDiscardMonsterSpellTarget(CardTarget target) =>
        target.Zone == DuelZone.Monster && TryResolveCardTarget(target, out int slot, out _) &&
        TryPlanRemovalLinks(target.Side, slot, out _);

    // Cost and target lists are independent read-only views. The UI must not
    // expose a hidden opposing monster's Card identity.
    public IReadOnlyList<QueuedDiscardSpellCost> QueuedDiscardSpellCosts(CardTarget source)
    {
        if (ActionsBlocked || !CanResolveDiscardMonsterSpellSource(source)) return [];
        var result = new List<QueuedDiscardSpellCost>();
        for (int i = 0; i < hands[source.Side].Count; i++)
            if (TryCaptureCardTarget(source.Side, DuelZone.Hand, i, out var cost) && cost != source)
                result.Add(new(cost, i, hands[source.Side][i]));
        return result.AsReadOnly();
    }

    public IReadOnlyList<QueuedTargetedMonsterSpellCandidate> QueuedTargetedMonsterSpellCandidates(CardTarget source)
    {
        if (QueuedDiscardSpellCosts(source).Count == 0) return [];
        var result = new List<QueuedTargetedMonsterSpellCandidate>();
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
            if (Monsters[p,z] is Card card && TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) &&
                IsDiscardMonsterSpellTarget(target))
                result.Add(new(target,z,card,MonsterFaceDown[p,z]));
        return result.AsReadOnly();
    }

    private bool TryPlanDiscardMonsterSpell(CardTarget source, CardTarget cost, CardTarget target) =>
        CanResolveDiscardMonsterSpellSource(source) && cost != source && cost.Side == source.Side &&
        cost.Zone == DuelZone.Hand && TryResolveCardTarget(cost,out _,out _) &&
        IsDiscardMonsterSpellTarget(target);

    public bool TryQueueTargetedMonsterSpell(CardTarget source, CardTarget cost, CardTarget target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal spell, another card in your hand as cost and a tracked monster.";
        if (primitiveActionQueue is not { IsBusy:false } queue || ActionsBlocked ||
            !TryPlanDiscardMonsterSpell(source,cost,target)) return false;
        queue.Enqueue(new ActivateDiscardMonsterSpellCommand(source.Side,source,cost,target),origin);
        message = "Discard-cost monster spell admitted.";
        return true;
    }
}
