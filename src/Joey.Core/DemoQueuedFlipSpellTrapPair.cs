namespace Joey.Core;

public sealed partial class DemoDuel
{
    private bool admittingQueuedFlipSpellTrapPair;
    private (PendingFlipSpellTrapTargets Request, CardTarget Source)? resumedSummonFlipPair;

    private (PendingFlipSpellTrapTargets Request, CardTarget Target)? queuedFlipSpellTrapFirstPick;

    public bool UsesQueuedFlipSpellTrapSelection(PendingFlipSpellTrapTargets request) =>
        primitiveActionQueue is not null && ReferenceEquals(PendingFlipSpellTrapTargets, request) &&
        deferredBattle is null && Phase == DemoPhase.Main &&
        FlipSpellTrapSelectionRules.Effect(request.Source.Id) == request.Operation;

    public bool TrySelectQueuedFlipSpellTrapTarget(PendingFlipSpellTrapTargets request, int player, int zone,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a current eligible FLIP Spell/Trap target.";
        if (!UsesQueuedFlipSpellTrapSelection(request) ||
            primitiveActionQueue is not { IsBusy: false } || !CanAdmitQueuedFlipSpellTrapPair())
            return false;

        // The existing executor requires no legacy selections; only the
        // presentation pick is temporarily cleared during final admission.
        bool Admit(CardTarget[] picks, out string result)
        {
            var prior = flipSpellTrapSelections.ToArray();
            flipSpellTrapSelections.Clear();
            if (TryQueueFlipSpellTrapPair(request, picks, out result, origin))
            {
                queuedFlipSpellTrapFirstPick = null;
                return true;
            }
            flipSpellTrapSelections.AddRange(prior);
            return false;
        }

        if (FlipEffectsNegated)
            return Admit([], out message);

        if (queuedFlipSpellTrapFirstPick is { } first && ReferenceEquals(first.Request, request))
        {
            if (!TryResolveFlipSpellTrapPick(request, first.Target, out _, out int firstZone))
            {
                queuedFlipSpellTrapFirstPick = null;
                flipSpellTrapSelections.Clear();
                message = "First FLIP Spell/Trap target changed; choose again.";
                return false;
            }
            if (first.Target.Side == player && firstZone == zone)
            {
                queuedFlipSpellTrapFirstPick = null;
                flipSpellTrapSelections.Clear();
                message = "Deselected Spell/Trap target (0/2).";
                return true;
            }
            var second = QueuedFlipSpellTrapCandidates(request).FirstOrDefault(c =>
                c.Target.Side == player && c.Zone == zone);
            return second is not null && Admit([first.Target, second.Target], out message);
        }
        if (flipSpellTrapSelections.Count != 0) return false;
        var choice = QueuedFlipSpellTrapCandidates(request).FirstOrDefault(c =>
            c.Target.Side == player && c.Zone == zone);
        if (choice is null) return false;
        queuedFlipSpellTrapFirstPick = (request, choice.Target);
        flipSpellTrapSelections.Add(new(player, zone, choice.Card));
        message = "Choose one more distinct Spell/Trap target.";
        return true;
    }

    private bool CanAdmitQueuedFlipSpellTrapPair() =>
        !GameOver && deferredBattle is null && Phase == DemoPhase.Main &&
        PendingFlipSpellTrapTargets is { } pending &&
        FlipSpellTrapSelectionRules.Effect(pending.Source.Id) == pending.Operation &&
        PendingFlipTarget is null && PendingCardSelection is null &&
        PendingSummonResponse is null && PendingAttackResponse is null &&
        PendingEffectDecision is null && PendingSpellTrapTarget is null &&
        PendingFusionSummon is null && !deferringFieldRemovalTriggers && PendingEndDiscards == 0;

    public IReadOnlyList<QueuedSpellTrapCandidate> QueuedFlipSpellTrapCandidates(PendingFlipSpellTrapTargets request)
    {
        if (primitiveActionQueue?.IsBusy == true || !CanAdmitQueuedFlipSpellTrapPair() ||
            !ReferenceEquals(PendingFlipSpellTrapTargets, request) || FlipEffectsNegated)
            return [];
        return CurrentFlipSpellTrapCandidates(request).ToList().AsReadOnly();
    }

    private IEnumerable<QueuedSpellTrapCandidate> CurrentFlipSpellTrapCandidates(PendingFlipSpellTrapTargets request)
    {
        foreach (var candidate in request.EligibleTargets)
        {
            bool field = candidate.Zone == SpellTrapTargetRules.FieldZone;
            if (TryCaptureCardTarget(candidate.Player, field ? DuelZone.FieldSpell : DuelZone.SpellTrap,
                field ? 0 : candidate.Zone, out var target) &&
                TryResolveFlipSpellTrapPick(request, target, out _, out _))
                yield return new(target, candidate.Zone, candidate.Card,
                    IsSpellTrapFaceDown(candidate.Player, candidate.Zone));
        }
    }

