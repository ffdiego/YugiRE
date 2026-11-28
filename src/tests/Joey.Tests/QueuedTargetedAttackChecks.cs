using Joey.Core;

internal static class QueuedTargetedAttackChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult r)=>Release;
    }
    private static Card Monster(ushort id,int atk,int def)=>new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card c,int slot) {var s=new Card?[5];s[slot]=c;return s;}
    private static void CheckResponseBeforeHiddenDefender(Action<bool, string> check)
    {
        ushort[] choices = [0, AttackTrapRules.MagicCylinder, AttackTrapRules.NegateAttack, AttackTrapRules.MirrorForce];
        foreach (int side in new[] { 0, 1 })
        foreach (ushort answer in choices)
        {
            bool pass = answer == 0;
            int responder = 1 - side;
            ushort id = pass ? AttackTrapRules.MagicCylinder : answer;
            var trap = new Card(id, id, "Attack response", "", 21u << 20);
            var setSpell = new Card(1010, 1010, "Pot of Greed", "", 22u << 20);
            var attacker = Monster(24, 1400, 1000);
            var defender = Monster(1351, 1000, 1000);
            var duel = DemoDuel.FromPartialObservation(responder, [trap, setSpell],
                side == 0 ? Slots(attacker, 2) : Slots(defender, 3),
                side == 1 ? Slots(attacker, 2) : Slots(defender, 3), 8000);
            duel.DefensePosition[responder, 3] = duel.MonsterFaceDown[responder, 3] = true;
            check(duel.SetFromHand(0, out _) && duel.SetFromHand(0, out _) &&
                duel.EndTurn(out _) && duel.BeginBattle(out _),
                "Targeted attack response fixture");
            var gate = new Gate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;

            check(duel.TryQueueTargetedAttack(2, 3, out _) &&
                duel.DescribeActionPresentation(queue.Trace.Last().Entry.Command) is null,
                "Targeted attack defers the sword cue while a response is eligible");
            queue.Tick();
            check(queue.Active?.Command is DeclareTargetedAttackCommand &&
                duel.PendingAttackResponse is null && duel.MonsterFaceDown[responder, 3],
                "The targeted response waits at the declaration gate");
            gate.Release = true;
            DrainResponseQueue(queue);
            check(duel.PendingAttackResponse is { } pending && !pending.Attack.DirectAttack &&
                pending.Attack.TargetZone == 3 && pending.EligibleTraps.Count == 1 &&
                duel.MonsterFaceDown[responder, 3] &&
                duel.Life.SequenceEqual([8000, 8000]) &&
                console.Execute(pass ? "respond-attack pass" : $"respond-attack {responder} 0").Enqueued,
                "Responding to a targeted attack leaves the original defender hidden");
            var preview = duel.DescribeActionPresentation(queue.Trace.Last().Entry.Command);
            check(pass
                    ? preview is { Kind: ActionPresentationKind.TargetedAttack, DestinationSlot: 3,
                        DeclarationOrigin: ActionOrigin.Player }
                    : preview is { Kind: ActionPresentationKind.Activate },
                "A pass uses the declared target and original input origin for the sword cue");

            gate.Release = false;
            queue.Tick();
            check(duel.PendingAttackResponse is null && duel.MonsterFaceDown[responder, 3] &&
                duel.Spells[responder, 0] == trap && duel.Life.SequenceEqual([8000, 8000]),
                "Response presentation precedes reveal, spend and damage");
            gate.Release = true;
            DrainResponseQueue(queue);

            var calculations = ActionTraceProjection.ReadExecuted(queue.Trace)
                .Where(row => row.BattleCalculation is not null).ToArray();
            check(pass
                ? calculations.Length == 1 && calculations[0].Command == "ResolveRevealedBattleCommand" &&
                  calculations[0].BattleCalculation is { Attacker: { Slot: 2, Attack: 1400 },
                      Defender: { Slot: 3, FaceDown: false, Defense: true, DefenseValue: 1000 }, Difference: 400 }
                : calculations.Length == 0, "A09 records the actual post-reveal calculation, not cancelled attacks");
            check(!console.HasPendingChoice && duel.Spells[responder, 1] == setSpell &&
                duel.SpellFaceDown[responder, 1] && duel.Life[responder] == 8000 &&
                duel.Life[side] == (answer == AttackTrapRules.MagicCylinder ? 6600 : 8000) &&
                duel.Phase == (answer == AttackTrapRules.NegateAttack ? DemoPhase.Main : DemoPhase.Battle) &&
                (pass ? duel.LastAttackTrap is null && duel.Spells[responder, 0] == trap &&
                    duel.Graveyard(responder).SequenceEqual([defender]) && duel.Monsters[responder, 3] is null
                    : duel.LastAttackTrap?.TrapResourceId == answer &&
                    duel.Graveyard(responder).SequenceEqual([trap]) && duel.Monsters[responder, 3] == defender &&
                    duel.MonsterFaceDown[responder, 3]) &&
                (answer == AttackTrapRules.MirrorForce
                    ? duel.Graveyard(side).SequenceEqual([attacker]) && duel.Monsters[side, 2] is null
                    : duel.Monsters[side, 2] == attacker),
                "Targeted pass reveals and resolves combat, while each Trap stops it before reveal");
        }
    }

    private static void CheckFieldSpellDefense(Action<bool, string> check)
    {
        var chorus = new Card(1069, 1069, "Chorus of Sanctuary", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (bool fieldActive in new[] { false, true })
        {
            int responder = 1 - side;
            var attacker = Monster(24, 1400, 1000);
            var defender = Monster(1351, 1000, 1000);
            var duel = DemoDuel.FromPartialObservation(responder, [chorus],
                side == 0 ? Slots(attacker, 2) : Slots(defender, 3),
                side == 1 ? Slots(attacker, 2) : Slots(defender, 3), 8000);
            duel.DefensePosition[responder, 3] = duel.MonsterFaceDown[responder, 3] = true;
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(console.Execute(fieldActive ? "activate hand 0" : "set 0").Enqueued,
                "Field defense fixture activates or Sets Chorus");
            DrainResponseQueue(queue);
            check(duel.EndTurn(out _) && duel.BeginBattle(out _) &&
                duel.DefenseValue(responder, 3) == 1000 && duel.TryQueueTargetedAttack(2, 3, out _),
                "The hidden defender uses base DEF before reveal, even under an active Field Spell");
            DrainResponseQueue(queue);
            check(duel.FieldSpells[responder] == chorus && duel.FieldSpellFaceDown[responder] != fieldActive &&
                duel.Life[responder] == 8000 && duel.Life[side] == (fieldActive ? 7900 : 8000) &&
                !duel.MonsterFaceDown[responder, 3] &&
                (fieldActive ? duel.Monsters[responder, 3] == defender && duel.DefenseValue(responder, 3) == 1500 :
                    duel.Monsters[responder, 3] is null && duel.Graveyard(responder).Contains(defender)),
                "Field bonus applies after reveal only when active and changes the battle outcome");
        }
    }

    private static void CheckBattleEquipmentCleanup(Action<bool, string> check)
    {
        var magePower = new Card(DynamicEquipmentRules.MagePower, DynamicEquipmentRules.MagePower,
            "Mage Power", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (bool lethal in new[] { false, true })
        {
            int responder = 1 - side;
            var attacker = Monster(24, lethal ? 1800 : 1400, 1000);
            var defender = Monster(1351, 1400, 1000);
            var duel = DemoDuel.FromPartialObservation(responder, [magePower],
                side == 0 ? Slots(attacker, 2) : Slots(defender, 3),
                side == 1 ? Slots(attacker, 2) : Slots(defender, 3), 8000,
                equipmentContextResolved: true, side0Deck: side == 0 ? [magePower] : [],
                side1Deck: side == 1 ? [magePower] : []);
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(duel.TryQueueEquipment(DuelZone.Hand, 0, side, 2, out _), "Responder equips attacker");
            DrainResponseQueue(queue);
            check(duel.EndTurn(out _) && duel.TryQueueTurnDraw(out _, ActionOrigin.Test),
                "Attacker admits the turn draw");
            DrainResponseQueue(queue);
            check(duel.TryQueueEquipment(DuelZone.Hand, 0, responder, 3, out _), "Attacker equips defender");
            DrainResponseQueue(queue);
            duel.Life[responder] = lethal ? 300 : 8000;
            check(duel.BeginBattle(out _) && duel.TryQueueTargetedAttack(2, 3, out _) &&
                duel.EquipmentCards.Count == 2, "Targeted battle admits tracked equipment on both combatants");
            DrainResponseQueue(queue);

            var cleanup = queue.Trace.Where(t => t.Stage == ActionTraceStage.Executed &&
                (t.Entry.Command.GetType().Name == "FinishBattleRemovalCommand" ||
                    t.Entry.Command is DestroyCommand)).ToArray();
            check(cleanup.Select(t => (t.Entry.Command.GetType().Name, t.Entry.Command.Side)).SequenceEqual(lethal
                ? [("FinishBattleRemovalCommand", responder), (nameof(DestroyCommand), side)]
                : [("FinishBattleRemovalCommand", side), (nameof(DestroyCommand), responder),
                    ("FinishBattleRemovalCommand", responder), (nameof(DestroyCommand), side)]) &&
                cleanup.All(t => t.Result?.Outcome == CommandOutcome.Applied),
                "Each Equip destruction executes after its monster arrives in the graveyard");
            check(lethal
                ? duel.Winner == side && duel.Life[responder] == 0 && duel.Monsters[side, 2] == attacker &&
                    duel.Graveyard(responder).SequenceEqual([defender]) &&
                    duel.Graveyard(side).SequenceEqual([magePower]) &&
                    duel.EquipmentCards is [{ Owner: var owner }] && owner == responder
                : duel.Winner is null && duel.Life.SequenceEqual([8000, 8000]) &&
                    duel.Graveyard(side).SequenceEqual([attacker, magePower]) &&
                    duel.Graveyard(responder).SequenceEqual([magePower, defender]) &&
                    duel.EquipmentCards.Count == 0,
                "Lethal and tied battles clear the right links and preserve the owners of both copies");
        }
    }

    private static void DrainResponseQueue(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Targeted attack response did not drain.");
    }

    public static void Run(Action<bool,string> check)
    {
        CheckResponseBeforeHiddenDefender(check);
        CheckFieldSpellDefense(check);
        CheckBattleEquipmentCleanup(check);
        foreach(int side in new[]{0,1})
        foreach(var (atk,other,defense,removeA,removeB,lossA,lossB) in new (int,int,bool,bool,bool,int,int)[]{
            (1400,1000,false,false,true,0,400),(1000,1400,false,true,false,400,0),
            (1000,1000,false,true,true,0,0),(0,0,false,false,false,0,0),
            (1400,1000,true,false,true,0,0),(1000,1000,true,false,false,0,0),
            (1000,1400,true,false,false,400,0)})
        {
            var a=Monster(24,atk,500);var b=Monster(1351,other,other);
            var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                side==1?Slots(a,2):Slots(b,3),8000);
            if(side==0) d.NextTurn(); // turn 2 (side 1), then turn 3 (side 0)
            if(side==0) d.NextTurn();
            d.DefensePosition[1-side,3]=defense;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.BeginBattle(out _) && d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var at) &&
                d.TryCaptureCardTarget(1-side,DuelZone.Monster,3,out var dt),"A05.8 fixture battle");
            // Capture independently for definite assignment and identity comparison.
            d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var originalA);
            d.TryCaptureCardTarget(1-side,DuelZone.Monster,3,out var originalB);
            check(d.TryQueueTargetedAttack(2,3,out _),"A05.8 targeted admission");
            q.Tick();q.Tick();
            check(d.Monsters[side,2]!=null && d.Monsters[1-side,3]!=null &&
                d.Life.SequenceEqual([8000,8000]),"A05.8 declaration gate precedes mutation");
            gate.Release=true;q.Tick();
            var stages=new List<string>();
            for(int i=0;i<30 && q.IsBusy;i++)
            {
                gate.Release=false;q.Tick();
                if(q.Active?.Command is { } snap && snap.GetType().Name=="SnapshotBattleRemovalCommand")
                    check(d.Monsters[snap.Side,snap.Side==side?2:3] is null && d.Graveyard(snap.Side).Count==0 &&
                        d.Life.SequenceEqual([8000,8000]),"A05.8 temporary record before LP and GY");
                gate.Release=true;q.Tick();
            }
            check(!q.IsBusy && (d.Monsters[side,2] is null)==removeA &&
                (d.Monsters[1-side,3] is null)==removeB && d.Life[side]==8000-lossA &&
                d.Life[1-side]==8000-lossB,"A05.8 common battle matrix");
            var battleDamage = q.Trace.Where(t => t.Stage == ActionTraceStage.Executed)
                .Select(t => t.Entry.Command).OfType<DamageCommand>();
            (int Side, int? Slot)[] expectedAnchors = lossA > 0 ? [(side, 2)] :
                lossB > 0 ? [(1 - side, 3)] : [];
            check(battleDamage.Select(damage => (damage.Side, damage.AffectedCombatantSlot)).SequenceEqual(expectedAnchors),
                "V06: ordinary battle keeps the affected combatant's reserved slot");
            if(removeA) check(d.TryCaptureCardTarget(side,DuelZone.Graveyard,0,out var g) &&
                g.Occurrence==originalA.Occurrence,"A05.8 attacker identity preserved");
            if(removeB) check(d.TryCaptureCardTarget(1-side,DuelZone.Graveyard,0,out var g) &&
                g.Occurrence==originalB.Occurrence,"A05.8 defender identity preserved");
            var trace=ActionTraceProjection.ReadExecuted(q.Trace);
            check(trace[0].Args!.SequenceEqual(new ushort[]{(ushort)((side<<15)|0x33),2,3,(ushort)(2-side)}) &&
                trace.Count(x=>x.Command=="SnapshotBattleRemovalCommand")==(removeA?1:0)+(removeB?1:0) &&
                trace.Count(x=>x.Command=="FinishBattleRemovalCommand")==(removeA?1:0)+(removeB?1:0),
                "A05.8 declaration and exactly one snapshot/final per removal");
            if(removeB) check(trace.Last().Args!.SequenceEqual(new ushort[]{(ushort)(((1-side)<<15)|0x7c),3,0,1}),
                "A05.8 defender final routing mapped");
            if(!removeA) check(!d.TryQueueTargetedAttack(2,3,out _),"A05.8 attack consumed");
        }
        foreach(int side in new[]{0,1})
        foreach(var (atk,def,removed,loss) in new (int,int,bool,int)[]{
            (1400,1000,true,0),(1000,1000,false,0),(1000,1400,false,400)})
        {
            var a=Monster(24,atk,500);var b=Monster(1351,2300,def);
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                    side==1?Slots(a,2):Slots(b,3),8000);
                if(side==0){d.NextTurn();d.NextTurn();}
                d.MonsterFaceDown[1-side,3]=d.DefensePosition[1-side,3]=true;
                return d;
            }
            var legacy=State();
            check(legacy.BeginBattle(out _) && legacy.Attack(2,3,out _),"A06 hidden defender legacy comparator");
            var d=State();var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            int positionChanges=0, summons=0;
            d.MonsterPositionChanged+=_=>positionChanges++;
            d.MonsterSpecialSummoned+=_=>summons++;
            d.MonsterNormalSummoned+=_=>summons++;
            check(d.BeginBattle(out _) && d.TryQueueTargetedAttack(2,3,out _),"A06 hidden defender admitted");
            d.TryCaptureCardTarget(1-side,DuelZone.Monster,3,out var original);
            q.Tick();q.Tick();
            check(d.MonsterFaceDown[1-side,3] && !d.MonsterWasFlipped[1-side,3] &&
                d.Life.SequenceEqual([8000,8000]) && d.Graveyard(1-side).Count==0,
                "A06 declaration gate keeps defender hidden");
            gate.Release=true;q.Tick();
            gate.Release=false;q.Tick();q.Tick();
            check(q.Active?.Command.GetType().Name=="RevealBattleDefenderCommand" &&
                !d.MonsterFaceDown[1-side,3] && d.MonsterWasFlipped[1-side,3] &&
                d.DefensePosition[1-side,3] && d.Monsters[1-side,3] is not null &&
                d.Life.SequenceEqual([8000,8000]) && d.Graveyard(1-side).Count==0 &&
                positionChanges==0 && summons==0,
                "A06 reveal gates calculation without changing pose or emitting Flip Summon");
            gate.Release=true;q.Tick();
            gate.Release=false;q.Tick();q.Tick();
            check(q.Active?.Command.GetType().Name=="ResolveRevealedBattleCommand" &&
                d.Monsters[1-side,3] is not null && d.Life.SequenceEqual([8000,8000]),
                "A06 calculation only plans later commands");
            gate.Release=true;
            for(int n=0;n<50 && q.IsBusy;n++)q.Tick();
            check(!q.IsBusy && q.Fault is null && d.Life.SequenceEqual(legacy.Life) &&
                d.Life[side]==8000-loss && d.Life[1-side]==8000 &&
                (d.Monsters[1-side,3] is null)==removed &&
                d.Graveyard(1-side).Select(c=>c.Id).SequenceEqual(legacy.Graveyard(1-side).Select(c=>c.Id)) &&
                !d.TryQueueTargetedAttack(2,3,out _),"A06 hidden defense matrix matches legacy and consumes attack");
            check(d.TryCaptureCardTarget(1-side,removed?DuelZone.Graveyard:DuelZone.Monster,removed?0:3,out var placed) &&
                placed.Occurrence==original.Occurrence,"A06 reveal preserves occurrence through final destination");
            var battleDamage = q.Trace.Where(t => t.Stage == ActionTraceStage.Executed)
                .Select(t => t.Entry.Command).OfType<DamageCommand>();
            check(battleDamage.Select(damage => damage.AffectedCombatantSlot).SequenceEqual(
                loss > 0 ? new int?[] { 2 } : []), "V06: damage against a revealed defender keeps the attacker slot");
            var trace=ActionTraceProjection.ReadExecuted(q.Trace);
            check(trace.Take(3).Select(t=>t.Command).SequenceEqual(new[]{
                "DeclareTargetedAttackCommand","RevealBattleDefenderCommand","ResolveRevealedBattleCommand"}) &&
                trace.Skip(1).Take(2).All(t=>t.Mapping=="unmapped"),
                "A06 reveal/calculation remain visible and unmapped");
        }

        foreach(bool afterReveal in new[]{false,true})
        {
            var stale=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1351,500,500),3),
                Slots(Monster(24,1400,500),2),8000);
            stale.MonsterFaceDown[0,3]=stale.DefensePosition[0,3]=true;
            var gate=new Gate();var sq=stale.CreatePrimitiveActionQueue(gate);
            check(stale.BeginBattle(out _) && stale.TryQueueTargetedAttack(2,3,out _),"A06 stale fixture admitted");
            sq.Tick();sq.Tick();gate.Release=true;sq.Tick();
            if(afterReveal)
            {
                gate.Release=false;sq.Tick();sq.Tick();gate.Release=true;sq.Tick();
            }
            var replacement=Monster(1351,500,500);
            stale.Monsters[0,3]=replacement;
            bool fault=false;
            try {sq.Tick();} catch(InvalidOperationException){fault=true;}
            check(fault && sq.Fault is not null && ReferenceEquals(stale.Monsters[0,3],replacement) &&
                stale.Life.SequenceEqual([8000,8000]) && stale.Graveyard(0).Count==0,
                "A06 same-ID replacement faults safely before next stage");
        }

        var hiddenEffect=DemoDuel.FromPartialObservation(1,[],Slots(Monster(FlipEffectRules.ImmortalOfThunder,500,500),3),
            Slots(Monster(24,1400,500),2),8000);
        hiddenEffect.MonsterFaceDown[0,3]=hiddenEffect.DefensePosition[0,3]=true;
        var hq=hiddenEffect.CreatePrimitiveActionQueue();hiddenEffect.BeginBattle(out _);
        check(!hiddenEffect.TryQueueTargetedAttack(2,3,out _) && hq.Trace.Count==0 &&
            hiddenEffect.MonsterFaceDown[0,3],"A06 unsupported FLIP remains legacy, no partial reveal");

        var withUnsupportedExtra = DemoDuel.FromPartialObservation(1, [], Slots(Monster(1351, 500, 500), 3),
            Slots(Monster(24, 1400, 500), 2), 8000);
        withUnsupportedExtra.Monsters[1, 0] = Monster(540, 500, 500);
        var unsupportedQueue = withUnsupportedExtra.CreatePrimitiveActionQueue();
        check(withUnsupportedExtra.BeginBattle(out _) && !withUnsupportedExtra.TryQueueTargetedAttack(2, 3, out _) &&
            unsupportedQueue.Trace.Count == 0 && withUnsupportedExtra.CanAttack(2),
            "A06 an unsupported extra monster keeps the battle outside the pilot");

        var d2=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1351,500,500),3),Slots(Monster(24,1400,500),2),8000);
        d2.Life[0]=100;var q2=d2.CreatePrimitiveActionQueue();d2.BeginBattle(out _);
        d2.TryQueueTargetedAttack(2,3,out _);for(int i=0;i<30 && q2.IsBusy;i++)q2.Tick();
        check(d2.Winner==1 && d2.Graveyard(0).Count==1 && !q2.IsBusy,"A05.8 reserved cleanup after lethal (sandbox policy)");
        var d3=DemoDuel.FromPartialObservation(1,[],Slots(Monster(FlipEffectRules.MorphingJar2,500,500),3),Slots(Monster(24,1400,500),2),8000);
        var q3=d3.CreatePrimitiveActionQueue();d3.BeginBattle(out _);
        check(!d3.TryQueueTargetedAttack(2,3,out _) && q3.Trace.Count==0,"A05.8 effect ID rejected");
    }
}
