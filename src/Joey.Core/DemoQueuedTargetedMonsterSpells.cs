namespace Joey.Core;

public static class TargetedMonsterSpellRules
{
    public const ushort TributeToTheDoomed = 1023;
    public const ushort ThousandKnives = 1211;
    public const ushort Fissure = 1001;
    public static bool Supports(Card card) => card.ClassCode == 22 && card.Id is TributeToTheDoomed or ThousandKnives or Fissure;
    // DAT inventory: 34, 1210 (no pack), 2034. Alternate art is still Dark Magician.
    public static bool IsDarkMagician(Card card) => card.IsMonster && card.Id is 34 or 1210 or 2034;
    public static bool RequiresDiscard(Card card) =>
        card.ClassCode == 22 && card.Id == TributeToTheDoomed;
}

public sealed record QueuedDiscardSpellCost(CardTarget Target, int Index, Card Card);
public sealed record QueuedTargetedMonsterSpellCandidate(CardTarget Target, int Zone, Card Card, bool FaceDown);

public sealed partial class DemoDuel
{
    private bool CanResolveTargetedMonsterSpellSource(CardTarget source, out Card card)
    {
        card = null!;
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || ActivePlayer != source.Side ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source, out int slot, out card) ||
            !TargetedMonsterSpellRules.Supports(card)) return false;
        if (card.Id == TargetedMonsterSpellRules.ThousandKnives &&
            !Enumerable.Range(0,5).Any(z => Monsters[source.Side,z] is Card monster &&
                !MonsterFaceDown[source.Side,z] && TargetedMonsterSpellRules.IsDarkMagician(monster))) return false;
        if (source.Zone == DuelZone.Hand) return FirstFreeSpellZone() >= 0;
        return source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side, slot] &&
            !equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot);
    }

    private bool IsTargetedMonsterSpellTarget(CardTarget target) =>
        target.Zone == DuelZone.Monster && TryResolveCardTarget(target, out int slot, out _) &&
        TryPlanRemovalLinks(target.Side, slot, out _);

    // Compute the minimum over ALL face-up opponents, before filtering
    // tracked occurrences/links. An untracked minimum must not promote
    // a higher-ATK monster. Use the same live calculation as combat.
    private bool IsLowestFaceUpOpponent(int caster, CardTarget target)
    {
        if (target.Side != 1-caster || target.Zone != DuelZone.Monster ||
            !TryResolveCardTarget(target,out int slot,out _) || MonsterFaceDown[target.Side,slot])
            return false;
        int attack = AttackValue(target.Side,slot);
        return !Enumerable.Range(0,5).Any(z => Monsters[target.Side,z] is not null &&
            !MonsterFaceDown[target.Side,z] && AttackValue(target.Side,z) < attack);
    }

    private bool IsMonsterSpellChoice(ushort id, int caster, CardTarget target) =>
        IsTargetedMonsterSpellTarget(target) && (id switch
        {
            TargetedMonsterSpellRules.Fissure => IsLowestFaceUpOpponent(caster,target),
            TargetedMonsterSpellRules.ThousandKnives => target.Side != caster,
            TargetedMonsterSpellRules.TributeToTheDoomed => true,
            _ => false
        });

    // Cost and target lists are independent read-only views. The UI must not
    // expose a hidden opposing monster's Card identity.
    public IReadOnlyList<QueuedDiscardSpellCost> QueuedDiscardSpellCosts(CardTarget source)
    {
        if (ActionsBlocked || !CanResolveTargetedMonsterSpellSource(source,out var spell) ||
            !TargetedMonsterSpellRules.RequiresDiscard(spell)) return [];
        var result = new List<QueuedDiscardSpellCost>();
        for (int i = 0; i < hands[source.Side].Count; i++)
            if (TryCaptureCardTarget(source.Side, DuelZone.Hand, i, out var cost) && cost != source)
                result.Add(new(cost, i, hands[source.Side][i]));
        return result.AsReadOnly();
    }

    public IReadOnlyList<QueuedTargetedMonsterSpellCandidate> QueuedTargetedMonsterSpellCandidates(CardTarget source)
    {
        if (ActionsBlocked || !CanResolveTargetedMonsterSpellSource(source,out var spell) ||
            TargetedMonsterSpellRules.RequiresDiscard(spell) && QueuedDiscardSpellCosts(source).Count == 0) return [];
        var result = new List<QueuedTargetedMonsterSpellCandidate>();
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
            if (Monsters[p,z] is Card card && TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) &&
                IsMonsterSpellChoice(spell.Id,source.Side,target))
                result.Add(new(target,z,card,MonsterFaceDown[p,z]));
        return result.AsReadOnly();
    }

    private bool TryPlanTargetedMonsterSpell(CardTarget source, CardTarget? cost, CardTarget target)
    {
        if (!CanResolveTargetedMonsterSpellSource(source,out var spell) ||
            !IsMonsterSpellChoice(spell.Id,source.Side,target)) return false;
        if (spell.Id is TargetedMonsterSpellRules.ThousandKnives or TargetedMonsterSpellRules.Fissure)
            return cost is null;
        return TargetedMonsterSpellRules.RequiresDiscard(spell) &&
            cost is { } payment && payment != source && payment.Side == source.Side &&
            payment.Zone == DuelZone.Hand && TryResolveCardTarget(payment,out _,out _);
    }

    public bool TryQueueTargetedMonsterSpell(CardTarget source, CardTarget target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal no-discard monster destruction spell and eligible occurrence.";
        if (primitiveActionQueue is not { IsBusy:false } queue || ActionsBlocked ||
            !TryPlanTargetedMonsterSpell(source,null,target)) return false;
        queue.Enqueue(new ActivateConditionalMonsterSpellCommand(source.Side,source,target),origin);
        message = "Monster destruction spell admitted.";
        return true;
    }

    public bool TryQueueTargetedMonsterSpell(CardTarget source, CardTarget cost, CardTarget target,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a legal spell, another card in your hand as cost and a tracked monster.";
        if (primitiveActionQueue is not { IsBusy:false } queue || ActionsBlocked ||
            !TryPlanTargetedMonsterSpell(source,cost,target)) return false;
        queue.Enqueue(new ActivateDiscardMonsterSpellCommand(source.Side,source,cost,target),origin);
        message = "Discard-cost monster spell admitted.";
        return true;
    }
}
