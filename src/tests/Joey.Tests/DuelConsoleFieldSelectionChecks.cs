using System.Text.Json;
using Joey.Core;

internal static class DuelConsoleFieldSelectionChecks
{
    private static Card Monster(ushort id, int atk=1000, byte attribute=2) =>
        new(id,id,"Fixture","",(3u<<20)|(4u<<25)|((uint)attribute<<29)|((uint)(atk/10)<<9)|80u);
    private static Card Spell(ushort id) => new(id,id,"Fixture","",22u<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();

    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; } = true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not SpecialSummonCommand || Release;
    }

    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<200 && q.IsBusy;i++) q.Tick();
        if(q.IsBusy) throw new InvalidOperationException("Field selection did not drain.");
    }

    private static DuelScriptStep Run(DuelConsoleSession console, params string[] lines)
    {
        var script = new DuelConsoleScript(console,lines);
        for(int i=0;i<200;i++)
        {
            var step=script.Advance();
            if(step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision)
                return step;
            if(console.Queue.IsBusy) console.Queue.Tick();
        }
        throw new InvalidOperationException("Field selection script did not finish.");
    }

    private static bool Complete(DuelConsoleSession console)
    {
        using var trace=JsonDocument.Parse(console.Execute("trace").Message);
        return trace.RootElement.GetProperty("complete").GetBoolean();
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int owner in new[]{0,1})
        foreach(bool decline in new[]{false,true})
        {
            var vermin=Monster(1307);
            var own=Slots(vermin);
            var d=DemoDuel.FromPartialObservation(owner,[],owner==0?own:Slots(),owner==1?own:Slots(),8000,
                side0Deck:owner==0?[vermin,Monster(24),vermin]:null,
                side1Deck:owner==1?[vermin,Monster(24),vermin]:null);
            d.MonsterFaceDown[owner,0]=d.DefensePosition[owner,0]=true;
            var console=new DuelConsoleSession(d);
            check(Run(console,"flip 0","damage 200 1").Status==DuelScriptStatus.WaitingForDecision &&
                console.PendingChoicePrompt.Contains("select-recruit") && !Complete(console),"A06 field recruitment pauses script");
            var choices=console.Execute("choices");
            int before=console.Queue.Trace.Count;
            check(choices.Success && choices.Message.Contains($"Side {owner} Deck") &&
                choices.Message.Contains("2: 1307") && !choices.Message.Contains("1: 24") &&
                !console.Execute("damage 200 1").Success &&
                !console.Execute("select-revive 0 4 attack").Success &&
                !console.Execute("select-recruit 1 4").Success &&
                !console.Execute("select-recruit 2 0").Success &&
                !console.Execute("select-recruit batch 0 2").Success &&
                console.Queue.Trace.Count==before,"A06 field choice is read-only and rejects wrong zone/copy/slot");
            check(d.TryCaptureCardTarget(owner,DuelZone.Deck,2,out var picked),"A06 track recruit");
            check(Run(console,decline?"select-recruit decline":"select-recruit 2 4","state").Status==DuelScriptStatus.Completed &&
                Complete(console) && d.DeckCount(owner)==(decline?3:2) &&
                console.Queue.Trace.Count(t=>t.Stage==ActionTraceStage.Executed && t.Entry.Command is ShuffleDeckCommand)==(decline?0:1),
                "A06 field decline or summon then shuffle");
            if(!decline) check(d.TryCaptureCardTarget(owner,DuelZone.Monster,4,out var placed) &&
                placed.Occurrence==picked.Occurrence && d.MonsterFaceDown[owner,4] && d.DefensePosition[owner,4],
                "A06 Bubonic preserves selected copy and set position");
        }

        // Resolve the battle through the real queue before the owner chooses
        // the recruited occurrence. The same queue owns both operations.
        foreach(ushort id in new ushort[]{1123,1114,1115})
        {
            var recruit=Monster(id,1400);
            var good=id==1123?Monster(24,1000):recruit;
            var d=DemoDuel.FromPartialObservation(1,[],Slots(recruit),Slots(Monster(24,2200,1)),8000,
                side0Deck:[good,Monster(25,1600),good], checkpoint: new DuelCheckpoint(2, DemoPhase.Battle));
            var gate=new Gate(); var console=new DuelConsoleSession(d,gate);
            check(d.TryQueueTargetedAttack(0,0,out _,ActionOrigin.Test), "A06 battle admitted");
            Drain(console.Queue);
            check(d.PendingCardSelection is {Owner:0},
                "A06 completed queued battle creates non-active owner request");
            check(!Complete(console) && Run(console).Status==DuelScriptStatus.WaitingForDecision,
                "A06 even empty script cannot complete at field prompt");
            check(d.TryCaptureCardTarget(0,DuelZone.Deck,2,out var first),"A06 track selected first copy");
            if(id==1123)
                check(!console.Execute("select-recruit batch 2 0").Success &&
                    console.Execute("select-recruit 2 4").Enqueued,"A06 elemental recruit uses explicit slot");
            else
                check(!console.Execute("select-recruit batch 2 2").Success &&
                    !console.Execute("select-recruit 2 4").Success &&
                    console.Execute("select-recruit batch 2 0").Enqueued,"A06 same-name batch accepts distinct indices");
            console.Queue.Tick();
            gate.Release=false; console.Queue.Tick(); console.Queue.Tick();
            int firstSlot=id==1123?4:0;
            check(console.Queue.IsBusy && d.TryCaptureCardTarget(0,DuelZone.Monster,firstSlot,out var moved) &&
                moved.Occurrence==first.Occurrence && d.DeckCount(0)==2 &&
                !console.Queue.Trace.Any(t=>t.Entry.Command is ShuffleDeckCommand),"A06 first recruit gates later movement and shuffle");
            gate.Release=true; Drain(console.Queue);
            check(Complete(console) && d.DeckCount(0)==(id==1123?2:1) &&
                console.Queue.Trace.Count(t=>t.Stage==ActionTraceStage.Executed && t.Entry.Command is ShuffleDeckCommand)==1 &&
                d.MonsterFaceDown[0,firstSlot]==(id==1115),"A06 correct recruit count, pose and single shuffle");
        }

        foreach(int owner in new[]{0,1})
        {
            int enemy=1-owner;
            var spear=Monster(1116,500); var ally=Monster(24); var other=Monster(25);
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(336)],owner==0?Slots(spear):Slots(other),
                owner==1?Slots(spear):Slots(other),8000,
                side0Graveyard:owner==0?[ally,Spell(342),spear]:[other],
                side1Graveyard:owner==1?[ally,Spell(342),spear]:[other]);
            d.MonsterFaceDown[owner,0]=d.DefensePosition[owner,0]=true;
            check(d.RevealMonsterByEffect(owner,0,out _),"A06 Spear history setup");
            var gate=new Gate(); var console=new DuelConsoleSession(d,gate);
            check(Run(console,"activate hand 0","state").Status==DuelScriptStatus.WaitingForDecision &&
                console.PendingChoicePrompt.Contains("select-revive") && !Complete(console),"A06 mandatory revival pauses");
            var options=console.Execute("choices");
            check(options.Success && options.Message.Contains("2: 1116") &&
                !options.Message.Contains("3: 1116") && !console.Execute("select-revive 3 4 set").Success &&
                !console.Execute("select-revive 1 4 attack").Success &&
                !console.Execute("select-revive decline").Success,"A06 excludes exact triggering Spear and spell");
            check(d.TryCaptureCardTarget(owner,DuelZone.Graveyard,2,out var target) &&
                console.Execute("select-revive 2 4 set").Enqueued,"A06 select first revival");
            console.Queue.Tick(); gate.Release=false; console.Queue.Tick(); console.Queue.Tick();
            check(console.Queue.IsBusy && d.PendingCardSelection is null && d.MonsterFaceDown[owner,4] &&
                d.TryCaptureCardTarget(owner,DuelZone.Monster,4,out var placed) && placed.Occurrence==target.Occurrence,
                "A06 first revival gates next owner's request");
            gate.Release=true; Drain(console.Queue);
            check(d.PendingCardSelection is {Owner:var next} && next==enemy && !Complete(console) &&
                !console.Execute("select-revive 0 0 attack").Success,"A06 second owner cannot use occupied slot");
            check(Run(console,"select-revive 0 1 attack","state").Status==DuelScriptStatus.Completed &&
                Complete(console) && d.Monsters[enemy,1]?.Id==25 && !d.MonsterFaceDown[enemy,1] &&
                !console.Queue.Trace.Any(t=>t.Entry.Command is ShuffleDeckCommand),"A06 both revivals use same queue without shuffle");
        }
    }
}
