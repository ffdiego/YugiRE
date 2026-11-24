using Joey.Core;

internal static class TimedEquipmentAndQueuedLpChecks
{
    private static Card Spell(ushort id) => new(id, id, "LP spell", "", 22u << 20);

    private static void Drain(ActionQueue q)
    {
        for (int i = 0; i < 100 && q.IsBusy; i++) q.Tick();
        if (q.IsBusy) throw new InvalidOperationException("Queue did not drain.");
    }

    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; } = true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    public static void Run(Action<bool, string> check)
    {
        for (int count = 0; count < 16; count++)
        {
            ushort flags = (ushort)(0xffc3 | (count << 2));
            var stim = TimedEquipmentRules.Contribution(1060, 15, count);
            var germ = TimedEquipmentRules.Contribution(1046, 15, count);
            var machine = TimedEquipmentRules.Contribution(1046, 7, count);
            check(TimedEquipmentRules.DecodeCounter(flags) == count &&
                stim.IsSupported && stim.Value.Attack == 700 - 200 * count && stim.Value.Defense == 0 &&
                germ.IsSupported && germ.Value.Attack == -300 * count && germ.Value.Defense == 0 &&
                machine.IsSupported && machine.Value.Attack == 0 && machine.Value.Defense == 0,
                $"Timed equipment counter {count}: decode and additive deltas");
            check(TimedEquipmentRules.SaturatingAdd(count,1) == Math.Min(15,count+1),
                $"Four-bit counter arithmetic {count}");
        }
        check(!TimedEquipmentRules.Contribution(1060,15,16).IsSupported &&
            !TimedEquipmentRules.Contribution(1046,15,-1).IsSupported &&
            !TimedEquipmentRules.Contribution(300,15,0).IsSupported &&
            !EquipmentRules.SupportsQueued(1060) && !EquipmentRules.SupportsQueued(1046),
            "Unresolved turn timing does not expose timed cards as playable");

        foreach (var (id, own, enemy) in new (ushort,int,int)[] {
            (342,0,-200),(343,0,-500),(344,0,-600),(345,0,-800),(1007,0,-300),
            (338,500,0),(339,600,0),(340,800,0),(341,1000,0),(1315,1000,1000)})
        foreach (int side in new[] {0,1})
        foreach (bool set in new[] {false,true})
        {
            var duel = DemoDuel.FromPartialObservation(side,[Spell(id)],new Card?[5],new Card?[5],8000);
            var gate = new Gate();
            var q = duel.CreatePrimitiveActionQueue(gate);
            if (set)
            {
                check(duel.TryQueueSpellTrapSet(0,out _), $"LP {id}: set admitted");
                Drain(q);
            }
            int before = q.Trace.Count;
            gate.Release = false;
            check(duel.TryQueueLifePointSpell(set ? DuelZone.SpellTrap : DuelZone.Hand,0,out _) &&
                duel.Life.SequenceEqual([8000,8000]) && duel.Graveyard(side).Count==0,
                $"LP {id}: admission is read-only");
            q.Tick(); q.Tick();
            check(duel.Graveyard(side).Count==0 && duel.Life.SequenceEqual([8000,8000]),
                $"LP {id}: activation producer waits before spending");
            gate.Release = true; Drain(q);
            var executed = q.Trace.Skip(before).Where(x=>x.Stage==ActionTraceStage.Executed).ToArray();
            var expectedLifeCommands = new List<DuelCommand>(2);
            if (own > 0) expectedLifeCommands.Add(new HealCommand(side, own));
            if (enemy > 0) expectedLifeCommands.Add(new HealCommand(1 - side, enemy));
            if (enemy < 0) expectedLifeCommands.Add(new DamageCommand(1 - side, -enemy));
            check(duel.Life[side]==8000+own && duel.Life[1-side]==8000+enemy &&
                duel.Hand.Count==0 && duel.Spells[side,0] is null &&
                duel.Graveyard(side).Single().Id==id &&
                executed.Length == 2 + expectedLifeCommands.Count &&
                executed[1].Entry.Command is SendToGraveyardCommand &&
                executed.Skip(2).Select(x => x.Entry.Command).SequenceEqual(expectedLifeCommands) &&
                executed.All(x=>x.Result?.Outcome==CommandOutcome.Applied),
                $"LP {id}: ordered separate spend and LP commands, both source zones and sides");
            check(!duel.TryQueueLifePointSpell(set ? DuelZone.SpellTrap : DuelZone.Hand,0,out _),
                $"LP {id}: spent occurrence not reusable");
        }
        var stale = DemoDuel.FromPartialObservation(0,[Spell(342)],new Card?[5],new Card?[5],8000);
        var sq=stale.CreatePrimitiveActionQueue();
        stale.TryQueueSpellTrapSet(0,out _); Drain(sq);
        check(stale.TryQueueLifePointSpell(DuelZone.SpellTrap,0,out _),"LP stale source initially admitted");
        stale.Spells[0,0]=Spell(342);
        Drain(sq);
        check(stale.Life.SequenceEqual([8000,8000]) && stale.Graveyard(0).Count==0 &&
            sq.Trace.Last().Result?.Outcome==CommandOutcome.Skipped,
            "LP stale same-ID replacement does not resolve");
        var lethal=DemoDuel.FromPartialObservation(0,[Spell(345)],new Card?[5],new Card?[5],8000);
        lethal.Life[1]=100;
        var lq=lethal.CreatePrimitiveActionQueue();
        check(lethal.TryQueueLifePointSpell(DuelZone.Hand,0,out _),"Lethal LP admitted");
        Drain(lq);
        check(lethal.Winner==0 && lethal.Life[1]==0 && lethal.Graveyard(0).Single().Id==345,
            "Lethal LP spell spends before duel end");
    }
}
