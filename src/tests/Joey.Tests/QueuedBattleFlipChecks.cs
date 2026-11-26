using System.Text.Json;
using Joey.Core;

internal static class QueuedBattleFlipChecks
{
    private static Card Monster(ushort id,int atk=1000,int def=1000,string name="Fixture") =>
        new(id,id,name,"",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card card,int slot)
    {
        var slots=new Card?[5];slots[slot]=card;return slots;
    }
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}=true;
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>
            command.Command is not (DrawCommand or DamageCommand) || Release;
    }
    private static void Until(ActionQueue q,Func<bool> stop)
    {
        for(int i=0;i<100&&!stop()&&q.IsBusy;i++)q.Tick();
        if(!stop())throw new InvalidOperationException("Battle FLIP did not reach expected stage.");
    }
    private static void Drain(ActionQueue q)=>Until(q,()=>!q.IsBusy);

    private static void CheckEquippedAttacker(Action<bool, string> check)
    {
        var magePower = new Card(DynamicEquipmentRules.MagePower, DynamicEquipmentRules.MagePower,
            "Mage Power", "", 22u << 20);
        foreach (int attackerSide in new[] { 0, 1 })
        foreach (ushort id in new[] { FlipEffectRules.Skelengel, FlipEffectRules.ManEaterBug, FlipEffectRules.PenguinSoldier })
        {
            int owner = 1 - attackerSide;
            var attacker = Monster(24, atk: 1200);
            var defender = Monster(id, def: 1500);
            var draw = Monster(1351);
            var duel = DemoDuel.FromPartialObservation(owner, [magePower],
                owner == 0 ? Slots(defender, 3) : Slots(attacker, 2),
                owner == 1 ? Slots(defender, 3) : Slots(attacker, 2), 8000,
                equipmentContextResolved: true, side0Deck: owner == 0 ? [draw] : [],
                side1Deck: owner == 1 ? [draw] : []);
            duel.MonsterFaceDown[owner, 3] = duel.DefensePosition[owner, 3] = true;
            var console = new DuelConsoleSession(duel);
            var queue = console.Queue;
            check(duel.TryQueueEquipment(DuelZone.Hand, 0, attackerSide, 2, out _),
                "The defending player equips the opposing attacker");
            Drain(queue);
            check(duel.EndTurn(out _) && duel.BeginBattle(out _) && duel.AttackValue(attackerSide, 2) == 1700 &&
                console.Execute("attack 2 3").Enqueued, "The equipped attacker enters the queued battle FLIP");
            Until(queue, () => queue.PendingDecision is not null || duel.PendingFlipTarget is not null);
            check(duel.IsMarkedForBattleDestruction(owner, 3) &&
                duel.Life.SequenceEqual([8000, 8000]), "The equipment bonus changes the destruction marker before FLIP");

            if (id == FlipEffectRules.Skelengel)
            {
                check(queue.PendingDecision is { } decision &&
                    console.Execute($"decision {decision.Id} accept").Success, "Skelengel accepts the queued draw");
            }
            else
            {
                check(duel.PendingFlipTarget is { } pending && duel.UsesQueuedMonsterFlipSelection(pending) &&
                    console.Execute($"select-flip {attackerSide} 2").Enqueued,
                    "The FLIP targets the original equipped attacker");
            }
            Drain(queue);

            check(duel.Graveyard(owner).SequenceEqual(id == FlipEffectRules.Skelengel
                    ? [defender] : [magePower, defender]) &&
                (id switch
                {
                    FlipEffectRules.Skelengel => duel.HandFor(owner).SequenceEqual([draw]) &&
                        duel.EquipmentCards.Count == 1 && duel.Monsters[attackerSide, 2] == attacker,
                    FlipEffectRules.ManEaterBug => duel.Graveyard(attackerSide).SequenceEqual([attacker]) &&
                        duel.EquipmentCards.Count == 0,
                    _ => duel.HandFor(attackerSide).SequenceEqual([attacker]) && duel.EquipmentCards.Count == 0
                }) && !console.HasPendingChoice && queue.Fault is null,
                "The FLIP and any linked cleanup complete before sending the marked defender");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        CheckEquippedAttacker(check);
        foreach(int side in new[]{0,1})
        foreach(int atk in new[]{1400,1000,600})
        foreach(bool accept in new[]{true,false})
        {
            int owner=1-side;
            var a=Monster(24,atk);var b=Monster(FlipEffectRules.Skelengel);
            var pick=Monster(1351);
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                    side==1?Slots(a,2):Slots(b,3),8000,
                    side0Deck:owner==0?[pick]:null,side1Deck:owner==1?[pick]:null);
                if(side==0){d.NextTurn();d.NextTurn();}
                d.MonsterFaceDown[owner,3]=d.DefensePosition[owner,3]=true;
                return d;
            }
            var answer=accept?EffectDecisionAnswer.Accept:EffectDecisionAnswer.Decline;
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(2,3,out _)&&legacy.PendingEffectDecision is not null,
                "A06 Skelengel legacy battle fixture");
            check(legacy.ResolveEffectDecision(legacy.PendingEffectDecision!.Id,answer,out _),"A06 legacy FLIP answer");

            (DemoDuel Duel,ActionTraceEntry[] Trace) Pilot(bool delayed)
            {
                var d=State();var gate=new Gate();var console=new DuelConsoleSession(d,delayed?gate:null);
                var q=console.Queue;
                d.TryCaptureCardTarget(owner,DuelZone.Monster,3,out var original);
                d.TryCaptureCardTarget(owner,DuelZone.Deck,0,out var drawn);
                check(d.BeginBattle(out _)&&console.Execute("attack 2 3").Enqueued,"A06 Skelengel battle admitted");
                if(delayed&&atk<1000)
                {
                    gate.Release=false;
                    Until(q,()=>q.Active?.Command is DamageCommand);
                    q.Tick();
                    check(d.Life[side]==7600&&q.PendingDecision is null&&d.Monsters[owner,3] is not null,
                        "A06 damage gate before optional FLIP");
                    gate.Release=true;
                }
                Until(q,()=>q.PendingDecision is not null);
                var request=q.PendingDecision!;
                check(request.Owner==owner&&request.Kind==EffectDecisionKind.DrawOneOnFlip&&
                    d.PendingEffectDecision is null&&d.Monsters[owner,3] is not null&&
                    !d.MonsterFaceDown[owner,3]&&d.DefensePosition[owner,3]&&
                    d.IsMarkedForBattleDestruction(owner,3)==(atk>1000)&&
                    d.Graveyard(owner).Count==0&&d.HandFor(owner).Count==0&&
                    d.Life[side]==8000-Math.Max(0,1000-atk),
                    "A06 battle decision keeps original revealed defender and mark");
                using(var trace=JsonDocument.Parse(console.Execute("trace").Message))
                    check(!trace.RootElement.GetProperty("complete").GetBoolean(),"A06 pending battle trace incomplete");
                check(new DuelConsoleScript(console,["damage 1"]).Advance().Status==DuelScriptStatus.WaitingForDecision&&
                    !console.Execute("damage 1").Success&&!d.ResolveEffectDecision(request.Id,answer,out _)&&
                    !q.SubmitDecision(request.Id+1,answer),"A06 only current queued decision admitted");
                check(console.Execute($"decision {request.Id} {(accept?"accept":"decline")}").Success&&
                    !q.SubmitDecision(request.Id,answer),"A06 answer submitted exactly once");
                if(delayed&&accept)
                {
                    gate.Release=false;Until(q,()=>q.Active?.Command is DrawCommand);q.Tick();
                    check(d.HandFor(owner).Count==1&&d.Monsters[owner,3] is not null&&
                        d.Graveyard(owner).Count==0&&d.IsMarkedForBattleDestruction(owner,3)==(atk>1000),
                        "A06 draw gate delays battle removal, no repeated draw");
                    gate.Release=true;
                }
                Drain(q);
                check(q.Fault is null&&!console.HasPendingChoice&&d.Life.SequenceEqual(legacy.Life)&&
                    d.HandFor(owner).SequenceEqual(legacy.HandFor(owner))&&
                    d.Graveyard(owner).SequenceEqual(legacy.Graveyard(owner))&&
                    d.DeckSnapshot(owner).SequenceEqual(legacy.DeckSnapshot(owner))&&
                    d.EffectDecisionHistory.SequenceEqual(legacy.EffectDecisionHistory)&&
                    !d.IsMarkedForBattleDestruction(owner,3)&&!d.TryQueueTargetedAttack(2,3,out _),
                    "A06 queued battle FLIP matches legacy and consumes attack");
                check(d.TryCaptureCardTarget(owner,atk>1000?DuelZone.Graveyard:DuelZone.Monster,atk>1000?0:3,out var placed)&&
                    placed.Occurrence==original.Occurrence,"A06 original defender identity preserved");
                if(accept)check(d.TryCaptureCardTarget(owner,DuelZone.Hand,0,out var moved)&&moved.Occurrence==drawn.Occurrence,
                    "A06 original draw identity preserved");
                var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).ToArray();
                check(executed.All(t=>t.Result?.Outcome==CommandOutcome.Applied)&&
                    executed.Count(t=>t.Entry.Command is DrawCommand)==(accept?1:0)&&
                    executed.Count(t=>t.Entry.Command.GetType().Name=="SnapshotBattleRemovalCommand")==(atk>1000?1:0),
                    "A06 battle FLIP exact operations");
                return (d,q.Trace.ToArray());
            }
            var immediate=Pilot(false);var gated=Pilot(true);
            check(immediate.Trace.SequenceEqual(gated.Trace),"A06 battle FLIP trace independent of presentation");
        }

        foreach(bool lethal in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(1,[],Slots(Monster(539),3),
                Slots(Monster(24,lethal?600:1400),2),lethal?100:8000,
                side0Deck:lethal?[Monster(1351)]:[]);
            d.MonsterFaceDown[0,3]=d.DefensePosition[0,3]=true;
            var q=d.CreatePrimitiveActionQueue();d.BeginBattle(out _);
            check(d.TryQueueTargetedAttack(2,3,out _),"A06 no-prompt battle admitted");
            Drain(q);
            check(q.PendingDecision is null&&d.EffectDecisionHistory.Count==0&&
                !q.Trace.Any(t=>t.Entry.Command is DrawCommand)&&
                q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).All(t=>t.Result?.Outcome==CommandOutcome.Applied)&&
                (lethal?d.Winner==0&&d.Monsters[0,3] is not null:d.Graveyard(0).Single().Id==539),
                "A06 empty deck or lethal damage skips prompt and still finishes battle");
        }

        Card[] pieces=ExodiaRules.RequiredNames.Select((name,i)=>Monster((ushort)(800+i),name:name)).ToArray();
        var exodia=DemoDuel.FromPartialObservation(0,pieces[..4],Slots(Monster(539),3),
            Slots(Monster(24,1400),2),8000,side0Deck:[pieces[4]]);
        exodia.NextTurn();
        exodia.MonsterFaceDown[0,3]=exodia.DefensePosition[0,3]=true;
        var eq=exodia.CreatePrimitiveActionQueue();exodia.BeginBattle(out _);
        check(exodia.TryQueueTargetedAttack(2,3,out _),"A06 Exodia battle admitted");
        Until(eq,()=>eq.PendingDecision is not null);
        check(eq.SubmitDecision(eq.PendingDecision!.Id,EffectDecisionAnswer.Accept),"A06 Exodia draw accepted");
        Drain(eq);
        check(exodia.Winner==0&&exodia.Graveyard(0).Single().Id==539&&exodia.Monsters[0,3] is null&&
            eq.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).All(t=>t.Result?.Outcome==CommandOutcome.Applied),
            "A06 cleanup completes after Exodia win");

        var faceup=DemoDuel.FromPartialObservation(1,[],Slots(Monster(539),3),Slots(Monster(24),2),8000);
        var fq=faceup.CreatePrimitiveActionQueue();faceup.BeginBattle(out _);
        check(!faceup.TryQueueTargetedAttack(2,3,out _)&&fq.Trace.Count==0,
            "A06 Skelengel pilot restricted to hidden defense");
    }
}
