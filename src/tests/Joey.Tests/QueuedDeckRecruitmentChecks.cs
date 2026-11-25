using Joey.Core;

internal static class QueuedDeckRecruitmentChecks
{
    private static Card Monster(ushort id, byte attribute, int atk=1200, int def=800) =>
        new(id,id,$"Monster {id}","",(3u<<20)|(4u<<25)|((uint)attribute<<29)|((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Recruitment queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not SpecialSummonCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(var (id,attribute) in new (ushort,byte)[]{(1123,2),(1108,5),(1117,1),(1110,4),(1119,3),(1120,6)})
        foreach(int owner in new[]{0,1})
        {
            var recruiter=Monster(id,attribute,1400);
            var strong=Monster(800,1,2200);
            var good=Monster(900,attribute,1500,2000);
            var filler=Monster(901,attribute,1600);
            var wrong=Monster(902,attribute==6?(byte)1:(byte)(attribute+1));
            var spell=new Card(342,342,"Spell","",(22u<<20)|((uint)attribute<<29));
            Card[] deck=[filler,good,wrong,good,spell];
            DemoDuel State()=>DemoDuel.FromPartialObservation(1,[],
                owner==0?Slots(recruiter,strong):Slots(strong),
                owner==1?Slots(recruiter,strong):Slots(strong),8000,
                side0Deck:owner==0?deck:null,side1Deck:owner==1?deck:null,effectShuffleSeed:77);
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(0,0,out _)&&legacy.ResolveCardSelection(3,4,out _),
                "P164 legacy comparator");
            var d=State();
            check(d.BeginBattle(out _)&&d.Attack(0,0,out _),"P164 legacy battle producer");
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            var request=d.PendingCardSelection??throw new InvalidOperationException("Missing P164 request");
            var candidates=d.QueuedDeckRecruitmentCandidates(request);
            check(request.Owner==owner&&candidates.Select(c=>c.SourceIndex).SequenceEqual(new[]{1,3})&&
                candidates[0].Target.Occurrence!=candidates[1].Target.Occurrence,
                "Recruiter filters correct owner, attribute, ATK and distinct physical copies");
            var pick=candidates[1];
            check(!d.TryQueueDeckRecruitment(request,pick.Target,1,out _)&&
                !d.TryQueueDeckRecruitment(request,pick.Target,5,out _)&&
                !d.TryQueueDeckRecruitment(request,null,0,out _)&&
                !d.TryQueueDeckRecruitment(request with {},pick.Target,4,out _)&&
                !d.TryQueueDeckRecruitment(request,pick.Target with {Side=1-owner},4,out _),
                "Recruitment rejects occupied/invalid zone, mismatched decline, stale request and owner");
            int shuffles=0;var summons=new List<DemoSpecialSummon>();
            d.DeckShuffled+=_=>shuffles++;d.MonsterSpecialSummoned+=summons.Add;
            check(d.UsesQueuedFieldSelection(request)&&d.TryCaptureQueuedFieldSelection(request,pick.SourceIndex,out var captured)&&
                captured==pick.Target&&!d.TryCaptureQueuedFieldSelection(request,0,out _),"Field producer captures exact legal copy");
            check(d.TrySubmitQueuedFieldSelection(request,[pick.Target],false,out _,destinationZone:4)&&
                d.PendingCardSelection==request&&d.DeckCount(owner)==5&&d.Monsters[owner,4] is null,
                "Recruitment admission leaves state unchanged");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(ReferenceEquals(d.Monsters[owner,4],pick.Card)&&
                d.DeckCount(owner)==4&&shuffles==0&&summons.Count==1&&q.IsBusy,
                "Special Summon moves exact occurrence once before shuffle gate");
            gate.Release=true;Drain(q);
            check(d.PendingCardSelection is null&&shuffles==1&&summons.Count==1&&
                !d.MonsterFaceDown[owner,4]&&!d.DefensePosition[owner,4]&&d.HandFor(owner).Count==0&&
                d.DeckSnapshot(owner).SequenceEqual(legacy.DeckSnapshot(owner))&&
                d.TryCaptureCardTarget(owner,DuelZone.Monster,4,out var placed)&&placed.Occurrence==pick.Target.Occurrence,
                "P164 face-up attack, no hand move, same seeded remaining deck and occurrence");
            var executed=q.Trace.Where(t=>t.Stage==ActionTraceStage.Executed).Select(t=>t.Entry.Command).ToArray();
            check(executed.Count(c=>c is SpecialSummonCommand)==1&&executed.Count(c=>c is ShuffleDeckCommand)==1&&
                Array.FindIndex(executed,c=>c is SpecialSummonCommand)<Array.FindIndex(executed,c=>c is ShuffleDeckCommand),
                "Exactly one summon then shuffle");

            var decline=State();decline.BeginBattle(out _);decline.Attack(0,0,out _);
            var dq=decline.CreatePrimitiveActionQueue();var pending=decline.PendingCardSelection!;
            var before=decline.DeckSnapshot(owner).ToArray();
            check(decline.TrySubmitQueuedFieldSelection(pending,[],false,out _),"Field producer optional decline admitted");
            Drain(dq);
            check(decline.PendingCardSelection is null&&decline.DeckSnapshot(owner).SequenceEqual(before)&&
                !dq.Trace.Any(t=>t.Entry.Command is SpecialSummonCommand or ShuffleDeckCommand),
                "Decline neither moves nor shuffles");
        }

        var both=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1123,2,1400)),
            Slots(Monster(1108,5,1400)),8000,side0Deck:[Monster(24,2)],side1Deck:[Monster(25,5)]);
        both.BeginBattle(out _);both.Attack(0,0,out _);
        var bq=both.CreatePrimitiveActionQueue();var first=both.PendingCardSelection!;
        check(first.Owner==1&&both.TryQueueDeckRecruitment(first,null,null,out _),"Two recruiters: first decline");
        Drain(bq);
        var second=both.PendingCardSelection!;
        check(second.Owner==0&&both.TryQueueDeckRecruitment(second,
            both.QueuedDeckRecruitmentCandidates(second).Single().Target,0,out _),"Two recruiters: next owner accepts");
        Drain(bq);
        check(both.PendingCardSelection is null&&both.Monsters[0,0]?.Id==24&&
            both.Monsters[1,0] is null&&both.DeckCount(1)==1&&both.DeckCount(0)==0&&!both.GameOver,
            "Recruitment processes both requests and last deck card is not deck-out");

        var stale=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1123,2,1400)),
            Slots(Monster(800,1,2200)),8000,side0Deck:[Monster(24,2)]);
        stale.BeginBattle(out _);stale.Attack(0,0,out _);
        var sq=stale.CreatePrimitiveActionQueue();var requestStale=stale.PendingCardSelection!;
        var target=stale.QueuedDeckRecruitmentCandidates(requestStale).Single().Target;
        check(stale.TryQueueDeckRecruitment(requestStale,target,0,out _),"Recruitment before slot change");
        sq.Tick();
        stale.Monsters[0,0]=Monster(26,2);
        Drain(sq);
        check(stale.Monsters[0,0]?.Id==26&&stale.DeckCount(0)==1&&
            !sq.Trace.Any(t=>t.Entry.Command is ShuffleDeckCommand),
            "Slot filled after admission: reject summon without consuming card or RNG");
    }
}
