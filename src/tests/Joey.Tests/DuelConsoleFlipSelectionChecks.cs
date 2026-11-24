using System.Text.Json;
using Joey.Core;

internal static class DuelConsoleFlipSelectionChecks
{
    private static Card Monster(ushort id) => new(id,id,"Fixture","",(15u<<20)|(4u<<25)|(100u<<9)|80u);
    private static Card Spell(ushort id, int type=22) => new(id,id,"Fixture","",(uint)type<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();

    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; } = true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command is not (DestroyCommand or ReturnToHandCommand) || Release;
    }

    private static DuelScriptStep Run(DuelConsoleSession console, params string[] lines)
    {
        var script = new DuelConsoleScript(console, lines);
        for (int i=0;i<200;i++)
        {
            var step=script.Advance();
            if (step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision)
                return step;
            if (console.Queue.IsBusy) console.Queue.Tick();
        }
        throw new InvalidOperationException("FLIP script did not finish.");
    }

    private static void Drain(ActionQueue q)
    {
        for (int i=0;i<200 && q.IsBusy;i++) q.Tick();
        if (q.IsBusy) throw new InvalidOperationException("FLIP queue did not drain.");
    }

    public static void Run(Action<bool,string> check)
    {
        foreach (int side in new[]{0,1})
        foreach (ushort id in new ushort[]{500,540,601})
        {
            int enemy=1-side;
            var own=Slots(Monster(id),Monster(24));
            var foe=Slots(Monster(25));
            var d=DemoDuel.FromPartialObservation(side,[],side==0?own:foe,side==1?own:foe,8000);
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var gate=new Gate();
            var console=new DuelConsoleSession(d,gate);
            var waiting=Run(console,"flip 0","damage 200 1");
            check(waiting.Status==DuelScriptStatus.WaitingForDecision && waiting.Message.Contains("select-flip") &&
                d.Life[1]==8000 && console.HasPendingChoice,"A06 FLIP pauses before unrelated input");
            using (var trace=JsonDocument.Parse(console.Execute("trace").Message))
                check(!trace.RootElement.GetProperty("complete").GetBoolean(),"A06 unanswered FLIP trace incomplete");
            int count=console.Queue.Trace.Count;
            check(console.Execute("choices").Success &&
                !console.Execute("damage 200 1").Success &&
                !console.Execute($"select-flip {enemy} 0 {enemy} 0").Success &&
                !console.Execute("select-flip 2 0").Success &&
                console.Queue.Trace.Count==count,"A06 read-only candidates and rejected inputs");
            if (id!=601) check(!console.Execute("select-flip decline").Success,"Mandatory FLIP cannot decline");
            check(d.TryCaptureCardTarget(enemy,DuelZone.Monster,0,out var target),"Track FLIP target");
            string answer=id==601?$"select-flip {enemy} 0 {side} 1":$"select-flip {enemy} 0";
            check(console.Execute(answer).Enqueued && d.Monsters[enemy,0] is not null,"A06 admission does not mutate");
            console.Queue.Tick();
            gate.Release=false;
            console.Queue.Tick();
            check(console.Queue.IsBusy && d.Monsters[enemy,0] is null &&
                d.Monsters[side,1] is not null,"A06 first movement gates the next");
            gate.Release=true; Drain(console.Queue);
            check(!console.HasPendingChoice && (id==601)==(d.Monsters[side,1] is null) &&
                d.TryCaptureCardTarget(enemy,id==500?DuelZone.Graveyard:DuelZone.Hand,0,out var moved) &&
                moved.Occurrence==target.Occurrence,"A06 FLIP preserves physical identity and destination");
            check(Run(console,"state").Status==DuelScriptStatus.Completed,"A06 script resumes");
        }

        var optional=DemoDuel.FromPartialObservation(0,[],Slots(Monster(601)),Slots(Monster(24)),8000);
        optional.MonsterFaceDown[0,0]=optional.DefensePosition[0,0]=true;
        var optionalConsole=new DuelConsoleSession(optional);
        check(Run(optionalConsole,"flip 0","select-flip decline").Status==DuelScriptStatus.Completed &&
            optional.Monsters[1,0] is not null,"A06 optional FLIP may choose zero");

        foreach (ushort id in new ushort[]{585,1521})
        foreach (int side in new[]{0,1})
        {
            int enemy=1-side;
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(332),Spell(1056,21)],
                side==0?Slots(Monster(id)):Slots(),side==1?Slots(Monster(id)):Slots(),8000);
            check(d.SetFromHand(0,out _) && d.SetFromHand(0,out _),"A06 pair setup");
            d.NextTurn();
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var console=new DuelConsoleSession(d);
            check(Run(console,"flip 0").Status==DuelScriptStatus.WaitingForDecision &&
                console.Execute("choices").Message.Contains($"{enemy} field: hidden"),"A06 pair lists FIELD separately");
            int count=console.Queue.Trace.Count;
            check(!console.Execute($"select-st-pair {enemy} field {enemy} field").Success &&
                !console.Execute($"select-st-pair {enemy} field").Success &&
                !console.Execute("select-st-pair decline").Success &&
                console.Queue.Trace.Count==count,"A06 pair requires exactly two distinct legal targets");
            check(Run(console,$"select-st-pair {enemy} field {enemy} 0","state").Status==DuelScriptStatus.Completed &&
                !console.HasPendingChoice && d.FieldSpells[enemy] is null && d.Spells[enemy,0] is null &&
                (id==585?d.Graveyard(enemy):d.HandFor(enemy)).Select(c=>c.Id).SequenceEqual(new ushort[]{332,1056}),
                "A06 pair uses queued FIELD and S/T movements");
        }
    }
}
