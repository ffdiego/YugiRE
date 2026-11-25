using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    // Explicit pilot. Unmigrated actions retain their existing producers.
    static bool useQueuedActions;
    static ActionQueue? queuedDuel;
    static CardTarget? queuedPreviewSource;

    sealed record ActionVisualSnapshot(DuelActionPresentation Action, Rectangle? Origin);

    sealed class AppActionPresentationGate : ICommandPresentationGate
    {
        private readonly ActiveCommandPresentation<ActionVisualSnapshot> active = new();
        private int startedTurn;

        // Draw has its own batch. A committed turn advance must release the
        // queue before the new turn can admit that batch.
        private bool WaitingForPhase => PhaseBusy && !queuedTurnDraw &&
            (active.Sequence is null || startedTurn == duel.Turn);

        public bool CanAdvance => !visualReviewPaused && !MotionBusy && !WaitingForPhase;

        public void OnStarting(QueuedDuelCommand command)
        {
            startedTurn = duel.Turn;
            commandCardEffects.Begin(command.Sequence);
            var description = duel.DescribeActionPresentation(command.Command);
            Rectangle? origin = null;
            if (description is { Source.Zone: DuelZone.Hand } hand)
                origin = hand.Source.Side == ViewPlayer ? HandBounds(hand.SourceIndex)
                    : CpuHandBounds(hand.SourceIndex, duel.HandFor(hand.Source.Side).Count);
            active.Capture(command.Sequence, description is null ? null : new(description, origin));
        }

        public bool CanComplete(QueuedDuelCommand command, CommandResult result)
        {
            if (!active.TryRelease(command.Sequence, result.Outcome == CommandOutcome.Applied,
                BeginActionVisual, () => MotionBusy || WaitingForPhase)) return false;
            commandCardEffects.Release(command.Sequence);
            if (queuedTurnDraw && command.Command is TurnDrawCommand)
            {
                message = result.Detail.Length != 0 ? result.Detail :
                    duel.ExodiaWinner == command.Command.Side ? "Exodia complete: all five Forbidden One pieces in hand." : "Drew one card.";
                ClearDuelSelection();
            }
            return true;
        }
    }

    static void BeginActionVisual(ActionVisualSnapshot snapshot)
    {
        var a = snapshot.Action;
        int side = a.Source.Side, slot = a.DestinationSlot;
        bool near = side == ViewPlayer;
        switch (a.Kind)
        {
            case ActionPresentationKind.DirectAttack:
            case ActionPresentationKind.TargetedAttack:
                // Direct attacks start the full cue after the click.
                // Queued targeted attacks currently come from the console.
                BeginAttackCue(side, a.SourceIndex, slot, alreadyAimed: false);
                break;
            case ActionPresentationKind.Activate:
                // Producer has emitted followups, but no source movement or
                // damage/destruction yet. Show the existing travel + glow.
                queuedPreviewSource = presentation.Style.PlayAnimations ? a.Source : null;
                BeginCardActivation(a.Card, ZoneBounds(side, slot, true), snapshot.Origin, near);
                break;
            case ActionPresentationKind.Summon:
            case ActionPresentationKind.Set:
                bool set = a.Kind == ActionPresentationKind.Set;
                bool spell = a.Destination == DuelZone.SpellTrap;
                ShowPlayedCardDetail(a.Card, hiddenFromPlayer: !near && set);
                if (snapshot.Origin is { } from && a.Destination != DuelZone.FieldSpell)
                {
                    handLayouts[side].RemoveAt(a.SourceIndex);
                    BeginFlight(a.Card, side, slot, from, spell, set);
                    if (set) QueueSetEffect(side, slot, spell);
                    else QueueNormalSummonEffect(a.Card, side, slot);
                }
                break;
            case ActionPresentationKind.Flip:
                ShowActivatedDetail(a.Card);
                var center = NativeDuelLayout.MonsterCenter(slot, near);
                BeginSpellActivation(center.X, center.Y, category: 1);
                break;
        }
    }

    static bool IsQueuedPreviewCard(int side, int index) =>
        queuedPreviewSource is { } source && source.Side == side && source.Zone == DuelZone.Hand &&
        duel.TryCaptureCardTarget(side, DuelZone.Hand, index, out var current) && source == current;

    static bool IsQueuedFixedSpell(Card card) => card.ClassCode == 22 &&
        (DrawSpellRules.Effect(card) is not null || LifePointSpellRules.Supports(card.Id) ||
         MonsterWipeRules.Scope(card.Id) is not null || SpellTrapWipeRules.Supports(card.Id) ||
         DeckSearchSpellRules.Filter(card.Id) is not null);

    static bool TryQueuedFixedSpell(Card card, DuelZone zone, int index, out string result) =>
        DrawSpellRules.Effect(card) is not null
            ? duel.TryQueueDrawSpell(zone, index, out result)
            : DeckSearchSpellRules.Filter(card.Id) is not null ? duel.TryQueueDeckSearchSpell(zone, index, out result)
            : LifePointSpellRules.Supports(card.Id) ? duel.TryQueueLifePointSpell(zone, index, out result)
            : MonsterWipeRules.Scope(card.Id) is not null ? duel.TryQueueMonsterWipe(zone, index, out result)
            : duel.TryQueueSpellTrapWipe(zone, index, out result);

    static void AdvanceQueuedDuel()
    {
        if (queuedDuel is not { IsBusy: true } queue || visualReviewPaused || alphaBlocked) return;
        try
        {
            // Only one scheduler passage per frame; the Core never reads time.
            if (labSession is null && !ConsoleCapturesInput && queue.PendingDecision is { } request)
            {
                var answer = EffectDecisionPolicies.Choose(optionalEffectPrompts, request,
                    effect => effect.Kind == EffectDecisionKind.DrawOneOnFlip
                        ? EffectDecisionAnswer.Accept : EffectDecisionAnswer.Decline);
                if (answer is { } choice) queue.SubmitDecision(request.Id, choice);
            }
            queue.Tick();
            if (!queue.IsBusy)
            {
                queuedPreviewSource = null;
                queuedTurnDraw = false;
            }
        }
        catch (InvalidOperationException ex)
        {
            alphaBlocked = true;
            message = "Queued action fault: " + ex.Message;
        }
    }
}
