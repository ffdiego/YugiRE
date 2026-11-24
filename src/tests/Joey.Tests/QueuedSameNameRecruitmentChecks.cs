using Joey.Core;

internal static class QueuedSameNameRecruitmentChecks
{
    private static Card Monster(ushort id, int atk=1000) =>
        new(id,id,$"Monster {id}","",(3u<<20)|(4u<<25)|((uint)(atk/10)<<9)|80u);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Same-name recruitment did not drain.");
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
        foreach(ushort id in new ushort[]{1115,1114})
        foreach(int owner in new[]{0,1})
        foreach(int count in new[]{0,1,2})
        {
            var recruit=Monster(id);
            var strong=Monster(800,2200);
            Card[] deck=[recruit,Monster(24),recruit,Monster(25)];
            DemoDuel State()=>DemoDuel.FromPartialObservation(1,[],
                owner==0?Slots(recruit):Slots(strong),owner==1?Slots(recruit):Slots(strong),8000,
                side0Deck:owner==0?deck:null,side1Deck:owner==1?deck:null,effectShuffleSeed:71);
            var legacy=State();
            check(legacy.BeginBattle(out _)&&legacy.Attack(0,0,out _),"P168 legacy battle");
            if(count==0)check(legacy.SkipOptionalCardSelection(out _),"P168 legacy decline");
            else
            {
                check(legacy.SelectBatchCard(2,out _),"P168 legacy first copy");
                check(count==1?legacy.FinishOptionalCardSelection(out _):legacy.SelectBatchCard(0,out _),
                    "P168 legacy complete selection");
            }
            var d=State();
            check(d.BeginBattle(out _)&&d.Attack(0,0,out _),"P168 battle producer");
            var lp=d.Life.ToArray();
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            var request=d.PendingCardSelection??throw new InvalidOperationException("Missing P168 request.");
            var candidates=d.QueuedDeckRecruitmentCandidates(request);
            check(request.Owner==owner&&request.MaxSelections==2&&
                candidates.Select(c=>c.SourceIndex).SequenceEqual(new[]{0,2})&&
                candidates[0].Target.Occurrence!=candidates[1].Target.Occurrence,
                "P168 only same-name physical copies");
            var picks=candidates.Reverse().Take(count).Select(c=>c.Target).ToArray();
            check(!d.TryQueueSameNameRecruitment(request,[candidates[0].Target,candidates[0].Target],out _)&&
                !d.TryQueueSameNameRecruitment(request with {},picks,out _)&&
                !d.TryQueueDeckRecruitment(request,candidates[0].Target,0,out _),
                "Duplicate, stale and wrong response API reject");
            int shuffles=0;var summons=new List<DemoSpecialSummon>();
            d.DeckShuffled+=_=>shuffles++;d.MonsterSpecialSummoned+=summons.Add;
            check(d.TryQueueSameNameRecruitment(request,picks,out _)&&d.DeckCount(owner)==4&&
                d.PendingCardSelection==request,"P168 admission nonmutating");
            q.Tick();
            if(count>0)
            {
                gate.Release=false;q.Tick();q.Tick();
                check(summons.Count==1&&d.DeckCount(owner)==3&&shuffles==0&&q.IsBusy&&
                    ReferenceEquals(d.Monsters[owner,0],candidates[1].Card),
                    "P168 first movement waits for its own gate");
                gate.Release=true;
            }
            Drain(q);
            bool set=id==1115;
            check(d.PendingCardSelection is null&&d.DeckCount(owner)==4-count&&
                summons.Count==count&&shuffles==(count==0?0:1)&&
                d.Life.SequenceEqual(lp)&&d.Life.SequenceEqual(legacy.Life)&&
                d.DeckSnapshot(owner).SequenceEqual(legacy.DeckSnapshot(owner)),
                "P168 exact count and one shuffle only after success, battle LP not duplicated");
            for(int i=0;i<count;i++)
                check(d.MonsterFaceDown[owner,i]==set&&d.DefensePosition[owner,i]==set&&
                    d.TryCaptureCardTarget(owner,DuelZone.Monster,i,out var placed)&&placed.Occurrence==picks[i].Occurrence,
                    "P168 correct position and physical copy");
        }

