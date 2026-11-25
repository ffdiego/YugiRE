using System.Text.Json;
using Joey.Core;

internal static class QueuedBattleRecruiterChecks
{
    private static Card Monster(ushort id,byte attr=2,int atk=1400,int def=800) =>
        new(id,id,$"Fixture {id}","",(3u<<20)|(4u<<25)|((uint)attr<<29)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(Card c,int slot)
    {
        var s=new Card?[5];s[slot]=c;return s;
    }
    private sealed class Gate : IPresentationGate
    {
        public bool Release {get;set;}=true;
        public bool CanAdvance=>true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result)=>
            Release || (command.Command.GetType().Name!="FinishBattleRemovalCommand" &&
                command.Command is not SpecialSummonCommand);
    }
    private static void Until(ActionQueue q,Func<bool> stop)
    {
        for(int i=0;i<100&&!stop()&&q.IsBusy;i++)q.Tick();
        if(!stop())throw new InvalidOperationException("P164 battle did not reach expected stage.");
    }
    private static void Drain(ActionQueue q)=>Until(q,()=>!q.IsBusy);
    private static bool Complete(DuelConsoleSession s)
    {
        using var doc=JsonDocument.Parse(s.Execute("trace").Message);
        return doc.RootElement.GetProperty("complete").GetBoolean();
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(var (id,attr) in new (ushort,byte)[]{(1123,2),(1108,5),(1117,1),(1110,4),(1119,3),(1120,6)})
        foreach(int owner in new[]{0,1})
        foreach(bool recruiterAttacks in new[]{false,true})
        foreach(bool accept in new[]{false,true})
        {
            int side=recruiterAttacks?owner:1-owner;
            var recruiter=Monster(id,attr);
            var strong=Monster(24,1,2200,2000);
            var a=recruiterAttacks?recruiter:strong;
            var b=recruiterAttacks?strong:recruiter;
            var good=Monster(1351,attr,1500);
            Card[] deck=[Monster(901,attr,1600),good,Monster(902,attr==6?(byte)1:(byte)(attr+1)),good];
            int deadSlot=recruiterAttacks?2:3;
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?Slots(a,2):Slots(b,3),
                    side==1?Slots(a,2):Slots(b,3),8000,
                    side0Deck:owner==0?deck:null,side1Deck:owner==1?deck:null,effectShuffleSeed:77);
                if(side==0){d.NextTurn();d.NextTurn();}
                return d;
            }
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(2,3,out _)&&legacy.PendingCardSelection?.Owner==owner,
                "A06 P164 legacy comparator");
            check(accept?legacy.ResolveCardSelection(3,4,out _):legacy.SkipOptionalCardSelection(out _),
                "A06 P164 legacy response");
            var d=State();var gate=new Gate();var console=new DuelConsoleSession(d,gate);var q=console.Queue;
            int shuffles=0,summons=0;d.DeckShuffled+=_=>shuffles++;d.MonsterSpecialSummoned+=_=>summons++;
            d.TryCaptureCardTarget(owner,DuelZone.Monster,deadSlot,out var original);
            d.TryCaptureCardTarget(owner,DuelZone.Deck,3,out var pick);
            check(d.BeginBattle(out _)&&console.Execute("attack 2 3").Enqueued,"A06 P164 battle admitted");
            gate.Release=false;
            Until(q,()=>q.Active?.Command.GetType().Name=="FinishBattleRemovalCommand");
            q.Tick();
            check(d.PendingCardSelection is null&&d.Graveyard(owner).Count==1&&
                d.TryCaptureCardTarget(owner,DuelZone.Graveyard,0,out var buried)&&buried.Occurrence==original.Occurrence&&
                !q.Trace.Any(t=>t.Entry.Command.GetType().Name=="OpenBattleRecruitmentCommand"),
                "A06 P164 GY arrival gate before choice");
            gate.Release=true;Drain(q);
            var request=d.PendingCardSelection!;
            check(request is not null&&request.Owner==owner&&request.EffectResourceId==id&&
                request.RequiredAttributeCode==attr&&request.MinSelections==0&&
                d.QueuedDeckRecruitmentCandidates(request).Select(c=>c.SourceIndex).SequenceEqual(new[]{1,3})&&
                console.HasPendingChoice&&!Complete(console)&&
                !d.ResolveCardSelection(3,4,out _)&&!d.SkipOptionalCardSelection(out _)&&
                !console.Execute("select-recruit 0 4").Success&&!console.Execute("damage 1").Success,
                "A06 P164 exact owner and eligible copies; legacy and unrelated responses blocked");
            check(console.Execute(accept?"select-recruit 3 4":"select-recruit decline").Enqueued&&
                d.PendingCardSelection==request&&d.DeckCount(owner)==4,"A06 P164 response admission read-only");
            if(accept)
            {
                gate.Release=false;Until(q,()=>q.Active?.Command is SpecialSummonCommand);q.Tick();
                check(summons==1&&shuffles==0&&d.Monsters[owner,4] is not null&&
                    d.TryCaptureCardTarget(owner,DuelZone.Monster,4,out var placed)&&placed.Occurrence==pick.Occurrence&&
                    !d.MonsterFaceDown[owner,4]&&!d.DefensePosition[owner,4],
                    "A06 P164 summon gate precedes shuffle and retains identity");
                gate.Release=true;
            }
            Drain(q);
            check(q.Fault is null&&!console.HasPendingChoice&&Complete(console)&&
                d.Life.SequenceEqual(legacy.Life)&&d.DeckSnapshot(owner).SequenceEqual(legacy.DeckSnapshot(owner))&&
                d.Graveyard(owner).SequenceEqual(legacy.Graveyard(owner))&&
                d.Monsters[owner,4]==legacy.Monsters[owner,4]&&summons==(accept?1:0)&&shuffles==(accept?1:0),
                "A06 P164 queued full battle matches legacy final state");
            check(q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).All(t=>t.Result?.Outcome==CommandOutcome.Applied),
                "A06 P164 all admitted operations applied");
        }

        foreach(int side in new[]{0,1})
        {
            int enemy=1-side;
            var both=DemoDuel.FromPartialObservation(side,[],Slots(Monster(1123,2),side==0?2:3),
                Slots(Monster(1108,5),side==1?2:3),8000,
                side0Deck:[Monster(24,2)],side1Deck:[Monster(1351,5)]);
            if(side==0){both.NextTurn();both.NextTurn();}
            var gate=new Gate();var console=new DuelConsoleSession(both,gate);var q=console.Queue;
            both.BeginBattle(out _);
            check(console.Execute("attack 2 3").Enqueued,"A06 dual P164 admitted");
            gate.Release=false;Until(q,()=>q.Active?.Command.GetType().Name=="FinishBattleRemovalCommand");
            check(both.Graveyard(side).Count==1&&both.Graveyard(enemy).Count==0&&both.PendingCardSelection is null,
                "A06 first GY arrival does not open dual selection");
            gate.Release=true;q.Tick();gate.Release=false;
            Until(q,()=>q.Active?.Command.GetType().Name=="FinishBattleRemovalCommand");
            check(both.Graveyard(0).Count==1&&both.Graveyard(1).Count==1&&both.PendingCardSelection is null,
                "A06 both GY arrivals precede any prompt");
            gate.Release=true;Drain(q);
            check(both.PendingCardSelection?.Owner==side&&console.Execute("select-recruit decline").Enqueued,
                "A06 dual P164 attacker owner first");
            Drain(q);
            check(both.PendingCardSelection?.Owner==enemy&&!both.SkipOptionalCardSelection(out _)&&
                console.Execute("select-recruit 0 4").Enqueued,"A06 dual P164 defender owner second");
            Drain(q);
            check(both.PendingCardSelection is null&&both.DeckCount(side)==1&&both.DeckCount(enemy)==0&&
                both.Monsters[enemy,4] is not null&&!both.GameOver&&Complete(console),
                "A06 dual P164 serialized requests and last-card summon");
        }

        foreach(bool faceDown in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1123),3),Slots(Monster(24,1,2200),2),8000,
                side0Deck:[Monster(1351)]);
            d.MonsterFaceDown[0,3]=d.DefensePosition[0,3]=faceDown;
            var q=d.CreatePrimitiveActionQueue();d.BeginBattle(out _);
            check(d.TryQueueTargetedAttack(2,3,out _),"A06 P164 pose admitted");
            Drain(q);
            check(d.PendingCardSelection?.EffectResourceId==1123&&d.Graveyard(0).Count==1&&
                q.Trace.Any(t=>t.Entry.Command.GetType().Name=="RevealBattleDefenderCommand")==faceDown,
                "A06 P164 face-down arrival recruits only after reveal and battle");
        }

        var effect=DemoDuel.FromPartialObservation(1,[],Slots(Monster(500,2,400,600),3),
            Slots(Monster(1123),2),8000,side1Deck:[Monster(1351)]);
        effect.MonsterFaceDown[0,3]=effect.DefensePosition[0,3]=true;
        var ec=new DuelConsoleSession(effect);effect.BeginBattle(out _);
        check(ec.Execute("attack 2 3").Enqueued,"A06 recruiter attacks Man-Eater");
        Drain(ec.Queue);
        check(ec.Execute("select-flip 1 2").Enqueued,"A06 Man-Eater destroys recruiter by effect");
        Drain(ec.Queue);
        check(effect.Graveyard(1).Single().Id==1123&&effect.Graveyard(0).Single().Id==500&&
            effect.PendingCardSelection is null&&effect.DeckCount(1)==1&&
            !ec.Queue.Trace.Any(t=>t.Entry.Command.GetType().Name=="OpenBattleRecruitmentCommand"),
            "A06 effect-destroyed recruiter never opens battle recruitment");

        foreach(bool lethal in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1123),3),
                Slots(Monster(24,1,2200),2),8000,side0Deck:lethal?[Monster(1351)]:[Monster(901,1)]);
            if(lethal)d.Life[0]=100;
            var q=d.CreatePrimitiveActionQueue();d.BeginBattle(out _);
            check(d.TryQueueTargetedAttack(2,3,out _),"A06 no-choice P164 admitted");Drain(q);
            check(d.PendingCardSelection is null&&d.Graveyard(0).Count==1&&d.GameOver==lethal&&
                q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).All(t=>t.Result?.Outcome==CommandOutcome.Applied),
                "A06 no eligible cards or lethal damage finishes without prompt");
        }
    }
}
