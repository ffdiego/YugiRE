namespace Joey.Core;

public sealed partial class DemoDuel
{
    private ActionQueue? primitiveActionQueue;

    /// <summary>
    /// One queue per duel. Effect-level operations, not a general legal input API.
    /// Existing App/CPU entrypoints remain legacy; do not mix their decisions
    /// with admitted queue work. Direct array writes are fixture-only.
    /// </summary>
    public ActionQueue CreatePrimitiveActionQueue(IPresentationGate? gate = null)
    {
        if (primitiveActionQueue is not null)
            throw new InvalidOperationException("A duel already owns an action queue.");
        return primitiveActionQueue = new(new PrimitiveActionExecutor(this), gate);
    }

    /// <summary>
    /// Opt-in legal hand activation for Raigeki only. No mutation at admission.
    /// The source and live target snapshot are revalidated in the executor.
    /// </summary>
    public bool TryQueueRaigekiFromHand(int index, out string message)
    {
        message = "Raigeki requires an idle duel queue and a legal hand activation.";
        if (primitiveActionQueue is not { IsBusy: false } queue ||
            !CanActivateFromHand(index) || Hand[index].Id != MonsterWipeRules.Raigeki ||
            Hand[index].ClassCode != 22 ||
            !TryCaptureCardTarget(ActivePlayer, DuelZone.Hand, index, out var source))
            return false;

        var targets = MonsterWipeRules.Targets(MonsterWipeRules.Raigeki, ActivePlayer, Monsters, MonsterFaceDown);
        if (targets.Any(t => !CanQueueMonsterRemoval(t.Player, t.Zone) ||
            !TryCaptureCardTarget(t.Player, DuelZone.Monster, t.Zone, out _)))
        {
            message = "Raigeki pilot cannot resolve an untracked occurrence or a removal with unmigrated side effects.";
            return false;
        }
        queue.Enqueue(new ActivateRaigekiCommand(ActivePlayer, source), ActionOrigin.Player);
        message = "Raigeki admitted to action queue.";
        return true;
    }

    // The legacy removal helper can also mutate LP or remove attached equips.
    // Explicitly refuse those cases until those effects have their own commands.
    // Graveyard choices are deferred and handed off after all removals finish.
    private bool CanQueueMonsterRemoval(int player, int slot) =>
        Monsters[player, slot] is Card card &&
        !ImmortalThunderRules.LosesLifeOnFieldToGraveyard(card.Id, MonsterWasFlipped[player, slot]) &&
        !equipment.Any(e => e.TargetPlayer == player && e.TargetZone == slot);

    private sealed class PrimitiveActionExecutor(DemoDuel duel) : IDuelCommandExecutor
    {
        private sealed class Wipe(int caster, IEnumerable<CardTarget> targets)
        {
            public int Caster { get; } = caster;
            public HashSet<CardTarget> Targets { get; } = new(targets);
            public int[] Counts { get; } = [0, 0];
            public int FaceDown { get; set; }
        }

        private Wipe? wipe;
        private bool finishScheduled;

        public CommandResult Execute(DuelCommand command, IActionCommandSink followups)
        {
            // The legacy public input gate also blocks when the queue is busy.
            // The executor itself must use only the legacy rule blockers.
            if (command is FinishFieldRemovalCommand)
                return FinishRemoval();
            if (duel.LegacyActionsBlocked)
                return new(CommandOutcome.Rejected, "Duel has ended or a legacy decision is pending.");

            switch (command)
            {
                case DrawCommand draw:
                    if (duel.decks[draw.Side].Count == 0)
                    {
                        duel.Winner = 1 - draw.Side;
                        return new(CommandOutcome.Applied, "Empty deck: player loses (sandbox policy).");
                    }
                    duel.DrawToHand(draw.Side);
                    return new(CommandOutcome.Applied);
                case DamageCommand damage:
                    duel.ChangeLife(damage.Side, -damage.Amount);
                    return new(CommandOutcome.Applied);
                case HealCommand heal:
                    duel.ChangeLife(heal.Side, heal.Amount);
                    return new(CommandOutcome.Applied);
                case ActivateRaigekiCommand activate:
                    return ActivateRaigeki(activate, followups);
                case SendToGraveyardCommand send when send.Target.Zone == DuelZone.Hand:
                    return SendHandToGraveyard(send.Target, discard: false);
                case DiscardCommand discard:
                    return SendHandToGraveyard(discard.Target, discard: true);
                case DestroyCommand destroy when destroy.Target.Zone == DuelZone.Monster:
                    return DestroyMonster(destroy.Target, followups);
                default:
                    return new(CommandOutcome.Rejected, "Command not migrated to DemoDuel.");
            }
        }

