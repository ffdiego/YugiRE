namespace Joey.Core;

/// <summary>Read-only FLIP target view. Opponent hidden identity is for
/// authorized simulation; the presentation must not expose it.</summary>
public sealed record QueuedMonsterFlipCandidate(CardTarget Target, int Zone, Card Card, bool FaceDown);

public sealed partial class DemoDuel
{
    // A narrowly scoped admission exception to the existing pending-choice
    // gate. Only this method can set it, only while enqueueing the answer.
    private bool admittingQueuedMonsterFlipAnswer;
    private (DemoFlipTarget Request, CardTarget Source)? resumedSummonMonsterFlip;

    private static bool SupportsQueuedMonsterSelection(DemoFlipTarget pending) => pending.Trigger switch
    {
        DemoMonsterEffectTrigger.Flip => pending.Effect is
            DemoFlipEffect.DestroyMonster or DemoFlipEffect.ReturnOne or DemoFlipEffect.ReturnUpToTwo or
            DemoFlipEffect.ChangeFaceUpPosition or DemoFlipEffect.PreventMonsterAttack,
        DemoMonsterEffectTrigger.PositionChange => pending.Effect == DemoFlipEffect.DestroyOpponentMonster &&
            MonsterPositionEffectRules.TriggersOnAttackToDefense(pending.SourceCard.Id),
        DemoMonsterEffectTrigger.NormalOrFlipSummon => pending.Effect == DemoFlipEffect.PreventMonsterAttack &&
            pending.SourceCard.Id == InvitationToADarkSleep,
        _ => false
    };

    private bool SuppressesQueuedMonsterSelection(DemoFlipTarget pending) =>
        pending.Trigger == DemoMonsterEffectTrigger.Flip && FlipEffectsNegated;

    private static bool IsBounce(DemoFlipTarget pending) =>
        pending.Effect is DemoFlipEffect.ReturnOne or DemoFlipEffect.ReturnUpToTwo;

    private static bool IsEligibleTargetSide(DemoFlipTarget pending, int side) =>
        (pending.Effect != DemoFlipEffect.DestroyOpponentMonster &&
            pending.Trigger != DemoMonsterEffectTrigger.NormalOrFlipSummon) || side != pending.Owner;

    private static bool MovesMonsterFlipTarget(DemoFlipTarget pending) =>
        pending.Effect is not (DemoFlipEffect.ChangeFaceUpPosition or DemoFlipEffect.PreventMonsterAttack);

    // UI/CPU pre-admission selection is not a duel command. Retain the
    // exact occurrence for the first optional pick, not a mutable slot.
    private (DemoFlipTarget Request, CardTarget Source, CardTarget Target)? queuedFlipFirstPick;

    public bool UsesQueuedMonsterFlipSelection(DemoFlipTarget request) =>
        ReferenceEquals(PendingFlipTarget, request) &&
        deferredBattle is null && SupportsQueuedMonsterSelection(request) &&
        (Phase == DemoPhase.Main || HasQueuedBattleTargetChoice);

    internal bool TryGetQueuedFlipSource(DemoFlipTarget request, out CardTarget source)
    {
        source = default;
        if (!UsesQueuedMonsterFlipSelection(request) ||
            primitiveActionQueue is not { IsBusy: false } || !CanAdmitQueuedMonsterFlipAnswer())
            return false;
        if (resumedSummonMonsterFlip is { } resumed && ReferenceEquals(resumed.Request, request))
        {
            source = resumed.Source;
            return true;
        }
        return TryCaptureCardTarget(request.Owner, DuelZone.Monster, request.SourceZone, out source) &&
            MatchesQueuedMonsterFlipSource(request, source);
    }

    private bool MatchesQueuedMonsterFlipSource(DemoFlipTarget request, CardTarget source) =>
        resumedSummonMonsterFlip is { } resumed && ReferenceEquals(resumed.Request, request)
            ? source == resumed.Source
            : source.Zone == DuelZone.Monster && source.Side == request.Owner &&
              TryResolveCardTarget(source, out int slot, out var card) &&
              slot == request.SourceZone && ReferenceEquals(card, request.SourceCard);

