namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Admits an explicit Ritual choice from the active player's hand and field.</summary>
    public bool TryQueueRitualSummon(CardTarget spell, CardTarget monster, int slot,
        IReadOnlyList<CardTarget> materials, bool defense, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose a matching Ritual monster, eligible tributes and a usable destination.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked || materials is null)
            return false;
        var prepare = new PrepareRitualSummonCommand(spell.Side, spell, monster, slot, defense,
            Array.AsReadOnly(materials.ToArray()));
        if (!CanPrepareRitualSummon(prepare)) return false;
        queue.Enqueue(prepare, origin);
        message = "Ritual Summon admitted.";
        return true;
    }

    private bool CanPrepareRitualSummon(PrepareRitualSummonCommand prepare)
    {
        var (side, spell, monster, slot, _, materials) = prepare;
        if (side != ActivePlayer || LegacyActionsBlocked || PendingEndDiscards != 0 ||
            Phase != DemoPhase.Main || slot is < 0 or >= 5 ||
            spell.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) || monster.Side != side ||
            monster.Zone != DuelZone.Hand || materials.Count == 0 ||
            materials.Distinct().Count() != materials.Count ||
            (FirstFreeMonsterZone() < 0 && materials[0].Zone != DuelZone.Monster) ||
            !TryResolveCardTarget(spell, out int spellIndex, out var spellCard) ||
            !RitualSearchRules.TryGetBinding(spellCard, out var binding) ||
            !TryResolveCardTarget(monster, out _, out var monsterCard) ||
            !monsterCard.IsMonster || monsterCard.Id != binding.MonsterId)
            return false;
        if (spell.Zone == DuelZone.Hand ? FirstFreeSpellZone() < 0 :
            !SpellFaceDown[side, spellIndex] || equipment.Any(e => e.Owner == side && e.SourceZone == spellIndex))
            return false;

        int level = 0;
        int minimum = int.MaxValue;
        bool releasesDestination = false;
        foreach (var target in materials)
        {
            if (target == monster || target.Side != side || target.Zone is not (DuelZone.Hand or DuelZone.Monster) ||
                !TryResolveCardTarget(target, out int index, out var card) || !card.IsMonster || card.Level == 0)
                return false;
            level += card.Level;
            minimum = Math.Min(minimum, card.Level);
            releasesDestination |= target.Zone == DuelZone.Monster && index == slot;
        }

        return level >= binding.RequiredLevel && level - minimum < binding.RequiredLevel &&
            (Monsters[side, slot] is null || releasesDestination);
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private PrepareRitualSummonCommand? ritualSummon;
        private bool ritualSpellSpent;

        private CommandResult PrepareRitualSummon(PrepareRitualSummonCommand prepare, IActionCommandSink followups)
        {
            if (ritualSummon is not null || placement is not null || finishScheduled ||
                followups is not IActionBatchSink { PendingCount: 0 } queue ||
                !duel.CanPrepareRitualSummon(prepare))
                return new(CommandOutcome.Rejected, "Ritual source, materials or destination changed.");

            var batch = new List<DuelCommand> { new SendToGraveyardCommand(prepare.Side, prepare.Spell) };
            var plans = new Dictionary<CardTarget, DuelCommand[]>();
            foreach (var material in prepare.Materials)
            {
                batch.Add(new TributeCommand(prepare.Side, material));
                if (material.Zone != DuelZone.Monster) continue;
                if (!duel.TryResolveCardTarget(material, out int slot, out _) ||
                    !duel.TryPlanRemovalLinks(prepare.Side, slot, out var links))
                    return new(CommandOutcome.Rejected, "Ritual tribute is no longer eligible.");
                plans.Add(material, links);
                batch.AddRange(links);
            }
            batch.Add(new SpecialSummonCommand(prepare.Side, prepare.Monster, prepare.Slot, FaceUpDefense: prepare.Defense));
            queue.EnqueueBatch(batch);
            foreach (var pair in plans) plannedLinks.Add(pair.Key, pair.Value);
            ritualSummon = prepare;
            ritualSpellSpent = false;
            paidTributes.Clear();
            queue.ScheduleContinuation(_ => new([new FinishRitualSummonCommand(prepare.Side)]));
            return new(CommandOutcome.Applied);
        }

        private bool IsPaidRitualSummon(SpecialSummonCommand summon) =>
            ritualSummon is { } ritual && ritualSpellSpent && ritual.Monster == summon.Source &&
            ritual.Slot == summon.Slot && ritual.Defense == summon.FaceUpDefense && paidTributes.SetEquals(ritual.Materials);

        private CommandResult FinishRitualSummon(FinishRitualSummonCommand finish)
        {
            if (ritualSummon is not { } ritual || ritual.Side != finish.Side)
                return new(CommandOutcome.Rejected, "No matching Ritual Summon to finalize.");
            foreach (var material in ritual.Materials) plannedLinks.Remove(material);
            ritualSummon = null;
            ritualSpellSpent = false;
            paidTributes.Clear();
            duel.BeginNextQueuedDeckSearch();
            return new(CommandOutcome.Applied);
        }
    }
}
