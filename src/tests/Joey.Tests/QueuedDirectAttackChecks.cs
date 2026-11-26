using Joey.Core;
using Joey.Lab;
using System.Text.Json;

internal static class QueuedDirectAttackChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>Release;
    }
    private static Card Monster(int attack,ushort id=24)=>new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(attack/10)<<9));
    private static Card?[] Slots(Card? c=null)=>[null,null,c,null,null];
    private static void CheckQueuedAttackResponses(Action<bool, string> check)
    {
        var attacker = Monster(1400);
        foreach (ushort id in new[] { AttackTrapRules.MagicCylinder, AttackTrapRules.NegateAttack })
        foreach (int responder in new[] { 0, 1 })
        foreach (bool pass in new[] { false, true })
        {
            var trap = new Card(id, id, "Attack Trap", "", 21u << 20);
            bool endsBattle = !pass && id == AttackTrapRules.NegateAttack;
            int side = 1 - responder;
            var duel = DemoDuel.FromPartialObservation(responder, [trap, trap],
                side == 0 ? Slots(attacker) : Slots(), side == 1 ? Slots(attacker) : Slots(), 8000);
            check(duel.SetFromHand(0, out _) && duel.SetFromHand(0, out _) &&
                duel.EndTurn(out _) && duel.BeginBattle(out _), "Queued Trap fixture");
            var gate = new Gate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("attack 2 direct").Enqueued &&
                duel.DescribeActionPresentation(queue.Trace.Last().Entry.Command) is null,
                "Queued response suppresses the attack cue until a pass");
            queue.Tick();
            check(queue.Active?.Command is DeclareDirectAttackCommand &&
                duel.PendingAttackResponse is null && duel.Life.SequenceEqual([8000, 8000]),
                "Declaration gate precedes the response window");

            gate.Release = true;
            Drain(queue);
            check(console.HasPendingChoice && duel.PendingAttackResponse is { } choice &&
                duel.UsesQueuedAttackResponse(choice) && choice.EligibleTraps.Count == 2 &&
                console.Execute("choices").Message.Contains(id.ToString()) &&
                !console.Execute("battle end").Success && !duel.PassAttackResponse(out _) &&
                !duel.ActivateAttackTrap(responder, 0, out _),
                "Queued response owns the only pass or activation path");

            if (!pass)
            {
                check(console.Execute($"respond-attack {responder} 0").Enqueued,
                    "Trap selects the original trap before a fixture-only occurrence swap");
                SwapTraps(duel, responder);
                Drain(queue);
                check(queue.Trace.Last().Result?.Outcome == CommandOutcome.Rejected &&
                    console.HasPendingChoice && duel.Graveyard(responder).Count == 0 &&
                    duel.Life.SequenceEqual([8000, 8000]) &&
                    !console.Execute($"respond-attack {responder} 0").Enqueued,
                    "Trap rejects swapped copies at execution and at admission without closing the window");
                SwapTraps(duel, responder);
            }

            var answer = pass ? console.Execute("respond-attack pass").Enqueued :
                new CpuDuelController(responder).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Response };
            var admitted = queue.Trace.Last().Entry;
            var preview = duel.DescribeActionPresentation(admitted.Command);
            check(answer && admitted.Origin == (pass ? ActionOrigin.Console : ActionOrigin.Cpu) &&
                preview?.Kind == (pass ? ActionPresentationKind.DirectAttack : ActionPresentationKind.Activate) &&
                duel.Life.SequenceEqual([8000, 8000]) && duel.Spells[responder, 0] == trap,
                "Queue admits the selected response read-only and owns its presentation");

            gate.Release = false;
            queue.Tick();
            check(duel.PendingAttackResponse is null && duel.Life.SequenceEqual([8000, 8000]) &&
                duel.Spells[responder, 0] == trap,
                "Response presentation gate precedes spend and damage");
            gate.Release = true;
            queue.Tick();
            if (!pass)
            {
                gate.Release = false;
                queue.Tick();
                check(queue.Active?.Command is SendToGraveyardCommand &&
                    duel.Graveyard(responder).SequenceEqual([trap]) &&
                    duel.Phase == DemoPhase.Battle && duel.Life.SequenceEqual([8000, 8000]),
                    "Trap spends its exact occurrence before applying the response effect");
                gate.Release = true;
            }
            Drain(queue);
            int expectedDamage = endsBattle ? 0 : 1400;
            check(duel.Life[pass ? responder : side] == 8000 - expectedDamage &&
                duel.Life[pass ? side : responder] == 8000 &&
                duel.Phase == (endsBattle ? DemoPhase.Main : DemoPhase.Battle) &&
                duel.Monsters[side, 2] == attacker && duel.Spells[responder, 1] == trap &&
                (pass ? duel.LastAttackTrap is null && duel.Spells[responder, 0] == trap
                    : duel.LastAttackTrap is { } result && result.TrapResourceId == id &&
                        result.ReflectedDamage == expectedDamage && result.BattlePhaseEnded == endsBattle &&
                        duel.Spells[responder, 0] is null) &&
                queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Completed &&
                    entry.Entry.Command is ChangeBattlePhaseCommand { EnterBattle: false } &&
                    entry.Result?.Outcome == CommandOutcome.Applied) == (endsBattle ? 1 : 0) &&
                !duel.TryQueueDirectAttack(2, out _) && !duel.TryQueueBattlePhaseChange(true, out _) &&
                !console.HasPendingChoice,
                "Pass resumes the same attack, Cylinder reflects once and Negate Attack ends battle without damage");
        }
    }

    private static void CheckDirectMirrorForce(Action<bool, string> check)
    {
        var mirror = new Card(1056, AttackTrapRules.MirrorForce, "Mirror Force", "", 21u << 20);
        var attacker = Monster(1400);
        var ally = Monster(1000, 1351);
        var defender = Monster(800);
        var hidden = Monster(600, 1351);
        foreach (int side in new[] { 0, 1 })
        {
            int responder = 1 - side;
            Card?[] field = [ally, defender, attacker, hidden, null];
            var duel = DemoDuel.FromPartialObservation(responder, [mirror],
                side == 0 ? field : Slots(), side == 1 ? field : Slots(), 8000);
            duel.DefensePosition[side, 1] = duel.DefensePosition[side, 3] = true;
            duel.MonsterFaceDown[side, 3] = true;
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.BeginBattle(out _),
                "Queued Mirror Force fixture");
            var gate = new Gate { Release = true };
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;

            check(console.Execute("attack 2 direct").Enqueued, "Direct response accepts other supported normal allies");
            Drain(queue);
            check(duel.PendingAttackResponse?.EligibleTraps.Single().Trap.Id == mirror.Id &&
                console.Execute($"respond-attack {responder} 0").Enqueued,
                "Mirror Force response selects the Set occurrence before damage");
            gate.Release = false;
            queue.Tick();
            check(duel.Spells[responder, 0] == mirror && duel.Graveyard(side).Count == 0,
                "Mirror Force activation gate precedes all removal");

            gate.Release = true;
            queue.Tick();
            gate.Release = false;
            queue.Tick();
            check(queue.Active?.Command is SendToGraveyardCommand &&
                duel.Graveyard(responder).SequenceEqual([mirror]) && duel.Graveyard(side).Count == 0,
                "Mirror Force spends before destruction");
            gate.Release = true;
            queue.Tick();
            gate.Release = false;
            queue.Tick();
            check(queue.Active?.Command is DestroyCommand && duel.Graveyard(side).SequenceEqual([ally]) &&
                duel.Monsters[side, 2] == attacker && duel.LastAttackTrap is null,
                "First destruction gate blocks the second target and the final result");
            gate.Release = true;
            Drain(queue);
            check(duel.Life.SequenceEqual([8000, 8000]) && duel.Phase == DemoPhase.Battle &&
                duel.Graveyard(side).SequenceEqual([ally, attacker]) &&
                duel.Monsters[side, 1] == defender && duel.Monsters[side, 3] == hidden &&
                duel.MonsterFaceDown[side, 3] &&
                duel.LastMonsterWipe is { TotalDestroyed: 2 } &&
                duel.LastAttackTrap is { TrapResourceId: AttackTrapRules.MirrorForce,
                    DestroyedMonsters: 2, AttackContinued: false, ReflectedDamage: 0, BattlePhaseEnded: false },
                "Mirror Force removes only face-up attack monsters and keeps the Battle Phase");
        }
    }

    private static void SwapTraps(DemoDuel duel, int side) =>
        (duel.Spells[side, 0], duel.Spells[side, 1]) = (duel.Spells[side, 1], duel.Spells[side, 0]);

    private static void Drain(ActionQueue queue)
    {
        for (int i = 0; i < 40 && queue.IsBusy; i++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Attack response queue did not drain.");
    }

    private static void CheckSetBackRow(Action<bool, string> check)
    {
        var setSpell = new Card(1010, 1010, "Pot of Greed", "", 22u << 20);
        var setTrap = new Card(1002, 1002, "Trap Hole", "", 21u << 20);
        var attackingSideTrap = new Card(AttackTrapRules.MagicCylinder,
            AttackTrapRules.MagicCylinder, "Magic Cylinder", "", 21u << 20);
        foreach (int side in new[] { 0, 1 })
        {
            int responder = 1 - side;
            var attacker = Monster(1400);
            var duel = DemoDuel.FromPartialObservation(responder, [setSpell, setTrap],
                side == 0 ? Slots(attacker) : Slots(), side == 1 ? Slots(attacker) : Slots(), 8000,
                side0Deck: side == 0 ? [attackingSideTrap] : [],
                side1Deck: side == 1 ? [attackingSideTrap] : []);
            check(duel.SetFromHand(0, out _) && duel.SetFromHand(0, out _) && duel.EndTurn(out _) &&
                duel.Draw(out _) && duel.SetFromHand(0, out _) && duel.BeginBattle(out _), "Both sides set back-row cards");
            var queue = duel.CreatePrimitiveActionQueue();
            duel.SpellFaceDown[responder, 0] = false;
            check(!duel.TryQueueDirectAttack(2, out _) && queue.Trace.Count == 0,
                "Face-up Spell/Trap still falls outside the queued battle pilot");
            duel.SpellFaceDown[responder, 0] = true;
            check(duel.TryQueueDirectAttack(2, out _), "Direct attack admits both sides' Set cards");
            Drain(queue);
            check(duel.Life[responder] == 6600 && duel.PendingAttackResponse is null &&
                duel.Spells[side, 0] == attackingSideTrap && duel.SpellFaceDown[side, 0] &&
                duel.Spells[responder, 0] == setSpell && duel.SpellFaceDown[responder, 0] &&
                duel.Spells[responder, 1] == setTrap && duel.SpellFaceDown[responder, 1],
                "Set cards keep their identity and only eligible opposing Traps can respond");
        }
    }

    private static void CheckFieldSpellDamage(Action<bool, string> check)
    {
        var yami = new Card(334, 334, "Yami", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        {
            int responder = 1 - side;
            var attacker = Monster(1400);
            var duel = DemoDuel.FromPartialObservation(responder, [yami],
                side == 0 ? Slots(attacker) : Slots(), side == 1 ? Slots(attacker) : Slots(), 8000);
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(console.Execute("activate hand 0").Enqueued, "Active field fixture admits Yami");
            Drain(queue);
            check(duel.TryCaptureCardTarget(responder, DuelZone.FieldSpell, 0, out var fieldTarget),
                "Yami has a tracked occurrence in the Field Zone");
            check(duel.EndTurn(out _) && duel.BeginBattle(out _) && duel.AttackValue(side, 2) == 1600,
                "Opposing Yami boosts the Fiend before the direct attack");
            check(duel.TryQueueDirectAttack(2, out _) && duel.Life[responder] == 8000,
                "Field Spell does not prevent read-only attack admission");
            Drain(queue);
            check(duel.Life[responder] == 6400 && duel.ActiveFieldSpell == yami &&
                duel.TryCaptureCardTarget(responder, DuelZone.FieldSpell, 0, out var currentField) &&
                currentField == fieldTarget && duel.PendingAttackResponse is null,
                "Direct attack uses live field-modified ATK and preserves the Field Spell");
        }
    }

    private static void CheckEquippedDirectAttack(Action<bool, string> check)
    {
        var magePower = new Card(DynamicEquipmentRules.MagePower, DynamicEquipmentRules.MagePower,
            "Mage Power", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (ushort response in new ushort[] { 0, AttackTrapRules.MagicCylinder, AttackTrapRules.MirrorForce })
        {
            int responder = 1 - side;
            bool pass = response == 0;
            bool wipe = response == AttackTrapRules.MirrorForce;
            ushort trapId = pass ? AttackTrapRules.MagicCylinder : response;
            var trap = new Card(trapId, trapId, "Attack Trap", "", 21u << 20);
            var attacker = Monster(1400);
            var duel = DemoDuel.FromPartialObservation(responder, [magePower, trap],
                side == 0 ? Slots(attacker) : Slots(), side == 1 ? Slots(attacker) : Slots(), 8000,
                equipmentContextResolved: true);
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(duel.TryQueueEquipment(DuelZone.Hand, 0, side, 2, out _),
                "Mage Power can equip the opposing monster");
            Drain(queue);
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.BeginBattle(out _) &&
                duel.AttackValue(side, 2) == 2400 && duel.TryQueueDirectAttack(2, out _),
                "Equipped direct attack admits the current ATK and the Set response");
            Drain(queue);
            check(duel.PendingAttackResponse?.EligibleTraps.Single().Zone == 1 &&
                console.Execute(pass ? "respond-attack pass" : $"respond-attack {responder} 1").Enqueued,
                "The linked Equip does not appear as an attack response");
            Drain(queue);

            check(duel.Life[side] == (response == AttackTrapRules.MagicCylinder ? 5600 : 8000) &&
                duel.Life[responder] == (pass ? 5600 : 8000) &&
                (wipe ? duel.Graveyard(side).SequenceEqual([attacker]) &&
                    duel.Graveyard(responder).SequenceEqual([trap, magePower]) &&
                    duel.EquipmentCards.Count == 0 && duel.Spells[responder, 0] is null :
                    duel.Monsters[side, 2] == attacker && duel.Spells[responder, 0] == magePower &&
                    duel.EquipmentCards.Count == 1),
                "Direct damage and Cylinder use equipped ATK; Mirror Force cleans up the original link");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        CheckQueuedAttackResponses(check);
        CheckDirectMirrorForce(check);
        CheckSetBackRow(check);
        CheckFieldSpellDamage(check);
        CheckEquippedDirectAttack(check);
        var d=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1400)),8000);
        var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
        check(!d.TryQueueDirectAttack(2,out _),"A05.7 Main Phase rejects attack");
        check(d.BeginBattle(out _) && d.TryQueueDirectAttack(2,out _),"A05.7 Battle attack admitted");
        check(d.Life[0]==8000,"A05.7 admission read only");
        q.Tick();q.Tick();
        check(d.Life[0]==8000 && q.Active?.Command is DeclareDirectAttackCommand && q.PendingCount==1,
            "A05.7 declaration waits at gate before damage");
        gate.Release=true;
        q.Tick();gate.Release=false;q.Tick();
        check(d.Life[0]==6600 && q.Active?.Command is DamageCommand,"A05.7 one separate damage mutation");
        q.Tick();check(d.Life[0]==6600,"A05.7 no repeated damage while stalled");
        gate.Release=true;q.Tick();
        var rows=ActionTraceProjection.ReadExecuted(q.Trace);
        check(!q.IsBusy && rows.Count==2 && rows[0].Args!.SequenceEqual(new ushort[]{0x8034,2,1,0}) &&
            rows[1].Args!.SequenceEqual(new ushort[]{0x43,1400,0,1}) &&
            !d.TryQueueDirectAttack(2,out _),"A05.7 declaration/damage and once-per-turn");
        foreach(var enemy in new[]{false,true})
        {
            var blocked=DemoDuel.FromPartialObservation(1,[],Slots(enemy?Monster(1000):null),
                Slots(Monster(1000,219)),8000);
            var bq=blocked.CreatePrimitiveActionQueue();blocked.BeginBattle(out _);
            check(!blocked.TryQueueDirectAttack(2,out _) && bq.Trace.Count==0,"A05.7 rejects effects/other monsters");
        }
        var stale=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1000)),8000);
        var sq=stale.CreatePrimitiveActionQueue();stale.BeginBattle(out _);
        check(stale.TryQueueDirectAttack(2,out _),"A05.7 stale admitted");
        stale.Monsters[1,2]=null; // fixture-only invalidation
        sq.Tick();
        check(stale.Life[0]==8000 && sq.Trace.Last().Result?.Outcome==CommandOutcome.Skipped &&
            ActionTraceProjection.ReadExecuted(sq.Trace).Single().Mapping=="non_applied","A05.7 stale attack not projected");
        var zero=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(0)),8000);
        var zq=zero.CreatePrimitiveActionQueue();zero.BeginBattle(out _);
        check(zero.TryQueueDirectAttack(2,out _),"A05.7 zero ATK declaration");
        zq.Tick();
        check(!zq.IsBusy && zero.Life[0]==8000 && ActionTraceProjection.ReadExecuted(zq.Trace).Count==1,
            "A05.7 zero damage emits no LP command");
        var lethal=DemoDuel.FromPartialObservation(1,[],Slots(),Slots(Monster(1400)),8000);
        lethal.Life[0]=1000;var lq=lethal.CreatePrimitiveActionQueue();lethal.BeginBattle(out _);
        lethal.TryQueueDirectAttack(2,out _);lq.Tick();lq.Tick();
        check(lethal.Winner==1 && lethal.Life[0]==0 && !lq.IsBusy,"A05.7 lethal attack completes");
        using var output=new StringWriter();using var error=new StringWriter();
        check(ActionTraceExport.RunBattlePilot([],output,error)==0,"A05.7 battle pilot CLI");
        using var json=JsonDocument.Parse(output.ToString());
        var mapped=json.RootElement.GetProperty("rows").EnumerateArray().Where(x=>x.GetProperty("mapping").GetString()=="mapped")
            .Select(x=>x.GetProperty("args")[0].GetInt32()).ToArray();
        check(mapped.SequenceEqual([0x8061,0x80CE,0x8034,0x43]),"A05.7 draw summon attack damage order");
    }
}
