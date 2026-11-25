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
            monster.Side != side ||
            monster.Zone != DuelZone.Hand || materials.Count == 0 ||
            materials.Distinct().Count() != materials.Count ||
            (FirstFreeMonsterZone() < 0 && materials[0].Zone != DuelZone.Monster) ||
            !TryGetQueuedRitualBinding(spell, out var binding) ||
            !TryResolveCardTarget(monster, out _, out var monsterCard) ||
            !monsterCard.IsMonster || monsterCard.Id != binding.MonsterId)
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

    private bool TryGetQueuedRitualBinding(CardTarget spell, out RitualSearchRules.Binding binding)
    {
        binding = default;
        if (spell.Side != ActivePlayer || spell.Zone is not (DuelZone.Hand or DuelZone.SpellTrap) ||
            !TryResolveCardTarget(spell, out int index, out var card) ||
            !RitualSearchRules.TryGetBinding(card, out binding)) return false;
        return spell.Zone == DuelZone.Hand ? FirstFreeSpellZone() >= 0 :
            SpellFaceDown[spell.Side, index] && !equipment.Any(e => e.Owner == spell.Side && e.SourceZone == index);
    }

    internal PrepareRitualSummonCommand? SuggestQueuedRitualSummon()
    {
        if (primitiveActionQueue is not { IsBusy: false } || ActionsBlocked || Phase != DemoPhase.Main)
            return null;

        int side = ActivePlayer;
        int freeSlot = FirstFreeMonsterZone();
        RitualMaterial[]? monstersInHand = null;
        RitualMaterial[]? materials = null;
        foreach (var zone in new[] { DuelZone.Hand, DuelZone.SpellTrap })
        {
            int count = zone == DuelZone.Hand ? Hand.Count : 5;
            for (int index = 0; index < count; index++)
            {
                var card = zone == DuelZone.Hand ? Hand[index] : Spells[side, index];
                if (card is null || !RitualSearchRules.IsRitualSpell(card) ||
                    !TryCaptureCardTarget(side, zone, index, out var spell) ||
                    !TryGetQueuedRitualBinding(spell, out var binding)) continue;

                monstersInHand ??= RitualMaterials(DuelZone.Hand).ToArray();
                foreach (var monster in monstersInHand.Where(material => Hand[material.Index].Id == binding.MonsterId))
                {
                    materials ??= monstersInHand.Concat(RitualMaterials(DuelZone.Monster))
                        .OrderByDescending(material => material.Level).ToArray();
                    var plan = SuggestRitualMaterials(spell, monster.Target, materials, binding.RequiredLevel, freeSlot);
                    if (plan is not null) return plan;
                }
            }
        }
        return null;
    }

    private sealed record RitualMaterial(CardTarget Target, int Index, int Level);

    private IEnumerable<RitualMaterial> RitualMaterials(DuelZone zone)
    {
        int count = zone == DuelZone.Hand ? Hand.Count : 5;
        for (int index = 0; index < count; index++)
        {
            var card = zone == DuelZone.Hand ? Hand[index] : Monsters[ActivePlayer, index];
            if (card is not { IsMonster: true, Level: > 0 } ||
                !TryCaptureCardTarget(ActivePlayer, zone, index, out var target) ||
                zone == DuelZone.Monster && !TryPlanRemovalLinks(ActivePlayer, index, out _)) continue;
            yield return new(target, index, card.Level);
        }
    }

    private PrepareRitualSummonCommand? SuggestRitualMaterials(CardTarget spell, CardTarget monster,
        IReadOnlyList<RitualMaterial> materials, int requiredLevel, int freeSlot)
    {
        // Retain one unfinished selection per subtotal and field presence.
        // Materials arrive largest first, so the one reaching the threshold
        // is the smallest and its absence proves the selection is minimal.
        var partial = new RitualMaterial[requiredLevel, 2][];
        partial[0, 0] = [];
        foreach (var material in materials)
        {
            if (material.Target == monster) continue;
            bool fromField = material.Target.Zone == DuelZone.Monster;
            for (int subtotal = requiredLevel - 1; subtotal >= 0; subtotal--)
            for (int hasField = 0; hasField < 2; hasField++)
            {
                if (partial[subtotal, hasField] is not { } chosen) continue;
                int level = subtotal + material.Level;
                int nextHasField = fromField ? 1 : hasField;
                if (level < requiredLevel)
                {
                    partial[level, nextHasField] ??= [.. chosen, material];
                    continue;
                }
                if (freeSlot < 0 && nextHasField == 0) continue;

                RitualMaterial[] cost = [.. chosen, material];
                int slot = freeSlot;
                if (slot < 0)
                {
                    var first = cost.First(item => item.Target.Zone == DuelZone.Monster);
                    slot = first.Index;
                    cost = [first, .. cost.Where(item => item != first)];
                }
                var plan = new PrepareRitualSummonCommand(spell.Side, spell, monster, slot, false,
                    Array.AsReadOnly(cost.Select(item => item.Target).ToArray()));
                if (CanPrepareRitualSummon(plan)) return plan;
            }
        }
        return null;
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