    public bool TrySelectQueuedMonsterFlipTarget(DemoFlipTarget request, int player, int zone,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a current eligible FLIP target.";
        if (!TryGetQueuedFlipSource(request, out var source)) return false;
        if (SuppressesQueuedMonsterSelection(request))
            return TryQueueMonsterFlipAnswer(source, [], out message, origin);

        var choice = QueuedMonsterFlipCandidates().FirstOrDefault(c => c.Target.Side == player && c.Zone == zone);
        if (choice is null) return false;
        if (request.Effect != DemoFlipEffect.ReturnUpToTwo)
            return TryQueueMonsterFlipAnswer(source, [choice.Target], out message, origin);

        if (queuedFlipFirstPick is { } first && ReferenceEquals(first.Request, request))
        {
            if (first.Source != source ||
                !TryPlanQueuedMonsterFlipAnswer(source, [first.Target], out _, out _))
            {
                // A stale pick cannot silently transfer a replacement card.
                queuedFlipFirstPick = null;
                flipSelections.Clear();
                message = "First FLIP target changed; choose again.";
                return false;
            }
            if (!TryQueueMonsterFlipAnswer(source, [first.Target, choice.Target], out message, origin))
                return false;
            queuedFlipFirstPick = null;
            return true;
        }

        if (flipSelections.Count != 0 ||
            !TryPlanQueuedMonsterFlipAnswer(source, [choice.Target], out _, out _)) return false;
        queuedFlipFirstPick = (request, source, choice.Target);
        flipSelections.Add(new(player, zone, choice.Card));
        message = "FLIP: selected 1/2 target(s). Choose another or Done.";
        return true;
    }

    public bool TryFinishQueuedMonsterFlipSelection(DemoFlipTarget request, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "No optional queued FLIP selection.";
        if (!TryGetQueuedFlipSource(request, out var source)) return false;
        if (SuppressesQueuedMonsterSelection(request))
            return TryQueueMonsterFlipAnswer(source, [], out message, origin);
        if (request.Effect != DemoFlipEffect.ReturnUpToTwo) return false;

        CardTarget[] targets = [];
        if (queuedFlipFirstPick is { } first && ReferenceEquals(first.Request, request))
        {
            if (first.Source != source ||
                !TryPlanQueuedMonsterFlipAnswer(source, [first.Target], out _, out _))
            {
                queuedFlipFirstPick = null;
                flipSelections.Clear();
                message = "First FLIP target changed; choose again.";
                return false;
            }
            targets = [first.Target];
        }
        else if (flipSelections.Count != 0) return false;
        if (!TryQueueMonsterFlipAnswer(source, targets, out message, origin)) return false;
        queuedFlipFirstPick = null;
        return true;
    }

    private bool CanAdmitQueuedMonsterFlipAnswer() =>
        !GameOver && deferredBattle is null && (Phase == DemoPhase.Main ||
            HasQueuedBattleTargetChoice && queuedBattleReveal is { } battle && ValidateBattleReveal(battle,faceDown:false)) &&
        PendingFlipTarget is { } pending && SupportsQueuedMonsterSelection(pending) &&
        PendingFlipSpellTrapTargets is null && PendingCardSelection is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    public IReadOnlyList<QueuedMonsterFlipCandidate> QueuedMonsterFlipCandidates()
    {
        if (primitiveActionQueue?.IsBusy == true || GameOver || PendingFlipTarget is not { } pending ||
            !SupportsQueuedMonsterSelection(pending) || SuppressesQueuedMonsterSelection(pending)) return [];
        return CurrentMonsterFlipCandidates(pending).ToList().AsReadOnly();
    }