    private bool TryGetFlipSpellTrapSource(PendingFlipSpellTrapTargets request, out CardTarget source)
    {
        if (resumedSummonFlipPair is { } resumed && ReferenceEquals(resumed.Request, request))
        {
            source = resumed.Source;
            return true;
        }

        source = default;
        for (int slot = 0; slot < 5; slot++)
        {
            if (ReferenceEquals(Monsters[request.Owner, slot], request.Source))
                return TryCaptureCardTarget(request.Owner, DuelZone.Monster, slot, out source);
        }
        return false;
    }

    public bool TryQueueFlipSpellTrapPair(PendingFlipSpellTrapTargets request, IReadOnlyList<CardTarget> targets,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose exactly two eligible distinct Spell/Trap occurrences.";
        if (primitiveActionQueue is not { IsBusy: false } queue || targets is null ||
            !TryPlanFlipSpellTrapPair(request, targets, out _, out _, out _)) return false;
        var answer = new ResolveFlipSpellTrapPairCommand(request.Owner, request,
            targets.Count > 0 ? targets[0] : null, targets.Count > 1 ? targets[1] : null);
        admittingQueuedFlipSpellTrapPair = true;
        try { queue.Enqueue(answer, origin); }
        finally { admittingQueuedFlipSpellTrapPair = false; }
        message = "FLIP pair admitted.";
        return true;
    }

    private bool TryResolveFlipSpellTrapPick(PendingFlipSpellTrapTargets request, CardTarget target,
        out Card card, out int zone)
    {
        card = null!;
        zone = -1;
        if (target.Zone is not (DuelZone.SpellTrap or DuelZone.FieldSpell) ||
            !TryResolveCardTarget(target, out int slot, out card) ||
            !SpellTrapRemovalLinkMatches(target.Side, target.Zone, slot, card)) return false;
        int visualZone = target.Zone == DuelZone.FieldSpell ? SpellTrapTargetRules.FieldZone : slot;
        Card selectedCard = card;
        zone = visualZone;
        return request.EligibleTargets.Any(t => t.Player == target.Side && t.Zone == visualZone &&
            ReferenceEquals(t.Card, selectedCard)) &&
            FlipSpellTrapSelectionRules.IsEligible(request.Source.Id, card, IsSpellTrapFaceDown(target.Side, visualZone));
    }

    private bool TryPlanFlipSpellTrapPair(PendingFlipSpellTrapTargets request, IReadOnlyList<CardTarget> targets,
        out CardTarget source, out DuelCommand[] batch, out CardTarget[] cascade)
    {
        source = default;
        batch = [];
        cascade = [];
        if (!CanAdmitQueuedFlipSpellTrapPair() || !ReferenceEquals(PendingFlipSpellTrapTargets, request) ||
            flipSpellTrapSelections.Count != 0 || targets.Count > 2 ||
            targets.Distinct().Count() != targets.Count || targets.Count == 1) return false;
        if (!TryGetFlipSpellTrapSource(request, out source)) return false;
        if (FlipEffectsNegated) return true;
        if (targets.Count != 2) return false;
        var commands = new List<DuelCommand>();
        var extra = new List<CardTarget>();
        var boardSides = new HashSet<int>();
        foreach (var target in targets)
        {
            if (!TryResolveFlipSpellTrapPick(request,target,out var card,out int zone)) return false;
            bool bounce = request.Operation == FlipSpellTrapOperation.ReturnToOwnerHand;
            commands.Add(bounce ? new ReturnToHandCommand(target.Side,target) : new DestroyCommand(target.Side,target));
            if (bounce && card.Id == DestinyBoardRules.BoardResourceId &&
                !IsSpellTrapFaceDown(target.Side,zone) &&
                DestinyBoardRules.HasActiveBoard(target.Side,Spells,SpellFaceDown)) boardSides.Add(target.Side);
        }
        foreach (int side in boardSides.Order())
        for (int z = 0; z < 5; z++)
        {
            if (Spells[side,z] is not Card piece || SpellFaceDown[side,z] || !IsDestinyBoardPiece(piece)) continue;
            if (!TryCaptureCardTarget(side,DuelZone.SpellTrap,z,out var target) ||
                !SpellTrapRemovalLinkMatches(side,DuelZone.SpellTrap,z,piece)) return false;
            if (targets.Contains(target)) continue;
            extra.Add(target);
            commands.Add(new DestroyCommand(side,target) { Cause = RemovalCause.LinkedCleanup });
        }
        cascade = extra.ToArray();
        batch = commands.ToArray();
        return true;
    }
}
