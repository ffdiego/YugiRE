using Joey.Core;

internal static class QueuedTemporaryStatChecks
{
    private static Card Monster(ushort id = 24) =>
        new(id, id, "Monster", "", (4u << 25) | (3u << 20) | (100u << 9) | 40u);

    private sealed class SpendGate : IPresentationGate
    {
        public bool Released { get; set; } = true;
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not SendToGraveyardCommand || Released;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 100 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Temporary modifier queue did not drain.");
    }

    private static DemoDuel WithMonster(int side, params Card[] hand) =>
        DemoDuel.FromPartialObservation(side, hand, [Monster(), null, null, null, null],
            [Monster(), null, null, null, null], 8000);

    private static CardTarget Capture(DemoDuel duel, int side, DuelZone zone, int index) =>
        duel.TryCaptureCardTarget(side, zone, index, out var target)
            ? target : throw new InvalidOperationException("Missing fixture occurrence.");

    public static void Run(Action<bool, string> check, string? datPath)
    {
        using var archive = datPath is null ? null : new Archive(datPath);
        var catalog = archive is null ? null : Catalog.Load(archive);
        var modifiers = new (ushort Id, int ClassCode, int Attack, int Defense)[]
        {
            (1015, 21, 500, 0), (1016, 21, 0, 500), (1075, 22, 700, 0),
            (1076, 22, 0, 700), (1082, 21, 0, -500)
        };

        foreach (var (id, classCode, attack, defense) in modifiers)
        foreach (int side in new[] { 0, 1 })
        {
            var spellTrap = catalog?.First(c => c.Id == id) ?? new Card(id, id, "Modifier", "", (uint)classCode << 20);
            check(spellTrap.ClassCode == classCode, "P123 DAT card class");
            int enemy = 1 - side;
            var duel = WithMonster(side, spellTrap);
            var gate = new SpendGate();
            var queue = duel.CreatePrimitiveActionQueue(gate);
            var target = Capture(duel, enemy, DuelZone.Monster, 0);
            var hand = Capture(duel, side, DuelZone.Hand, 0);
            duel.DefensePosition[enemy, 0] = side == 1;
            duel.MonsterFaceDown[enemy, 0] = true;
            check(!duel.TryQueueTemporaryStatModifier(hand, target, out _), "P123 requires a face-up target");
            duel.MonsterFaceDown[enemy, 0] = false;
            if (classCode == 21)
                check(!duel.TryQueueTemporaryStatModifier(hand, target, out _), "P123 cannot activate Trap from hand");
            else
            {
                check(duel.TryQueueTemporaryStatModifier(hand, target, out _) &&
                    duel.AttackValue(enemy, 0) == 1000 && duel.DefenseValue(enemy, 0) == 400,
                    "P123 Quick-Play hand admission doesn't mutate");
                gate.Released = false;
                for (int i = 0; i < 10; i++) queue.Tick();
                check(queue.IsBusy && duel.Graveyard(side).Single().Id == id &&
                    duel.AttackValue(enemy, 0) == 1000 && duel.DefenseValue(enemy, 0) == 400,
                    "P123 source spend gate precedes modifier");
                gate.Released = true;
                Drain(queue);
                check(duel.AttackValue(enemy, 0) == 1000 + attack &&
                    duel.DefenseValue(enemy, 0) == Math.Max(0, 400 + defense), "P123 Quick-Play applies exact delta");
            }

            var setDuel = WithMonster(side, spellTrap);
            var setQueue = setDuel.CreatePrimitiveActionQueue();
            var own = Capture(setDuel, side, DuelZone.Monster, 0);
            check(setDuel.TryQueueSpellTrapSet(0, out _), "P123 Set fixture admitted");
            Drain(setQueue);
            var set = Capture(setDuel, side, DuelZone.SpellTrap, 0);
            check(!setDuel.TryQueueTemporaryStatModifier(set, own, out _) &&
                setDuel.TryQueueEndTurn(out _), "P123 Set needs a later turn");
            Drain(setQueue);
            check(setDuel.ActivePlayer == enemy && setDuel.TryQueueBattlePhaseChange(true, out _),
                "P123 enters opposing Battle Phase");
            Drain(setQueue);
            check(setDuel.TryQueueTemporaryStatModifier(set, own, out _),
                "P123 opponent's prior Set may activate on an idle Battle Phase");
            Drain(setQueue);
            check(setDuel.Graveyard(side).Single().Id == id &&
                setDuel.AttackValue(side, 0) == 1000 + attack &&
                setDuel.DefenseValue(side, 0) == Math.Max(0, 400 + defense),
                "P123 Set resolves the same table entry");
            check(setDuel.TryQueueEndTurn(out _), "P123 ends current turn");
            Drain(setQueue);
            check(setDuel.AttackValue(side, 0) == 1000 && setDuel.DefenseValue(side, 0) == 400,
                "P123 expires when the turn advances");
        }

        Card rush = new(1075, 1075, "Modifier", "", 22u << 20);
        var stacked = WithMonster(0, rush, rush);
        var console = new DuelConsoleSession(stacked);
        check(console.Execute("activate hand 0 0 monster 0").Enqueued, "P123 console producer");
        Drain(console.Queue);
        check(console.Execute("activate hand 0 0 monster 0").Enqueued, "P123 repeated copies");
        Drain(console.Queue);
        check(stacked.AttackValue(0, 0) == 2400, "P123 stat modifiers stack");
        var prior = Capture(stacked, 0, DuelZone.Monster, 0);
        console.Queue.Enqueue(new ReturnToHandCommand(0, prior));
        Drain(console.Queue);
        check(stacked.TryQueueNormalPlacement(0, 0, false, out _), "P123 target returns to field same turn");
        Drain(console.Queue);
        check(Capture(stacked, 0, DuelZone.Monster, 0) == prior && stacked.AttackValue(0, 0) == 1000,
            "P123 a new field stay drops previous buffs even on the same occurrence");

        var combat = WithMonster(1, rush);
        var combatQueue = combat.CreatePrimitiveActionQueue();
        check(combat.TryQueueBattlePhaseChange(true, out _), "P123 combat fixture enters Battle");
        Drain(combatQueue);
        check(combat.TryQueueTemporaryStatModifier(Capture(combat, 1, DuelZone.Hand, 0),
            Capture(combat, 1, DuelZone.Monster, 0), out _), "P123 Quick-Play before attack");
        Drain(combatQueue);
        check(combat.TryQueueTargetedAttack(0, 0, out _), "P123 attack admitted after modifier");
        Drain(combatQueue);
        check(combat.Monsters[1, 0] is not null && combat.Monsters[0, 0] is null && combat.Life[0] == 7300,
            "P123 queued combat consumes the modified stat.");

        var dragon = Monster() with { Properties = (Monster().Properties & ~(31u << 20)) | (1u << 20) };
        var protectedDragon = DemoDuel.FromPartialObservation(0, [rush], [dragon, null, null, null, null],
            [Monster(740), null, null, null, null], 8000);
        protectedDragon.CreatePrimitiveActionQueue();
        check(!protectedDragon.TryQueueTemporaryStatModifier(Capture(protectedDragon, 0, DuelZone.Hand, 0),
            Capture(protectedDragon, 0, DuelZone.Monster, 0), out _), "P123 Lord of D. guards Dragons from targets");

        var umi = new Card(333, 333, "Field", "", 22u << 20);
        var fisherman = DemoDuel.FromPartialObservation(0, [umi, rush], [Monster(1326), null, null, null, null],
            new Card?[5], 8000);
        check(fisherman.Play(0, out _), "P123 Umi fixture activates");
        fisherman.CreatePrimitiveActionQueue();
        check(!fisherman.TryQueueTemporaryStatModifier(Capture(fisherman, 0, DuelZone.Hand, 0),
            Capture(fisherman, 0, DuelZone.Monster, 0), out _), "P123 Umi protects The Legendary Fisherman from Spell targets");

        var stale = WithMonster(0, rush);
        var staleQueue = stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueTemporaryStatModifier(Capture(stale, 0, DuelZone.Hand, 0),
            Capture(stale, 1, DuelZone.Monster, 0), out _), "P123 reserves tracked exact target");
        stale.Monsters[1, 0] = Monster();
        Drain(staleQueue);
        check(stale.Hand.Count == 1 && stale.AttackValue(1, 0) == 1000,
            "P123 stale target before execution cannot consume source or modify same-ID replacement");
    }
}
