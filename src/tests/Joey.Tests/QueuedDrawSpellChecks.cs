using Joey.Core;

internal static class QueuedDrawSpellChecks
{
    private static Card Spell(ushort id) => new(id, id, "Draw spell", "", 22u << 20);
    private static Card Fixture(int index) => new(index, (ushort)index, $"Fixture {index}", "", 22u << 20);
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; } = true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    private static void Drain(ActionQueue q)
    {
        for (int i=0; i<100 && q.IsBusy; i++) q.Tick();
        if (q.IsBusy) throw new InvalidOperationException("Draw spell queue did not drain.");
    }

    private static DemoDuel Duel(int side, IReadOnlyList<Card> hand, params Card[] deck) =>
        DemoDuel.FromPartialObservation(side, hand, new Card?[5], new Card?[5], 8000,
            side0Deck: side == 0 ? deck : null, side1Deck: side == 1 ? deck : null);

    public static void Run(Action<bool,string> check)
    {
        var first = Fixture(2000); var second = Fixture(2001);
        foreach (int side in new[] {0,1})
        foreach (bool set in new[] {false,true})
        foreach (ushort id in new ushort[] {1010,1065})
        {
            var spell = Spell(id);
            int count = id == 1010 ? 2 : 1;
            var d = Duel(side,[spell], count == 2 ? [first,second] : [first]);
            var gate = new Gate();
            var q = d.CreatePrimitiveActionQueue(gate);
            if (set) { check(d.TryQueueSpellTrapSet(0,out _),"Draw spell Set admitted"); Drain(q); }
            var transfers = new List<DemoHandTransfer>();
            d.HandTransferred += transfers.Add;
            int before = q.Trace.Count;
            gate.Release = false;
            check(d.TryQueueDrawSpell(set ? DuelZone.SpellTrap : DuelZone.Hand,0,out _) &&
                d.DeckCount(side)==count && d.Graveyard(side).Count==0,
                $"Draw {id}: admission does not spend or draw");
            q.Tick(); q.Tick();
            check(d.Graveyard(side).Count==0 && d.DeckCount(side)==count,
                "Activation waits at presentation gate");
            gate.Release=true; Drain(q);
            var executed=q.Trace.Skip(before).Where(x=>x.Stage==ActionTraceStage.Executed).ToArray();
            check(d.DeckCount(side)==0 && d.Hand.SequenceEqual(count==2 ? [first,second] : new[] {first}) &&
                d.Graveyard(side).Single().Id==id && d.Spells[side,0] is null &&
                d.Life[side]==8000 && d.Life[1-side]==(id==1065?9000:8000) &&
                !d.GameOver && d.CanDraw && transfers.Count==count &&
                executed[1].Entry.Command is SendToGraveyardCommand &&
                executed.Skip(2).Take(count).All(x=>x.Entry.Command is DrawCommand draw && draw.Side==side) &&
                executed.Length==4 && executed.All(x=>x.Result?.Outcome==CommandOutcome.Applied),
                $"Draw {id}: ordered moves, LP, separate draws and last-card legality");
            check(id!=1065 || executed[^1].Entry.Command is HealCommand heal && heal.Side==1-side && heal.Amount==1000,
                "Upstart heals opponent after draw");
            q.Enqueue(new DrawCommand(side)); Drain(q);
            check(d.Winner==1-side,"Only a subsequent draw from empty deck loses");
        }

        foreach (bool set in new[] {false,true})
        {
            var insufficient=Duel(0,[Spell(1010)],first);
            var q=insufficient.CreatePrimitiveActionQueue();
            if(set){insufficient.TryQueueSpellTrapSet(0,out _); Drain(q);}
            int before=q.Trace.Count;
            check(!insufficient.TryQueueDrawSpell(set?DuelZone.SpellTrap:DuelZone.Hand,0,out _) &&
                q.Trace.Count==before && insufficient.Graveyard(0).Count==0 && insufficient.DeckCount(0)==1,
                "Pot requires two cards before admission");
        }
        var stale=Duel(0,[Spell(1010)],first,second);
        var sq=stale.CreatePrimitiveActionQueue();
        stale.TryQueueSpellTrapSet(0,out _); Drain(sq);
        check(stale.TryQueueDrawSpell(DuelZone.SpellTrap,0,out _),"Stale draw source initially admitted");
        sq.Tick(); // producer completed; spend is queued, but has not run.
        stale.Spells[0,0]=Spell(1010);
        Drain(sq);
        check(stale.Hand.Count==0 && stale.DeckCount(0)==2 && stale.Graveyard(0).Count==0,
            "Stale spend does not grant free draws");

        // Preserve the current legacy fixed-draw batch semantics if Exodia
        // completes during the first draw. This is not a native timing claim.
        foreach (ushort id in new ushort[] {1010,1065})
        {
            var pieces = ExodiaRules.RequiredNames.Select((name,i)=>
                new Card(100+i,(ushort)(100+i),name,"",1u<<20)).ToArray();
            var hand=pieces.Take(4).Append(Spell(id)).ToArray();
            var d=Duel(0,hand,id==1010 ? [pieces[4],second] : [pieces[4]]);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueDrawSpell(DuelZone.Hand,4,out _),"Exodia draw spell admitted");
            Drain(q);
            check(d.Winner==0 && d.DeckCount(0)==0 && d.Hand.Count==(id==1010?6:5) &&
                d.Life[1]==(id==1065?9000:8000),
                "Fixed draw spell completes its reserved legacy batch after Exodia");
        }
    }
}
