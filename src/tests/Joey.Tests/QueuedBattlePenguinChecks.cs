using System.Text.Json;
using Joey.Core;

internal static class QueuedBattlePenguinChecks
{
    private static Card Monster(ushort id,int atk=1000,int def=1000) =>
        new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card card,int slot)
    {
        var result=new Card?[5];result[slot]=card;return result;
    }
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}=true;
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>
            command.Command is not (ReturnToHandCommand or DamageCommand) || Release;
    }
    private static void Until(ActionQueue q,Func<bool> stop)
    {
        for(int i=0;i<100&&!stop()&&q.IsBusy;i++)q.Tick();
        if(!stop())throw new InvalidOperationException("Penguin battle did not reach expected stage.");
    }
    private static void Drain(ActionQueue q)=>Until(q,()=>!q.IsBusy);
    private static bool Complete(DuelConsoleSession session)
    {
        using var trace=JsonDocument.Parse(session.Execute("trace").Message);
        return trace.RootElement.GetProperty("complete").GetBoolean();
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(int atk in new[]{1400,1000,600})
        foreach(int choice in atk>1000?new[]{0,1}:new[]{0,1,2,3})
        {
            int owner=1-side;
            var a=Monster(24,atk);var b=Monster(FlipEffectRules.PenguinSoldier);
            bool returnAttacker=(choice&1)!=0,returnDefender=(choice&2)!=0;
            bool destroyed=atk>1000;
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                    side==1?Slots(a,2):Slots(b,3),8000);
                if(side==0){d.NextTurn();d.NextTurn();}
                d.MonsterFaceDown[owner,3]=d.DefensePosition[owner,3]=true;
                return d;
            }
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(2,3,out _)&&legacy.PendingFlipTarget is not null,
                "A06 Penguin legacy battle fixture");
            if(returnAttacker)check(legacy.ResolveFlipTarget(side,2,out _),"A06 legacy selects attacker");
            if(returnDefender)check(legacy.ResolveFlipTarget(owner,3,out _),"A06 legacy selects surviving Penguin");
            if(legacy.PendingFlipTarget is not null)check(legacy.FinishFlipTargets(out _),"A06 legacy optional finish");

            ActionTraceEntry[] Pilot(bool delayed)
            {
                var d=State();var gate=new Gate();var console=new DuelConsoleSession(d,delayed?gate:null);
                var q=console.Queue;
                d.TryCaptureCardTarget(side,DuelZone.Monster,2,out var originalA);
                d.TryCaptureCardTarget(owner,DuelZone.Monster,3,out var originalB);
                check(d.BeginBattle(out _)&&console.Execute("attack 2 3").Enqueued,"A06 Penguin attack admitted");
                if(delayed&&atk<1000)
                {
                    gate.Release=false;Until(q,()=>q.Active?.Command is DamageCommand);q.Tick();
                    check(d.PendingFlipTarget is null&&d.Life[side]==7600,"A06 Penguin damage gate before choice");
                    gate.Release=true;
                }
                Drain(q);
                var candidates=d.QueuedMonsterFlipCandidates();
                check(console.HasPendingChoice&&!Complete(console)&&d.PendingFlipTarget is {Owner:var p}&&p==owner&&
                    candidates.Count==(destroyed?1:2)&&candidates.Any(c=>c.Target==originalA)&&
                    candidates.Any(c=>c.Target==originalB)==!destroyed&&
                    d.IsMarkedForBattleDestruction(owner,3)==destroyed&&d.Graveyard(owner).Count==0&&
                    d.Life[side]==8000-Math.Max(0,1000-atk),
                    "A06 Penguin pending choice excludes marked defender");
                var options=console.Execute("choices");
                check(options.Success&&options.Message.Contains($"{side} 2: 24")&&
                    options.Message.Contains($"{owner} 3: 601")==!destroyed&&
                    new DuelConsoleScript(console,["damage 1"]).Advance().Status==DuelScriptStatus.WaitingForDecision&&
                    !console.Execute("damage 1").Success&&
                    !d.ResolveFlipTarget(side,2,out _)&&!d.FinishFlipTargets(out _)&&
                    !console.Execute($"select-flip {side} 2 {side} 2").Success&&
                    (!destroyed||!console.Execute($"select-flip {owner} 3").Success),
                    "A06 Penguin guards choice and legacy answer bypass");
                string input=choice switch
                {
                    0=>"select-flip decline",
                    1=>$"select-flip {side} 2",
                    2=>$"select-flip {owner} 3",
                    _=>$"select-flip {side} 2 {owner} 3"
                };
                check(console.Execute(input).Enqueued&&d.PendingFlipTarget is not null&&d.Monsters[side,2] is not null,
                    "A06 Penguin selection admission is read-only");
                if(delayed&&choice!=0)
                {
                    gate.Release=false;Until(q,()=>q.Active?.Command is ReturnToHandCommand);q.Tick();
                    check(q.IsBusy&&d.Graveyard(owner).Count==0&&
                        d.IsMarkedForBattleDestruction(owner,3)==destroyed&&
                        d.HandFor(side).Count+d.HandFor(owner).Count==1,
                        "A06 first return gate delays second return and battle cleanup");
                    gate.Release=true;
                }
                Drain(q);
                check(q.Fault is null&&!console.HasPendingChoice&&Complete(console)&&
                    d.Life.SequenceEqual(legacy.Life)&&d.HandFor(side).SequenceEqual(legacy.HandFor(side))&&
                    d.HandFor(owner).SequenceEqual(legacy.HandFor(owner))&&d.Graveyard(owner).SequenceEqual(legacy.Graveyard(owner))&&
                    (d.Monsters[side,2] is null)==returnAttacker&&
                    (d.Monsters[owner,3] is null)==(destroyed||returnDefender)&&
                    !d.IsMarkedForBattleDestruction(owner,3),
                    "A06 Penguin queue matches legacy final field, hand and LP");
                check(d.TryCaptureCardTarget(side,returnAttacker?DuelZone.Hand:DuelZone.Monster,returnAttacker?0:2,out var placedA)&&
                    placedA.Occurrence==originalA.Occurrence&&
                    d.TryCaptureCardTarget(owner,destroyed?DuelZone.Graveyard:returnDefender?DuelZone.Hand:DuelZone.Monster,
                        destroyed||returnDefender?0:3,out var placedB)&&placedB.Occurrence==originalB.Occurrence,
                    "A06 Penguin both combatants preserve occurrence identity");
                var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).ToArray();
                check(executed.All(t=>t.Result?.Outcome==CommandOutcome.Applied)&&
                    executed.Count(t=>t.Entry.Command is ReturnToHandCommand)==(returnAttacker?1:0)+(returnDefender?1:0)&&
                    executed.Count(t=>t.Entry.Command.GetType().Name=="SnapshotBattleRemovalCommand")==(destroyed?1:0)&&
                    Array.FindIndex(executed,t=>t.Entry.Command.GetType().Name=="FinishFieldRemovalCommand")<
                    Array.FindIndex(executed,t=>t.Entry.Command.GetType().Name=="FinishBattleFlipCommand"),
                    "A06 Penguin finishes returns before battle");
                return q.Trace.ToArray();
            }
            check(Pilot(false).SequenceEqual(Pilot(true)),"A06 Penguin trace independent of presentation");
        }

        foreach (int side in new[] { 0, 1 })
        {
            int owner = 1 - side;
            var bouncer = Monster(FlipEffectRules.PenguinSoldier);
            var attacker = Monster(24, 1400);
            var bystander = Monster(1351);
            DemoDuel State()
            {
                var attackers = Slots(attacker, 2);
                attackers[0] = bystander;
                var defenders = Slots(bouncer, 3);
                defenders[1] = bystander;
                var d = DemoDuel.FromPartialObservation(side, [], side == 0 ? attackers : defenders,
                    side == 1 ? attackers : defenders, 8000);
                if (side == 0) { d.NextTurn(); d.NextTurn(); }
                d.MonsterFaceDown[owner, 3] = d.DefensePosition[owner, 3] = true;
                return d;
            }

            var legacy = State();
            check(legacy.BeginBattle(out _) && legacy.Attack(2, 3, out _) &&
                legacy.ResolveFlipTarget(side, 0, out _) && legacy.ResolveFlipTarget(owner, 1, out _),
                "A06 Penguin legacy selects two noncombatants on opposite sides");
            var duel = State();
            var gate = new Gate();
            var session = new DuelConsoleSession(duel, gate);
            check(duel.BeginBattle(out _) && session.Execute("attack 2 3").Enqueued,
                "A06 Penguin admits additional supported normal monsters");
            Drain(session.Queue);
            bool capturedFirst = duel.TryCaptureCardTarget(side, DuelZone.Monster, 0, out var first);
            bool capturedSecond = duel.TryCaptureCardTarget(owner, DuelZone.Monster, 1, out var second);
            check(capturedFirst && capturedSecond && first.Occurrence != second.Occurrence &&
                duel.QueuedMonsterFlipCandidates().Count == 3 &&
                session.Execute($"select-flip {side} 0 {owner} 1").Enqueued,
                "A06 Penguin targets distinguish same-resource noncombatant occurrences");
            gate.Release = false;
            Until(session.Queue, () => session.Queue.Active?.Command is ReturnToHandCommand);
            check(duel.Monsters[side, 0] is null && duel.Monsters[owner, 1] is not null &&
                duel.IsMarkedForBattleDestruction(owner, 3) && duel.Graveyard(owner).Count == 0,
                "A06 Penguin wait after first noncombatant return preserves other targets");
            gate.Release = true;
            Drain(session.Queue);
            check(duel.TryCaptureCardTarget(side, DuelZone.Hand, 0, out var firstInHand) &&
                duel.TryCaptureCardTarget(owner, DuelZone.Hand, 0, out var secondInHand) &&
                firstInHand.Occurrence == first.Occurrence && secondInHand.Occurrence == second.Occurrence &&
                duel.Life.SequenceEqual(legacy.Life) && duel.Graveyard(owner).SequenceEqual(legacy.Graveyard(owner)) &&
                duel.HandFor(side).SequenceEqual(legacy.HandFor(side)) &&
                duel.HandFor(owner).SequenceEqual(legacy.HandFor(owner)) && duel.Monsters[side, 2] == attacker,
                "A06 Penguin moves both extra identities before battle cleanup and matches legacy");
        }

        var lethal=DemoDuel.FromPartialObservation(1,[],Slots(Monster(601),3),Slots(Monster(24,600),2),100);
        lethal.MonsterFaceDown[0,3]=lethal.DefensePosition[0,3]=true;
        var lq=lethal.CreatePrimitiveActionQueue();lethal.BeginBattle(out _);
        check(lethal.TryQueueTargetedAttack(2,3,out _),"A06 Penguin lethal fixture admitted");
        Drain(lq);
        check(lethal.Winner==0&&lethal.PendingFlipTarget is null&&lethal.Monsters[0,3] is not null&&
            lq.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).All(t=>t.Result?.Outcome==CommandOutcome.Applied),
            "A06 lethal damage skips target prompt (bounded terminal policy)");

        var other=DemoDuel.FromPartialObservation(1,[],Slots(Monster(FlipEffectRules.NeedleWorm),3),Slots(Monster(24),2),8000);
        other.MonsterFaceDown[0,3]=other.DefensePosition[0,3]=true;
        var oq=other.CreatePrimitiveActionQueue();other.BeginBattle(out _);
        check(!other.TryQueueTargetedAttack(2,3,out _)&&oq.Trace.Count==0,"A06 other FLIPs still fail closed");
    }
}