        foreach(int owner in new[]{0,1})
        foreach(bool decline in new[]{false,true})
        {
            var vermin=Monster(1307);
            var d=DemoDuel.FromPartialObservation(owner,[],owner==0?Slots(vermin):Slots(),
                owner==1?Slots(vermin):Slots(),8000,
                side0Deck:owner==0?[vermin,Monster(24),vermin]:null,
                side1Deck:owner==1?[vermin,Monster(24),vermin]:null);
            d.MonsterFaceDown[owner,0]=d.DefensePosition[owner,0]=true;
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueChangePosition(0,out _),"Bubonic manual FLIP admitted");Drain(q);
            var request=d.PendingCardSelection!;
            var candidates=d.QueuedDeckRecruitmentCandidates(request);
            check(candidates.Count==2&&request.MaxSelections==1&&
                !d.TryQueueSameNameRecruitment(request,candidates.Select(c=>c.Target).ToArray(),out _)&&
                !d.TryQueueSameNameRecruitment(request,[candidates[1].Target],out _,singleSlot:0),
                "Bubonic maximum one and free slot required");
            check(d.TryQueueSameNameRecruitment(request,decline?[]:[candidates[1].Target],out _,
                singleSlot:decline?null:4),"Bubonic answer admitted");
            Drain(q);
            check(d.PendingCardSelection is null&&d.DeckCount(owner)==(decline?3:2)&&
                (decline?d.Monsters[owner,4] is null:
                    ReferenceEquals(d.Monsters[owner,4],candidates[1].Card)&&
                    d.MonsterFaceDown[owner,4]&&d.DefensePosition[owner,4])&&
                q.Trace.Count(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is ShuffleDeckCommand)==(decline?0:1),
                "Bubonic selected copy face-down or decline without shuffle");
        }

        var suppressed=DemoDuel.FromPartialObservation(0,[],Slots(Monster(1307)),Slots(),8000,
            side0Deck:[Monster(1307)]);
        suppressed.MonsterFaceDown[0,0]=suppressed.DefensePosition[0,0]=true;
        var sq=suppressed.CreatePrimitiveActionQueue();suppressed.TryQueueChangePosition(0,out _);Drain(sq);
        var pending=suppressed.PendingCardSelection!;
        var target=suppressed.QueuedDeckRecruitmentCandidates(pending).Single().Target;
        check(suppressed.TryQueueSameNameRecruitment(pending,[target],out _),"Bubonic answer before suppression");
        suppressed.Spells[1,0]=new Card(1500,FlipEffectSuppressionRules.RoyalCommand,"Royal Command","",21u<<20);
        Drain(sq);
        check(suppressed.PendingCardSelection is null&&suppressed.DeckCount(0)==1&&
            !sq.Trace.Any(x=>x.Entry.Command is SpecialSummonCommand or ShuffleDeckCommand),
            "Royal Command suppresses pending Bubonic without shuffle");

        var attacked=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1307)),Slots(Monster(800,2200)),8000,
            side0Deck:[Monster(1307)]);
        attacked.MonsterFaceDown[0,0]=attacked.DefensePosition[0,0]=true;
        check(attacked.BeginBattle(out _)&&attacked.Attack(0,0,out _)&&
            attacked.PendingCardSelection is {EffectResourceId:1307},"Bubonic battle fixture");
        var aq=attacked.CreatePrimitiveActionQueue();
        check(attacked.QueuedDeckRecruitmentCandidates(attacked.PendingCardSelection!).Count==0&&
            !attacked.TryQueueSameNameRecruitment(attacked.PendingCardSelection!,[],out _)&&!aq.IsBusy,
            "Battle FLIP remains legacy until Damage Step migration");
    }
}
