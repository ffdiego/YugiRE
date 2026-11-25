namespace Joey.Core;

public sealed partial class DemoDuel
{
    public const ushort FinalDestiny = 1067;
    private const int FinalDestinyCost = 5;

    private bool IsFinalDestinySource(CardTarget source)
    {
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || ActivePlayer != source.Side ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source,out int slot,out var spell) ||
            spell.ClassCode != 22 || spell.Id != FinalDestiny) return false;
        return source.Zone == DuelZone.Hand ? FirstFreeSpellZone() >= 0 :
            source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side,slot] &&
            !equipment.Any(e => e.Owner == source.Side && e.SourceZone == slot);
    }

    public IReadOnlyList<QueuedDiscardSpellCost> QueuedFinalDestinyCosts(CardTarget source)
    {
        if (ActionsBlocked || !IsFinalDestinySource(source) ||
            !TryPlanFinalDestinyField(source,out _,out _,out _)) return [];
        var choices = new List<QueuedDiscardSpellCost>();
        for (int i=0;i<hands[source.Side].Count;i++)
            if (TryCaptureCardTarget(source.Side,DuelZone.Hand,i,out var target) && target != source)
                choices.Add(new(target,i,hands[source.Side][i]));
        return choices.Count >= FinalDestinyCost ? choices.AsReadOnly() : [];
    }

    private bool CanPayFinalDestiny(CardTarget source, IReadOnlyList<CardTarget> remaining) =>
        IsFinalDestinySource(source) && remaining.Distinct().Count() == remaining.Count &&
        remaining.All(t => t != source && t.Side == source.Side && t.Zone == DuelZone.Hand &&
            TryResolveCardTarget(t,out _,out _));

    private bool TryPlanFinalDestinyField(CardTarget source, out DuelCommand[] commands,
        out CardTarget[] targets, out Dictionary<CardTarget,DuelCommand[]> plans)
    {
        commands = []; targets = []; plans = new();
        var moves = new List<DuelCommand>();
        var seen = new HashSet<CardTarget>();
        // Same monster order as the existing monster wipe, with immediate
        // linked consequences. Remaining S/T and FIELD follow afterward.
        for(int p=0;p<2;p++)
        for(int z=0;z<5;z++)
        {
            if (Monsters[p,z] is null) continue;
            if (!TryCaptureCardTarget(p,DuelZone.Monster,z,out var target) ||
                !TryPlanRemovalLinks(p,z,out var links)) return false;
            seen.Add(target); plans.Add(target,links);
            moves.Add(new DestroyCommand(p,target));
            foreach(var link in links)
            {
                if (link is DestroyCommand destroy && !seen.Add(destroy.Target)) return false;
                moves.Add(link);
            }
        }
        foreach(int p in new[]{1-source.Side,source.Side})
        for(int z=0;z<=SpellTrapTargetRules.FieldZone;z++)
        {
            if (SpellTrapCardAt(p,z) is not Card card) continue;
            var zone = z == SpellTrapTargetRules.FieldZone ? DuelZone.FieldSpell : DuelZone.SpellTrap;
            if (!TryCaptureCardTarget(p,zone,zone == DuelZone.FieldSpell?0:z,out var target) ||
                !SpellTrapRemovalLinkMatches(p,zone,z,card)) return false;
            if (target == source || !seen.Add(target)) continue;
            moves.Add(new DestroyCommand(p,target));
        }
        commands=moves.ToArray(); targets=seen.ToArray();
        return true;
    }

    public bool TryQueueFinalDestiny(CardTarget source, IReadOnlyList<CardTarget> costs,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose Final Destiny and exactly five other tracked cards from your hand.";
        if (primitiveActionQueue is not { IsBusy:false } queue || ActionsBlocked ||
            costs is null || costs.Count != FinalDestinyCost ||
            !CanPayFinalDestiny(source,costs) ||
            !TryPlanFinalDestinyField(source,out _,out var targets,out _) || targets.Length==0)
            return false;
        // Immutable fixed-size command; never retain caller-owned list.
        queue.Enqueue(new ActivateFinalDestinyCommand(source.Side,source,
            costs[0],costs[1],costs[2],costs[3],costs[4]),origin);
        message = "Final Destiny admitted.";
        return true;
    }
}
