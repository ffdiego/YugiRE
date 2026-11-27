namespace Joey.Core;

public sealed partial class DemoDuel
{
    private static bool IsRevivalSpell(Card card) =>
        card.ClassCode == 22 && Joey.Cards.RevivalSpellEffects.Supports(card.Id);

    private static bool IsRevivalSpellMonster(Card card) =>
        card.IsMonster && NormalSummonRules.Category(card) == 0 && NormalMonsterCatalog.Contains(card.Id);

    private static bool IsRevivalSpellRequest(PendingCardSelection request) =>
        request is { Source: CardSelectionSource.Graveyard, Destination: CardSelectionDestination.Field,
            MinSelections: 1, MaxSelections: 1, Filter: CardSelectionFilter.AnyMonster,
            EffectResourceId: Joey.Cards.RevivalSpellEffects.MonsterReborn, MaySelectFaceDownDefense: false };

    private bool CanResolveRevivalSpell(CardTarget source)
    {
        if (LegacyActionsBlocked || PendingEndDiscards != 0 || source.Side != ActivePlayer ||
            Phase != DemoPhase.Main || !TryResolveCardTarget(source, out int index, out var card) ||
            !IsRevivalSpell(card) || FirstFreeMonsterZone() < 0 ||
            !graves[source.Side].Any(IsRevivalSpellMonster)) return false;
        return source.Zone == DuelZone.Hand ? FirstFreeSpellZone() >= 0 :
            source.Zone == DuelZone.SpellTrap && SpellFaceDown[source.Side,index] &&
            !equipment.Any(e => e.Owner == source.Side && e.SourceZone == index);
    }

    private bool TryQueueRevivalSpell(CardTarget source, out string message, ActionOrigin origin)
    {
        message = "Choose a revival spell, free monster zone and eligible own Graveyard monster.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            !CanResolveRevivalSpell(source)) return false;
        queue.Enqueue(new ActivateGraveyardRevivalSpellCommand(source.Side, source), origin);
        message = "Revival spell admitted.";
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private CardTarget? revivalSpellSource;
        private bool revivalSpellSpent;

        private CommandResult ActivateRevivalSpell(ActivateGraveyardRevivalSpellCommand command, IActionCommandSink followups)
        {
            if (revivalSpellSource is not null || followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.CanResolveRevivalSpell(command.Source))
                return new(CommandOutcome.Rejected, "Revival spell no longer legal.");
            revivalSpellSource = command.Source;
            revivalSpellSpent = false;
            queue.Enqueue(new SendToGraveyardCommand(command.Side, command.Source));
            queue.ScheduleContinuation(_ => new([new OpenGraveyardRevivalSpellCommand(command.Side, command.Source)]));
            return new(CommandOutcome.Applied);
        }

        private CommandResult OpenRevivalSpell(OpenGraveyardRevivalSpellCommand command)
        {
            if (revivalSpellSource != command.Source)
                return new(CommandOutcome.Rejected, "No matching revival spell reserved.");
            bool spent = revivalSpellSpent;
            revivalSpellSource = null;
            revivalSpellSpent = false;
            if (!spent || duel.GameOver) return new(CommandOutcome.Skipped, "Revival spell not spent.");
            var candidates = duel.graves[command.Side].Select((card,index) => new CardSelectionCandidate(index,card))
                .Where(c => IsRevivalSpellMonster(c.Card)).ToArray();
            if (candidates.Length == 0 || duel.FirstFreeMonsterZone() < 0)
                return new(CommandOutcome.Skipped, "No eligible monster or destination remains.");
            duel.selectedBatchIndices.Clear();
            duel.PendingCardSelection = new(command.Side, CardSelectionSource.Graveyard,
                CardSelectionDestination.Field, CardSelectionFilter.AnyMonster,
                Array.AsReadOnly(candidates), 1, 1, Joey.Cards.RevivalSpellEffects.MonsterReborn);
            return new(CommandOutcome.Applied);
        }
    }
}
