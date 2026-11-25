namespace Joey.Core;

internal static class TemporaryStatRules
{
    internal readonly record struct Modifier(int ClassCode, int Attack, int Defense);

    internal static Modifier? For(ushort resourceId) => resourceId switch
    {
        1015 => new(21, 500, 0),
        1016 => new(21, 0, 500),
        1075 => new(22, 700, 0),
        1076 => new(22, 0, 700),
        1082 => new(21, 0, -500),
        _ => null
    };
}

public sealed partial class DemoDuel
{
    private readonly Dictionary<Card, NativeAiAttributeDelta> temporaryStats = new(ReferenceEqualityComparer.Instance);

    public bool TryQueueTemporaryStatModifier(CardTarget source, CardTarget target, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible temporary modifier and face-up monster.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            !CanActivateTemporaryStatModifier(source, target, out _))
            return false;
        queue.Enqueue(new ActivateTemporaryStatCommand(source.Side, source, target, Turn), origin);
        message = "Temporary attribute change admitted.";
        return true;
    }

    private bool CanActivateTemporaryStatModifier(CardTarget source, CardTarget target,
        out TemporaryStatRules.Modifier modifier)
    {
        modifier = default;
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || Phase is not (DemoPhase.Main or DemoPhase.Battle) ||
            source.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryResolveCardTarget(source, out int index, out var card) ||
            TemporaryStatRules.For(card.Id) is not { } found || found.ClassCode != card.ClassCode ||
            !TryResolveTemporaryStatTarget(target, card.ClassCode, out _))
            return false;

        if (source.Zone == DuelZone.Hand)
        {
            if (source.Side != ActivePlayer || card.ClassCode != 22 || FirstFreeSpellZone() < 0)
                return false;
        }
        else if (!SpellFaceDown[source.Side, index] || spellSetTurn[source.Side, index] >= Turn ||
                 equipment.Any(e => e.Owner == source.Side && e.SourceZone == index))
        {
            return false;
        }
        if (card.ClassCode == 21 && (FlipEffectSuppressionRules.JinzoFaceUp(Monsters, MonsterFaceDown) ||
            FlipEffectSuppressionRules.RoyalDecreeFaceUp(Spells, SpellFaceDown)))
            return false;

        modifier = found;
        return true;
    }

    private bool TryResolveTemporaryStatTarget(CardTarget target, int sourceClass, out Card card)
    {
        card = null!;
        if (target.Zone != DuelZone.Monster || !TryResolveCardTarget(target, out int slot, out card) ||
            MonsterFaceDown[target.Side, slot] || IsMarkedForBattleDestruction(target.Side, slot))
            return false;

        // Shared native candidate 0057AE20 consults 0056C510 and, for Spell
        // sources, 0056C460. The sandbox uses the DAT type and active field.
        if (card.ClassCode == 1)
        {
            for (int side = 0; side < 2; side++)
            for (int zone = 0; zone < 5; zone++)
                if (Monsters[side, zone]?.Id == 740 && !MonsterFaceDown[side, zone] &&
                    !IsMarkedForBattleDestruction(side, zone))
                    return false;
        }

        return sourceClass != 22 || card.Id is not (1326 or 1329) || ActiveFieldSpell?.Id != 333;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private ActivateTemporaryStatCommand? temporaryStatActivation;
        private TemporaryStatRules.Modifier temporaryStatModifier;
        private bool temporaryStatSourceSpent;

        private CommandResult ActivateTemporaryStat(ActivateTemporaryStatCommand activate, IActionCommandSink followups)
        {
            if (temporaryStatActivation is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                activate.ExpectedTurn != duel.Turn ||
                !duel.CanActivateTemporaryStatModifier(activate.Source, activate.Target, out var modifier))
                return new(CommandOutcome.Rejected, "Temporary modifier source, turn or target changed.");

            queue.EnqueueBatch([new SendToGraveyardCommand(activate.Side, activate.Source),
                new ResolveTemporaryStatCommand(activate.Side, activate.Source, activate.Target)]);
            temporaryStatActivation = activate;
            temporaryStatModifier = modifier;
            temporaryStatSourceSpent = false;
            return new(CommandOutcome.Applied);
        }

        private CommandResult ResolveTemporaryStat(ResolveTemporaryStatCommand resolve)
        {
            if (temporaryStatActivation is not { } activation || activation.Source != resolve.Source ||
                activation.Target != resolve.Target)
                return new(CommandOutcome.Rejected, "No matching temporary modifier reserved.");

            temporaryStatActivation = null;
            if (!temporaryStatSourceSpent || activation.ExpectedTurn != duel.Turn ||
                !duel.TryResolveTemporaryStatTarget(resolve.Target, temporaryStatModifier.ClassCode, out var target))
                return new(CommandOutcome.Skipped, "Temporary modifier source was not spent or target changed.");

            var prior = duel.temporaryStats.GetValueOrDefault(target);
            duel.temporaryStats[target] = new(
                prior.Attack + temporaryStatModifier.Attack, prior.Defense + temporaryStatModifier.Defense);
            return new(CommandOutcome.Applied);
        }
    }
}
