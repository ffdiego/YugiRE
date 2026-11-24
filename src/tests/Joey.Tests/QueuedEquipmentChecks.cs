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
