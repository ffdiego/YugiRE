using Joey.Core;

internal static class QueuedFlipBanishBurnChecks
{
    private static Card Monster(ushort id) => new(id,id,"Monster","",(10u<<20)|(4u<<25));
    private static Card Spell(ushort id) => new(id,id,"Spell","",22u<<20);
    private static Card?[] Slots(Card? card=null) => [card,null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("FLIP banish batch did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not BanishCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort effect in new ushort[]{1435,1436})
        foreach(int count in new[]{0,1,2,3,4,5})
        foreach(bool lethal in new[]{false,true})
        {
            bool deck=effect==1435;
            int required=deck?3:2, enemy=1-side;
            Card[] pool=[Spell(342),Spell(342),Monster(24),Spell(343),Monster(25)];
            var selected=pool.Take(count).ToArray();
            Card[] ownDeck=deck?selected:[Monster(29),Monster(30),Monster(31),Monster(32),Monster(33)];
            Card[] hand=deck?[Spell(344)]:selected;
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,hand,side==0?Slots(Monster(effect)):Slots(),
                    side==1?Slots(Monster(effect)):Slots(),8000,
                    side0Deck:side==0?ownDeck:[Monster(28)],
                    side1Deck:side==1?ownDeck:[Monster(28)],effectShuffleSeed:1122);
                d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
                if(lethal)d.Life[enemy]=750;
                return d;
            }
            var legacy=State();check(legacy.ChangePosition(0,out _),"Legacy P051/P052 comparator");
            var d=State();var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            var original=new List<CardTarget>();
            for(int i=0;i<count;i++)
            {
                if(!d.TryCaptureCardTarget(side,deck?DuelZone.Deck:DuelZone.Hand,i,out var t))
                    throw new InvalidOperationException("Missing cost occurrence.");
                original.Add(t);
            }
            int transfers=0;d.HandTransferred+=_=>transfers++;
            check(d.TryQueueChangePosition(0,out _)&&d.MonsterFaceDown[side,0]&&
                d.Banished(side).Count==0&&d.LastFlipBanishBurn is null,"Admission has no effect");
            q.Tick();q.Tick();q.Tick();
            gate.Release=false;q.Tick();q.Tick();
            bool paid=count>=required;
            if(paid)
                check(d.Banished(side).Count==1&&d.Life[enemy]==(lethal?750:8000)&&
                    d.LastFlipBanishBurn is null&&q.IsBusy,"First banishment awaits gate before damage");
            gate.Release=true;Drain(q);
            var result=d.LastFlipBanishBurn??throw new InvalidOperationException("Missing banish audit.");
            var expected=legacy.LastFlipBanishBurn!;
            check(result.Applied==paid&&result.Damage==expected.Damage&&result.CostSource==expected.CostSource&&
                result.RequiredCount==required&&result.BanishedCards.SequenceEqual(expected.BanishedCards)&&
                d.Banished(side).SequenceEqual(legacy.Banished(side))&&
                d.HandFor(side).SequenceEqual(legacy.HandFor(side))&&
                d.DeckSnapshot(side).SequenceEqual(legacy.DeckSnapshot(side))&&
                d.Life.SequenceEqual(legacy.Life)&&d.Winner==legacy.Winner&&
                d.Graveyard(side).Count==0&&d.Banished(enemy).Count==0&&transfers==0,
                "Seeded selection, audit, exact LP and destinations match legacy");
            var commands=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(commands.Count(c=>c is BanishCommand)==(paid?required:0)&&
                commands.Count(c=>c is DamageCommand)==(paid?1:0)&&
                (!paid||Array.FindLastIndex(commands,c=>c is BanishCommand)<Array.FindIndex(commands,c=>c is DamageCommand)),
                "One banish per copy, damage only after all");
            for(int i=0;i<d.Banished(side).Count;i++)
            {
                var banish=commands.OfType<BanishCommand>().ElementAt(i);
                check(d.TryCaptureCardTarget(side,DuelZone.Banished,i,out var actual)&&
                    actual.Occurrence==banish.Target.Occurrence&&original.Contains(banish.Target),
                    "Physical occurrence preserved in Banished");
            }
            if(!d.GameOver)
            {
                q.Enqueue(new ShuffleDeckCommand(side));Drain(q);
                // Legacy equivalent seeded shuffle on a separate queue. This
                // also tests no RNG consumption on unpaid Fire Sorcerer.
                var lq=legacy.CreatePrimitiveActionQueue();
                lq.Enqueue(new ShuffleDeckCommand(side));Drain(lq);
                check(d.DeckSnapshot(side).SequenceEqual(legacy.DeckSnapshot(side)),"RNG state matches legacy");
            }
        }

        var suppressed=DemoDuel.FromPartialObservation(0,[Spell(342),Spell(343)],
            Slots(Monster(1436)),Slots(),8000);
        suppressed.MonsterFaceDown[0,0]=suppressed.DefensePosition[0,0]=true;
        suppressed.Spells[1,0]=new Card(1530,1530,"Royal Command","",21u<<20);
        var sq=suppressed.CreatePrimitiveActionQueue();suppressed.TryQueueChangePosition(0,out _);Drain(sq);
        check(suppressed.LastFlipBanishBurn is null&&suppressed.Hand.Count==2&&
            suppressed.Banished(0).Count==0&&!sq.Trace.Any(t=>t.Entry.Command is BanishCommand or DamageCommand),
            "Royal Command prevents cost and damage");

        var primitive=DemoDuel.FromPartialObservation(0,[Spell(342),Spell(342)],Slots(),Slots(),8000,
            side0Deck:[Monster(24),Monster(24)]);
        var pq=primitive.CreatePrimitiveActionQueue();
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,0,out var top);
        primitive.TryCaptureCardTarget(0,DuelZone.Deck,1,out var below);
        primitive.TryCaptureCardTarget(0,DuelZone.Hand,1,out var second);
        pq.EnqueueBatch([new BanishCommand(0,below),new BanishCommand(0,top),
            new BanishCommand(0,top),new BanishCommand(0,second),new BanishCommand(0,below)]);
        Drain(pq);
        check(primitive.Banished(0).Count==3&&primitive.Hand.Count==1&&primitive.DeckCount(0)==0&&
            primitive.Graveyard(0).Count==0&&!primitive.GameOver&&
            pq.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Result?.Outcome==CommandOutcome.Skipped)==2,
            "Primitive skips non-top and obsolete targets, no deck-out");
    }
}