    private IEnumerable<QueuedMonsterFlipCandidate> CurrentMonsterFlipCandidates(DemoFlipTarget pending)
    {
        bool bounce = IsBounce(pending);
        bool positionOnly = pending.Effect == DemoFlipEffect.ChangeFaceUpPosition;
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
        {
            if (Monsters[p,z] is not Card card || !IsEligibleTargetSide(pending, p) ||
                bounce && IsMarkedForBattleDestruction(p,z) ||
                !TryCaptureCardTarget(p,DuelZone.Monster,z,out var target)) continue;
            if (positionOnly && MonsterFaceDown[p, z] ||
                MovesMonsterFlipTarget(pending) && !TryPlanRemovalLinks(p,z,out _,toGraveyard: !bounce)) continue;
            yield return new(target, z, card, MonsterFaceDown[p, z]);
        }
    }

    /// <summary>Admit a target answer for a supported monster effect.
    /// The legacy request type also carries the Dream Clown position trigger.</summary>
    public bool TryQueueMonsterFlipAnswer(CardTarget source, IReadOnlyList<CardTarget> targets,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a supported monster effect and valid distinct occurrences.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !CanAdmitQueuedMonsterFlipAnswer() || targets is null ||
            !TryPlanQueuedMonsterFlipAnswer(source, targets, out _, out _))
            return false;
        // The caller's list is copied into immutable command values.
        var trigger = PendingFlipTarget!.Trigger;
        DuelCommand answer = trigger switch
        {
            DemoMonsterEffectTrigger.PositionChange => new ResolveMonsterPositionTargetCommand(source.Side, source, targets[0]),
            DemoMonsterEffectTrigger.NormalOrFlipSummon => new ResolveMonsterSummonTargetCommand(source.Side, source, targets[0]),
            _ => new ResolveMonsterFlipTargetsCommand(source.Side, source,
                targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null)
        };
        admittingQueuedMonsterFlipAnswer = true;
        try { queue.Enqueue(answer, origin); }
        finally { admittingQueuedMonsterFlipAnswer = false; }
        message = trigger switch
        {
            DemoMonsterEffectTrigger.PositionChange => "Position effect answer admitted.",
            DemoMonsterEffectTrigger.NormalOrFlipSummon => "Summon effect answer admitted.",
            _ => "FLIP answer admitted."
        };
        return true;
    }

    private bool TryPlanQueuedMonsterFlipAnswer(CardTarget source, IReadOnlyList<CardTarget> targets,
        out DuelCommand[] batch, out Dictionary<CardTarget,DuelCommand[]> plans)
    {
        batch = [];
        plans = new();
        if (!CanAdmitQueuedMonsterFlipAnswer() || PendingFlipTarget is not { } pending ||
            !MatchesQueuedMonsterFlipSource(pending, source))
            return false;
        // Royal Command suppresses the pending effect. Accept only an empty
        // answer in that case; no target movement or legacy battle resolver.
        if (SuppressesQueuedMonsterSelection(pending)) return targets.Count == 0;
        bool bounce = IsBounce(pending);
        if (targets.Count > 2 || (pending.Effect != DemoFlipEffect.ReturnUpToTwo && targets.Count != 1) ||
            targets.Distinct().Count() != targets.Count) return false;
        var result = new List<DuelCommand>();
        foreach (var target in targets)
        {
            if (target.Zone != DuelZone.Monster || !IsEligibleTargetSide(pending, target.Side) ||
                !TryResolveCardTarget(target,out int slot,out _)) return false;
            if (pending.Effect == DemoFlipEffect.ChangeFaceUpPosition)
            {
                if (MonsterFaceDown[target.Side, slot]) return false;
                result.Add(new ChangePositionByFlipCommand(target.Side, target, !DefensePosition[target.Side, slot]));
                continue;
            }
            if (pending.Effect == DemoFlipEffect.PreventMonsterAttack)
            {
                result.Add(new BindMonsterAttackCommand(source.Side, source, target));
                continue;
            }
            if (bounce && IsMarkedForBattleDestruction(target.Side,slot) ||
                !TryPlanRemovalLinks(target.Side,slot,out var links,toGraveyard: !bounce))
                return false;
            plans.Add(target,links);
            result.Add(bounce ? new ReturnToHandCommand(target.Side,target) : new DestroyCommand(target.Side,target));
            result.AddRange(links);
        }
        batch = result.ToArray();
        return true;
    }
}