        private CommandResult ActivateRaigeki(ActivateRaigekiCommand activate, IActionCommandSink followups)
        {
            // A narrow pilot: other input is not admitted ahead of its batch.
            if (wipe is not null || finishScheduled ||
                followups is not ActionQueue { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Raigeki requires an isolated activation.");
            int side = activate.Side;
            if (!duel.TryResolveCardTarget(activate.Source, out int index, out var spell))
                return new(CommandOutcome.Skipped, "Source occurrence has left its hand.");
            if (duel.ActivePlayer != side || duel.Phase != DemoPhase.Main ||
                duel.FirstFreeSpellZone() < 0 || spell.Id != MonsterWipeRules.Raigeki || spell.ClassCode != 22)
                return new(CommandOutcome.Rejected, "Raigeki is no longer legal.");

            var snapshot = MonsterWipeRules.Targets(spell.Id, side, duel.Monsters, duel.MonsterFaceDown);
            if (snapshot.Count == 0)
                return new(CommandOutcome.Rejected, "No eligible opposing monsters.");
            var targets = new List<CardTarget>();
            foreach (var item in snapshot)
            {
                if (!duel.CanQueueMonsterRemoval(item.Player, item.Zone) ||
                    !duel.TryCaptureCardTarget(item.Player, DuelZone.Monster, item.Zone, out var target))
                    return new(CommandOutcome.Rejected, "Untracked occurrence or unmigrated removal side effects.");
                targets.Add(target);
            }
            // Use the existing sandbox target order, not an invented visual order.
            // Each actual movement receives its own command. No spell/monster
            // movement happens in this activation producer.
            queue.EnqueueBatch(new DuelCommand[] { new SendToGraveyardCommand(side, activate.Source) }
                .Concat(targets.Select(t => (DuelCommand)new DestroyCommand(t.Side, t))));
            wipe = new(side, targets);
            ScheduleFinish(side, followups);
            return new(CommandOutcome.Applied);
        }

        private CommandResult SendHandToGraveyard(CardTarget target, bool discard)
        {
            if (!duel.TryResolveCardTarget(target, out int index, out var card))
                return new(CommandOutcome.Skipped, "Hand occurrence is no longer present.");
            duel.hands[target.Side].RemoveAt(index);
            duel.graves[target.Side].Add(card);
            // Match legacy activation (no discard notification for a spent Spell).
            if (discard) duel.HandTransferred?.Invoke(new(target.Side, index, card, FromDeck: false));
            return new(CommandOutcome.Applied);
        }

        private CommandResult DestroyMonster(CardTarget target, IActionCommandSink followups)
        {
            if (!duel.TryResolveCardTarget(target, out int slot, out _))
                return new(CommandOutcome.Skipped, "Monster occurrence is no longer present.");
            if (!duel.CanQueueMonsterRemoval(target.Side, slot))
                return new(CommandOutcome.Rejected, "Removal has unmigrated linked or LP effects.");
            bool hidden = duel.MonsterFaceDown[target.Side, slot];
            duel.deferringFieldRemovalTriggers = true;
            try { duel.RemoveMonster(target.Side, slot); }
            finally { duel.deferringFieldRemovalTriggers = false; }
            if (wipe is { } current && current.Targets.Remove(target))
            {
                current.Counts[target.Side]++;
                if (hidden) current.FaceDown++;
            }
            ScheduleFinish(target.Side, followups);
            return new(CommandOutcome.Applied);
        }

        private void ScheduleFinish(int side, IActionCommandSink followups)
        {
            if (finishScheduled) return;
            finishScheduled = true;
            // This continuation proposes a command, without mutating state.
            // It runs only after the FIFO and its last presentation gate drain.
            followups.ScheduleContinuation(_ => new([new FinishFieldRemovalCommand(side)]));
        }

        private CommandResult FinishRemoval()
        {
            if (!finishScheduled) return new(CommandOutcome.Rejected, "No field removal to finalize.");
            finishScheduled = false;
            if (wipe is { } current)
            {
                duel.LastMonsterWipe = new(MonsterWipeRules.Raigeki, current.Caster,
                    current.Counts[0], current.Counts[1], current.FaceDown);
                wipe = null;
            }
            // Legacy choice bridge only; selection mutations are still A06/C12.
            // No search or optional prompt opens between the destruction commands.
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }
    }
}
