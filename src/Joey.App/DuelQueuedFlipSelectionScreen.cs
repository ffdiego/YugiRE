using Joey.Core;

static partial class Game
{
    // Local pre-admission picks. The rule engine is unchanged until the
    // complete response command executes; never stage a legacy selection.
    private static object? queuedFlipPickRequest;
    private static readonly List<(int Side, int Zone, CardTarget Target)> queuedFlipPicks = [];

    static void SyncQueuedFlipPicks(object request)
    {
        if (ReferenceEquals(queuedFlipPickRequest, request)) return;
        queuedFlipPickRequest = request;
        queuedFlipPicks.Clear();
    }

    static bool QueuedFlipPicked(object? request, int side, int zone) =>
        request is not null && ReferenceEquals(queuedFlipPickRequest, request) &&
        queuedFlipPicks.Any(p => p.Side == side && p.Zone == zone);

    static int QueuedFlipPickCount(object request) =>
        ReferenceEquals(queuedFlipPickRequest, request) ? queuedFlipPicks.Count : 0;

    static bool SubmitQueuedMonsterFlipPicks(DemoFlipTarget request)
    {
        SyncQueuedFlipPicks(request);
        if (!duel.TrySubmitQueuedMonsterFlipChoice(request, queuedFlipPicks.Select(p => p.Target).ToArray(),
                out message)) return false;
        queuedFlipPicks.Clear();
        return true;
    }

    static bool PickQueuedMonsterFlip(DemoFlipTarget request, int side, int zone)
    {
        SyncQueuedFlipPicks(request);
        if (duel.FlipEffectsNegated) return SubmitQueuedMonsterFlipPicks(request);
        var option = duel.QueuedMonsterFlipCandidates().FirstOrDefault(c => c.Target.Side == side && c.Zone == zone);
        if (option is null || queuedFlipPicks.Any(p => p.Target == option.Target))
        {
            message = "Choose a distinct eligible monster occurrence.";
            return false;
        }
        int max = request.Effect == DemoFlipEffect.ReturnUpToTwo ? 2 : 1;
        queuedFlipPicks.Add((side, zone, option.Target));
        if (queuedFlipPicks.Count < max)
        {
            message = $"Selected {queuedFlipPicks.Count}/{max} target(s). Choose another or Done.";
            return false;
        }
        if (SubmitQueuedMonsterFlipPicks(request)) return true;
        queuedFlipPicks.RemoveAt(queuedFlipPicks.Count - 1);
        return false;
    }

    static bool PickQueuedSpellTrapFlip(PendingFlipSpellTrapTargets request, int side, int zone)
    {
        SyncQueuedFlipPicks(request);
        if (!duel.FlipEffectsNegated)
        {
            var option = duel.QueuedFlipSpellTrapCandidates(request)
                .FirstOrDefault(c => c.Target.Side == side && c.Zone == zone);
            if (option is null || queuedFlipPicks.Any(p => p.Target == option.Target))
            {
                message = "Choose a distinct eligible Spell/Trap occurrence.";
                return false;
            }
            queuedFlipPicks.Add((side, zone, option.Target));
            if (queuedFlipPicks.Count < 2)
            {
                message = "Selected 1/2 targets.";
                return false;
            }
        }
        if (duel.TrySubmitQueuedFlipSpellTrapChoice(request, queuedFlipPicks.Select(p => p.Target).ToArray(),
                out message))
        {
            queuedFlipPicks.Clear();
            return true;
        }
        if (!duel.FlipEffectsNegated && queuedFlipPicks.Count != 0)
            queuedFlipPicks.RemoveAt(queuedFlipPicks.Count - 1);
        return false;
    }
}
