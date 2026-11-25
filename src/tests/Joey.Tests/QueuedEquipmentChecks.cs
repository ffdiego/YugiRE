using Joey.Core;

internal static class QueuedEquipmentChecks
{
    private static Card Monster(int idx, ushort id = 77) => new(idx, id, "Monster", "",
        (15u << 20) | (2u << 29) | (4u << 25) | (100u << 9) | 100u);
    private static Card Spell(ushort id) => new(id, id, "Spell", "", 22u << 20);
    private static void Drain(ActionQueue q)
    {
        for (int i = 0; i < 100 && q.IsBusy; i++) q.Tick();
        if (q.IsBusy) throw new Exception("Equipment queue failed to drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance { get; set; } = true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }
    public static void Run(Action<bool, string> check)
    {
        foreach (int owner in new[] { 0, 1 })
        foreach (int targetSide in new[] { 0, 1 })
        foreach (ushort id in new ushort[] { 300, 656, 1449, 1450 })
        foreach (bool fromSet in new[] { false, true })
        {
            Card?[][] fields = [[Monster(42), Monster(43), null, null, null],
                                [Monster(44), Monster(45), null, null, null]];
            var duel = DemoDuel.FromPartialObservation(owner, [Spell(id)], fields[0], fields[1], 8000,
                equipmentContextResolved: true);
            var gate = new Gate { Release = true };
            var q = duel.CreatePrimitiveActionQueue(gate);
            if (fromSet)
            {
                check(duel.TryQueueSpellTrapSet(0, out _), $"Queued equipment {id}: set admission");
                Drain(q);
            }
            duel.MonsterFaceDown[owner, 1] = true;
            gate.Release = false;
            int prior = duel.Hand.Count;
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            check(duel.TryQueueEquipment(zone, 0, targetSide, 0, out _) &&
                duel.EquipmentCards.Count == 0 && duel.Hand.Count == prior,
                $"Queued equipment {id}: no mutation on admission");
            q.Tick(); q.Tick();
            check(duel.EquipmentCards.Count == 1 && duel.Spells[owner,0]?.Id == id &&
                !duel.SpellFaceDown[owner,0] && duel.Hand.Count == 0 && q.IsBusy,
                $"Queued equipment {id}: applies once while presentation waits");
            gate.Release = true; Drain(q);
            int atk = id switch { 300 => 1300, 1449 => 1800, 1450 => 1500, _ => 1000 };
            int def = atk;
            check(duel.AttackValue(targetSide,0) == atk && duel.DefenseValue(targetSide,0) == def,
                $"Queued equipment {id}: correct live stats, controller {owner}, target {targetSide}");
            if (id == 1449)
            {
                duel.MonsterFaceDown[owner,1] = false;
                check(duel.AttackValue(targetSide,0) == 2600 && duel.DefenseValue(targetSide,0) == 2600,
                    "United counts only face-up monsters on equipment controller side");
            }
            if (id == 656)
            {
                q.Enqueue(new DamageCommand(owner, 1000)); Drain(q);
                check(duel.AttackValue(targetSide,0) == 2000 && duel.DefenseValue(targetSide,0) == 1000,
                    "Megamorph doubles base ATK when controller trails");
                q.Enqueue(new HealCommand(owner, 2000)); Drain(q);
                check(duel.AttackValue(targetSide,0) == 500, "Megamorph halves base ATK when controller leads");
                q.Enqueue(new DamageCommand(owner, 1000)); Drain(q);
                check(duel.AttackValue(targetSide,0) == 1000, "Megamorph equality restores unmodified base");
            }
            check(duel.TryCaptureCardTarget(owner, DuelZone.SpellTrap, 0, out var source),
                "Queued equipment source occurrence captured");
            q.Enqueue(new DestroyCommand(owner, source)); Drain(q);
            check(duel.EquipmentCards.Count == 0 && duel.Spells[owner,0] is null &&
                duel.Graveyard(owner).Single().Id == id && duel.AttackValue(targetSide,0) == 1000,
                "Queued equipment destruction clears link and restores base");
        }
        // All 21 legacy P025 sources use the same commit primitive in
        // queue and legacy mode; preserve target restrictions and modifiers.
        (ushort Id, int Type, int Attribute)[] legacyCases =
        [
            (300,15,2), (307,11,2), (302,3,2), (304,10,2), (309,13,2),
            (311,17,2), (314,1,2), (315,19,2), (318,12,2), (321,2,2),
            (322,18,2), (324,7,2), (325,9,2), (326,16,2), (327,14,2),
            (301,15,2), (306,15,1), (308,15,3), (653,15,4),
            (1012,15,4), (1013,15,6)
        ];
        foreach (var fixture in legacyCases)
        foreach (int owner in new[] { 0, 1 })
        foreach (int targetSide in new[] { 0, 1 })
        {
            var equip = Spell(fixture.Id);
            var target = Monster(42) with { Properties =
                ((uint)fixture.Type << 20) | ((uint)fixture.Attribute << 29) |
                (4u << 25) | (100u << 9) | 100u };
            DemoDuel Setup() => DemoDuel.FromPartialObservation(owner, [equip],
                targetSide == 0 ? [target,null,null,null,null] : new Card?[5],
                targetSide == 1 ? [target,null,null,null,null] : new Card?[5],
                8000, equipmentContextResolved: true);
            var legacy = Setup();
            var queued = Setup();
            var queue = queued.CreatePrimitiveActionQueue();
            check(legacy.Equip(0, targetSide, 0, out _) &&
                queued.TryCaptureCardTarget(owner, DuelZone.Hand, 0, out var source) &&
                queued.QueuedEquipmentCandidates(source).Count == 1 &&
                queued.TryQueueEquipment(DuelZone.Hand, 0, targetSide, 0, out _),
                $"C12 P025 {fixture.Id}: legacy and queued admission");
            var entry = queue.Trace.Last(x => x.Stage == ActionTraceStage.Admitted).Entry;
            check(entry.Origin == ActionOrigin.Player && entry.Command is EquipCommand &&
                queued.DescribeActionPresentation(entry.Command) is
                    { Kind: ActionPresentationKind.Activate, Destination: DuelZone.SpellTrap, DestinationSlot: 0 } &&
                queued.Hand.Count == 1 && queued.EquipmentCards.Count == 0,
                $"C12 P025 {fixture.Id}: one activation projection, no immediate mutation");
            Drain(queue);
            check(queued.Hand.Count == legacy.Hand.Count &&
                queued.EquipmentCards.SequenceEqual(legacy.EquipmentCards) &&
                queued.Spells[owner,0] == legacy.Spells[owner,0] &&
                queued.AttackValue(targetSide,0) == legacy.AttackValue(targetSide,0) &&
                queued.DefenseValue(targetSide,0) == legacy.DefenseValue(targetSide,0),
                $"C12 P025 {fixture.Id}: same equipment state and attributes");
        }

        foreach (int side in new[] { 0, 1 })
        {
            var target = Monster(42);
            var d = DemoDuel.FromPartialObservation(side, [Spell(300)],
                side == 0 ? [target,null,null,null,null] : new Card?[5],
                side == 1 ? [target,null,null,null,null] : new Card?[5], 8000,
                equipmentContextResolved: true,
                side0Deck: side == 0 ? [Spell(999)] : null,
                side1Deck: side == 1 ? [Spell(999)] : null);
            var queue = d.CreatePrimitiveActionQueue();
            var controller = new CpuDuelController(side);
            check(controller.Step(d, queueTurnActions: true).Action == CpuAction.Draw,
                "C12 CPU equipment: turn draw");
            Drain(queue);
            check(controller.Step(d, queueTurnActions: true) is
                { Status: CpuStepStatus.Acted, Action: CpuAction.Equip, HandIndex: 0, TargetZone: 0 } &&
                d.EquipmentCards.Count == 0 && d.Hand.Count == 2 && queue.IsBusy,
                "C12 CPU equipment: admission only");
            check(queue.Trace.Last(x => x.Stage == ActionTraceStage.Admitted).Entry is
                { Origin: ActionOrigin.Cpu, Command: EquipCommand },
                "C12 CPU equipment: same typed command");
            Drain(queue);
            check(d.EquipmentCards.Count == 1 && d.Spells[side,0]?.Id == 300 &&
                d.Hand.Count == 1 && d.AttackValue(side,0) == 1300,
                "C12 CPU equipment: existing selection and bonus preserved");
        }

        var mage = DemoDuel.FromPartialObservation(0, [Spell(1450), Spell(300), Spell(FieldSpellRules.SogenId)],
            [Monster(42), null, null, null, null], new Card?[5], 8000, equipmentContextResolved:true);
        var mq = mage.CreatePrimitiveActionQueue();
        check(mage.TryQueueEquipment(DuelZone.Hand,0,0,0,out _), "Mage Power admitted");
        Drain(mq);
        check(mage.TryQueueSpellTrapSet(0,out _), "Mage Power counts additional set spell");
        Drain(mq);
        check(mage.AttackValue(0,0) == 2000 && mage.DefenseValue(0,0) == 2000,
            "Mage Power counts itself and set cards");
        // A Field Spell (Sogen) is in the dedicated slot, not an S/T slot.
        check(mage.TryQueueSpellTrapSet(0,out _), "Mage Power field set admitted");
        Drain(mq);
        check(mage.FieldSpells[0] is not null && mage.FieldSpellFaceDown[0] &&
            mage.AttackValue(0,0) == 2500, "Mage Power counts set FIELD too");

        var stale = DemoDuel.FromPartialObservation(0,[Spell(1449)],
            [Monster(42),null,null,null,null],new Card?[5],8000,equipmentContextResolved:true);
        var sq = stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueEquipment(DuelZone.Hand,0,0,0,out _), "Stale equipment initially admitted");
        stale.Monsters[0,0] = Monster(42); // fixture replaces occurrence with same resource/index
        Drain(sq);
        check(stale.EquipmentCards.Count == 0 && stale.Hand.Count == 1 &&
            sq.Trace.Last().Result?.Outcome == CommandOutcome.Skipped, "Stale same-ID target cannot acquire link");
        check(!EquipmentRules.Supports(1449) && EquipmentRules.SupportsQueued(1449),
            "New dynamic equipment cannot bypass queue via legacy activation");
        check(DynamicEquipmentRules.Evaluate(656, 1001, 9000,8000,0,0).BaseAttack == 501,
            "Megamorph native half rounding");
    }
}
