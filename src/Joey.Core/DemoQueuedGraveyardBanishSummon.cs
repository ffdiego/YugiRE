namespace Joey.Core;

public sealed partial class DemoDuel
{
    public IReadOnlyList<QueuedGraveyardCandidate> QueuedGraveyardBanishSummonCandidates(CardTarget source)
    {
        if (ActionsBlocked || Phase != DemoPhase.Main || source.Side != ActivePlayer ||
            source.Zone != DuelZone.Hand || !TryResolveCardTarget(source,out _,out var card) ||
            GraveyardBanishSummonRules.QueuedRequiredCount(card) == 0 || FirstFreeMonsterZone() < 0)
            return [];
        var options = new List<QueuedGraveyardCandidate>();
        for (int i = 0; i < graves[source.Side].Count; i++)
        {
            Card candidate = graves[source.Side][i];
            if (GraveyardBanishSummonRules.QueuedIsEligible(card,candidate) &&
                TryCaptureCardTarget(source.Side,DuelZone.Graveyard,i,out var target))
                options.Add(new(target,i,candidate));
        }
        return options.Count >= GraveyardBanishSummonRules.QueuedRequiredCount(card)
            ? options.AsReadOnly() : [];
    }

    public bool TryQueueGraveyardBanishSummon(CardTarget source, int slot, IReadOnlyList<CardTarget> cost,
        bool defense, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported inherent summon, exact eligible cost and free monster slot.";
        if (primitiveActionQueue is not { IsBusy:false } queue || ActionsBlocked || cost is null ||
            !TryPlanGraveyardBanishSummon(source,slot,cost)) return false;
        queue.Enqueue(new PrepareGraveyardBanishSummonCommand(source.Side,source,slot,defense,
            cost[0],cost.Count>1?cost[1]:null,cost.Count>2?cost[2]:null),origin);
        message = "Inherent Special Summon admitted.";
        return true;
    }

    private bool TryPlanGraveyardBanishSummon(CardTarget source,int slot,IReadOnlyList<CardTarget> cost)
    {
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || Phase != DemoPhase.Main ||
            source.Side != ActivePlayer || source.Zone != DuelZone.Hand ||
            slot is < 0 or >= 5 || Monsters[source.Side,slot] is not null ||
            !TryResolveCardTarget(source,out _,out var card)) return false;
        int required = GraveyardBanishSummonRules.QueuedRequiredCount(card);
        return required > 0 && cost.Count == required && cost.Distinct().Count() == required &&
            cost.All(t => t.Side == source.Side && t.Zone == DuelZone.Graveyard &&
                TryResolveCardTarget(t,out _,out var candidate) &&
                GraveyardBanishSummonRules.QueuedIsEligible(card,candidate));
    }
}
