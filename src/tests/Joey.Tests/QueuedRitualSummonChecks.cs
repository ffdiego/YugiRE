using Joey.Core;

internal static class QueuedRitualSummonChecks
{
    private static Card Monster(ushort id, int level, int category = 0) =>
        new(id, id, $"Monster {id}", "", (3u << 20) | ((uint)level << 25) | ((uint)category << 18));
    private static Card Spell(ushort id) => new(id, id, $"Spell {id}", "", 22u << 20);

    private sealed class TributeGate : IPresentationGate
    {
        public bool Released { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand entry, CommandResult result) =>
            entry.Command.GetType().Name != "TributeCommand" || Released;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 150 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Ritual queue did not drain.");
    }

    private static void CheckCpu(Action<bool, string> check)
    {
        var spell = Spell(670);
        var ritual = Monster(359, 8, 3);
        var material = Monster(29, 5);
        var fieldTribute = Monster(30, 3);
        var inert = Spell(65000);
        foreach (int side in new[] { 0, 1 })
        foreach (bool set in new[] { false, true })
        foreach (bool full in new[] { false, true })
        {
            Card?[] ownField = full
                ? [fieldTribute, Monster(31, 1), Monster(32, 1), Monster(33, 1), Monster(34, 1)]
                : [fieldTribute, null, null, null, null];
            var duel = DemoDuel.FromPartialObservation(side, [spell, ritual, ritual, material],
                side == 0 ? ownField : new Card?[5], side == 1 ? ownField : new Card?[5],
                8000, equipmentContextResolved: true, side0Deck: [inert], side1Deck: [inert]);
            check(duel.Draw(out _), "Ritual CPU fixture draws before the Main Phase");
            var gate = new TributeGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            if (set)
            {
                check(duel.TryQueueSpellTrapSet(0, out _), "Ritual CPU fixture Sets the Ritual spell");
                Drain(queue);
            }
            var handCount = duel.Hand.Count;
            duel.TryCaptureCardTarget(side, DuelZone.Hand, set ? 0 : 1, out var savedCopy);
            duel.TryCaptureCardTarget(side, DuelZone.Hand, set ? 1 : 2, out var otherCopy);
            var cpu = new CpuDuelController(side);
            var step = cpu.Step(duel, queueTurnActions: true);
            var admission = queue.Trace.LastOrDefault();
            check(step is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell } &&
                admission?.Entry.Origin == ActionOrigin.Cpu && duel.Hand.Count == handCount &&
                duel.Graveyard(side).Count == 0,
                $"Ritual CPU admits by occurrence without mutating hand or field (side {side}, Set {set}, full {full})");
            for (int tick = 0; tick < 14; tick++) queue.Tick();
            int slot = full ? 0 : 1;
            check(queue.IsBusy && duel.Monsters[side, slot] is null &&
                duel.Graveyard(side).Select(card => card.Id).SequenceEqual(
                    new[] { spell.Id, full ? fieldTribute.Id : ritual.Id }),
                "Ritual CPU waits at the first tribute gate and clears a slot first on a full field");
            gate.Released = true;
            Drain(queue);
            check(queue.Trace.Where(t => t.Stage == ActionTraceStage.Executed &&
                t.Entry.Command.GetType().Name == "TributeCommand")
                .All(t => t.Entry.Command is ICardRemovalCommand { Cause: RemovalCause.RitualMaterial }),
                "Ritual payments carry their own cause, including hand materials");
            check(duel.TryCaptureCardTarget(side, DuelZone.Monster, slot, out var summoned) &&
                summoned.Occurrence == savedCopy.Occurrence && !duel.DefensePosition[side, slot] &&
                duel.Graveyard(side).Select(card => card.Id).SequenceEqual(
                    full ? [spell.Id, fieldTribute.Id, material.Id] : [spell.Id, ritual.Id]) &&
                (full
                    ? duel.TryCaptureCardTarget(side, DuelZone.Hand, 0, out var retained) &&
                      retained.Occurrence == otherCopy.Occurrence
                    : duel.TryCaptureCardTarget(side, DuelZone.Graveyard, 1, out var paid) &&
                      paid.Occurrence == otherCopy.Occurrence),
                "Ritual CPU uses a minimal legal subset and preserves the selected physical Ritual monster");
        }
    }

    public static void Run(Action<bool, string> check, string? datPath)
    {
        CheckCpu(check);
        var pairs = new (ushort Spell, ushort Monster, int Level)[]
        {
            (672,355,7),(670,359,8),(669,363,8),(673,364,6),(675,700,6),(676,701,6),
            (678,703,7),(693,708,7),(691,709,8),(696,715,7),(695,716,8),(699,717,7),
            (698,718,6),(720,721,8),(958,730,1),(1416,1411,8)
        };
        using var archive = datPath is null ? null : new Archive(datPath);
        var catalog = archive is null ? null : Catalog.Load(archive);
        foreach (var (spellId, monsterId, level) in pairs)
        foreach (int side in new[] { 0, 1 })
        {
            Card spell = catalog?.First(c => c.Id == spellId) ?? Spell(spellId);
            Card ritual = catalog?.First(c => c.Id == monsterId) ?? Monster(monsterId, level, 3);
            Card tribute = Monster(40, level);
            var ownField = new Card?[] { tribute, Monster(41, 2), Monster(42, 2), Monster(43, 2), Monster(44, 2) };
            var duel = DemoDuel.FromPartialObservation(side, [spell, ritual],
                side == 0 ? ownField : new Card?[5], side == 1 ? ownField : new Card?[5], 8000);
            var observedTributes = new List<DemoMonsterTribute>();
            duel.MonsterTributed += observedTributes.Add;
            var gate = new TributeGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            duel.TryCaptureCardTarget(side, DuelZone.Hand, 0, out var source);
            duel.TryCaptureCardTarget(side, DuelZone.Hand, 1, out var summoned);
            duel.TryCaptureCardTarget(side, DuelZone.Monster, 0, out var material);
            duel.TryCaptureCardTarget(side, DuelZone.Monster, 1, out var small);
            check(NormalSummonRules.RequiresSpecialSummon(ritual) && !duel.CanSummonFromHand(1) && !duel.CanSetFromHand(1), $"Ritual {monsterId} is special-only");
            check(!duel.TryQueueRitualSummon(source, summoned, 1, [material], false, out _) &&
                !duel.TryQueueRitualSummon(source, summoned, 0, [material, small], false, out _) &&
                !duel.TryQueueRitualSummon(source, summoned, 0, [small], false, out _),
                $"Ritual {spellId} rejects blocked slot and redundant or wrong tribute");
            bool defense = side == 1;
            check(duel.TryQueueRitualSummon(source, summoned, 0, [material], defense, out _) &&
                duel.Hand.Count == 2 && duel.Monsters[side, 0]?.Id == tribute.Id,
                $"Ritual {spellId} admission is nonmutating");
            for (int i = 0; i < 20; i++) queue.Tick();
            check(duel.Hand.Count == 1 && duel.Monsters[side, 0] is null && queue.IsBusy &&
                duel.Graveyard(side).Select(c => c.Id).SequenceEqual(new[] { spell.Id, tribute.Id }),
                $"Ritual {spellId} waits after paying tribute");
            check(observedTributes.Count == 1 && observedTributes[0].Cause == RemovalCause.RitualMaterial &&
                observedTributes[0].Card == tribute, "Ritual field snapshot is not effect destruction or normal tribute");
            gate.Released = true;
            Drain(queue);
            check(duel.TryCaptureCardTarget(side, DuelZone.Monster, 0, out var placed) &&
                placed.Occurrence == summoned.Occurrence && !duel.MonsterFaceDown[side, 0] &&
                duel.DefensePosition[side, 0] == defense && duel.Graveyard(1 - side).Count == 0,
                $"Ritual {spellId} places selected physical copy in chosen face-up position");
        }

        var setSpell = Spell(669);
        var summon = Monster(363, 8, 3);
        var tributeInHand = Monster(35, 4);
        var onField = Monster(36, 6);
        var d = DemoDuel.FromPartialObservation(0, [setSpell, summon, tributeInHand],
            [onField, null, null, null, null], new Card?[5], 8000);
        var q = d.CreatePrimitiveActionQueue();
        check(d.TryQueueSpellTrapSet(0, out _), "Set Ritual activation fixture");
        Drain(q);
        d.TryCaptureCardTarget(0, DuelZone.SpellTrap, 0, out var set);
        d.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var ritualCopy);
        d.TryCaptureCardTarget(0, DuelZone.Hand, 1, out var handCost);
        d.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var fieldCost);
        q.Enqueue(new SpecialSummonCommand(0, ritualCopy, 1));
        Drain(q);
        check(d.Monsters[0, 1] is null, "Bare Hand Special Summon cannot bypass Ritual cost");
        check(!d.TryQueueRitualSummon(set, ritualCopy, 1, [handCost, handCost], false, out _) &&
            !d.TryQueueRitualSummon(set, ritualCopy, 1, [ritualCopy], false, out _) &&
            d.TryQueueRitualSummon(set, ritualCopy, 1, [handCost, fieldCost], false, out _),
            "Set Ritual uses mixed materials only once each and preserves selected monster");
        Drain(q);
        check(d.Monsters[0, 1]?.Id == summon.Id && d.Graveyard(0).Select(c => c.Id)
            .SequenceEqual(new[] { setSpell.Id, tributeInHand.Id, onField.Id }),
            "Set Ritual resolves with 4+6 for level 8 and both tributes reach the graveyard");

        var stale = DemoDuel.FromPartialObservation(0, [setSpell, summon, summon],
            new Card?[5], new Card?[5], 8000);
        var staleQueue = stale.CreatePrimitiveActionQueue();
        stale.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var staleSpell);
        stale.TryCaptureCardTarget(0, DuelZone.Hand, 1, out var preserved);
        stale.TryCaptureCardTarget(0, DuelZone.Hand, 2, out var duplicate);
        check(stale.TryQueueRitualSummon(staleSpell, preserved, 2, [duplicate], false, out _),
            "A second occurrence of the Ritual monster may pay its materials.");
        stale.Monsters[0, 2] = Monster(99, 2);
        Drain(staleQueue);
        check(stale.Hand.Count == 3 && stale.Graveyard(0).Count == 0 && stale.Monsters[0, 2]?.Id == 99,
            "A destination change before execution rejects the entire Ritual reservation.");

        var consoleDuel = DemoDuel.FromPartialObservation(1, [setSpell, summon, tributeInHand],
            new Card?[5], [onField, null, null, null, null], 8000);
        var console = new DuelConsoleSession(consoleDuel);
        check(!console.Execute("ritual hand 0 1 2 defense hand 2 field").Enqueued &&
            !console.Execute("ritual hand 0 1 2 defense hand 2 field 5").Enqueued &&
            !console.Queue.IsBusy, "Reject invalid Ritual console material syntax and slot.");
        check(console.Execute("ritual hand 0 1 2 defense hand 2 field 0").Enqueued &&
            consoleDuel.Hand.Count == 3 && consoleDuel.Monsters[1, 2] is null,
            "Console admits a Ritual through the same nonmutating API.");
        Drain(console.Queue);
        check(consoleDuel.Monsters[1, 2]?.Id == summon.Id && consoleDuel.DefensePosition[1, 2],
            "Console Ritual resolves in face-up defense on the active side.");
    }
}
