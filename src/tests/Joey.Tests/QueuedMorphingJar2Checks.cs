using Joey.Core;

internal static class QueuedMorphingJar2Checks
{
    private static Card Monster(ushort id,int level=3,int category=0) =>
        new(id,id,$"Monster {id}","",(6u<<20)|((uint)level<<25)|((uint)category<<18)|(80u<<9)|70u);
    private static Card Spell(ushort id) => new(id,id,"Spell","",22u<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<500&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Jar #2 queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not ReturnToDeckCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(int seed in new[]{1,29,1979})
        foreach(bool withDeck in new[]{false,true})
        {
            int enemy=1-side;
            var own=Slots(Monster(1163),Monster(1600,7),Monster(0x37,3,1));
            var foes=Slots(Monster(24),Monster(25,6),Monster(706,6,2));
            Card[] deck0=withDeck?[Spell(342),Monster(26),Spell(343),Monster(27,5),Spell(344)]:[];
            Card[] deck1=withDeck?[Spell(345),Monster(28),Spell(346),Monster(29),Spell(347)]:[];
            DemoDuel State()
            {
                var d=DemoDuel.FromPartialObservation(side,[],side==0?own:foes,side==1?own:foes,8000,
                    side0Deck:deck0,side1Deck:deck1,effectShuffleSeed:seed);
                d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
                return d;
            }
            var legacy=State();var ls=new List<int>();legacy.DeckShuffled+=ls.Add;
            check(legacy.ChangePosition(0,out _),"Legacy Jar #2 comparator");
            var d=State();var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            var shuffles=new List<int>();var summons=new List<DemoSpecialSummon>();int draws=0,normal=0;
            d.DeckShuffled+=shuffles.Add;d.MonsterSpecialSummoned+=summons.Add;
            d.HandTransferred+=_=>draws++;d.MonsterNormalSummoned+=_=>normal++;
            check(d.TryQueueChangePosition(0,out _)&&d.MonsterFaceDown[side,0]&&d.LastMorphingJar2.Count==0,
                "Jar #2 admission nonmutating");
            q.Tick();q.Tick();q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.Monsters[0,0] is null&&d.Monsters[0,1] is not null&&shuffles.Count==0&&q.IsBusy,
                "First return waits at presentation gate");
            gate.Release=true;Drain(q);
            check(d.LastMorphingJar2.SequenceEqual(legacy.LastMorphingJar2)&&
                d.LastMorphingJar2[side].ReturnedToDeck==3&&d.LastMorphingJar2[enemy].ReturnedToDeck==2&&
                d.FusionDeck(enemy).Single().Id==706&&shuffles.SequenceEqual(ls)&&shuffles.SequenceEqual([0,1])&&
                d.Life.SequenceEqual(legacy.Life)&&!d.GameOver&&draws==0&&normal==0&&
                summons.Count==d.LastMorphingJar2.Sum(r=>r.SpecialSummoned)&&summons.All(s=>s.FaceDownDefense),
                "Jar #2 quota excludes Fusion, no draw, deterministic shuffle and face-down summons");
            for(int p=0;p<2;p++)
            {
                check(d.DeckSnapshot(p).SequenceEqual(legacy.DeckSnapshot(p))&&
                    d.Graveyard(p).SequenceEqual(legacy.Graveyard(p))&&
                    d.FusionDeck(p).SequenceEqual(legacy.FusionDeck(p))&&
                    Enumerable.Range(0,5).All(z=>d.Monsters[p,z]==legacy.Monsters[p,z]&&
                        d.MonsterFaceDown[p,z]==legacy.MonsterFaceDown[p,z]&&
                        d.DefensePosition[p,z]==legacy.DefensePosition[p,z]),
                    "Jar #2 final board and ordered zones match legacy");
            }
            var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(executed.Count(c=>c is ReturnToDeckCommand)==6&&
                executed.Count(c=>c is ShuffleDeckCommand)==2&&
                executed.Count(c=>c is SpecialSummonCommand or SendToGraveyardCommand)==d.LastMorphingJar2.Sum(r=>r.Excavated)&&
                Array.FindLastIndex(executed,c=>c is ShuffleDeckCommand)<Array.FindIndex(executed,c=>c is SpecialSummonCommand or SendToGraveyardCommand),
                "Jar #2 individual returns and excavation movements after shuffles");
        }

        var solo=DemoDuel.FromPartialObservation(0,[],Slots(Monster(1163)),Slots(Monster(706,6,2)),8000,
            side1Deck:[Monster(24),Spell(342)]);
        solo.MonsterFaceDown[0,0]=solo.DefensePosition[0,0]=true;
        var sq=solo.CreatePrimitiveActionQueue();
        solo.TryCaptureCardTarget(0,DuelZone.Monster,0,out var source);
        check(solo.TryQueueChangePosition(0,out _),"Jar #2 self-return admitted");Drain(sq);
        check(solo.TryCaptureCardTarget(0,DuelZone.Monster,0,out var back)&&back==source&&
            solo.MonsterFaceDown[0,0]&&solo.DefensePosition[0,0]&&!solo.MonsterWasFlipped[0,0]&&
            solo.LastMorphingJar2[1] is {ReturnedToDeck:0,Excavated:0}&&
            solo.FusionDeck(1).Single().Id==706&&solo.DeckCount(1)==2&&
            sq.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Command is ShuffleDeckCommand)==1&&
            !solo.TryQueueChangePosition(0,out _),"Self returns same occurrence face-down, Fusion-only side does not shuffle");

        var equipped=DemoDuel.FromPartialObservation(0,[Spell(300)],Slots(Monster(1163),
            new Card(24,24,"Warrior","",(15u<<20)|(4u<<25)|(100u<<9)|80u)),Slots(),8000,equipmentContextResolved:true);
        check(equipped.Equip(0,0,1,out _),"Jar #2 linked equipment fixture");
        equipped.MonsterFaceDown[0,0]=equipped.DefensePosition[0,0]=true;
        var eq=equipped.CreatePrimitiveActionQueue();check(equipped.TryQueueChangePosition(0,out _),"Jar #2 with equipment admitted");Drain(eq);
        check(equipped.EquipmentCards.Count==0&&equipped.Graveyard(0).Single().Id==300&&
            eq.Trace.Count(t=>t.Stage==ActionTraceStage.Executed&&t.Entry.Command is DestroyCommand)==1&&
            equipped.LastMorphingJar2[0].SpecialSummoned==2,"Returning monster separately destroys linked equip");

        var suppressed=DemoDuel.FromPartialObservation(0,[],Slots(Monster(1163)),Slots(Monster(24)),8000);
        suppressed.MonsterFaceDown[0,0]=suppressed.DefensePosition[0,0]=true;
        suppressed.Spells[1,0]=new Card(1530,1530,"Royal Command","",21u<<20);
        var nq=suppressed.CreatePrimitiveActionQueue();suppressed.TryQueueChangePosition(0,out _);Drain(nq);
        check(suppressed.LastMorphingJar2.Count==0&&suppressed.Monsters[0,0]?.Id==1163&&
            suppressed.Monsters[1,0]?.Id==24&&!nq.Trace.Any(t=>t.Entry.Command is ReturnToDeckCommand),
            "Royal Command prevents Jar #2 effect");
    }
}
