using Joey.Core;

internal static class QueuedMonsterWipeChecks
{
    private static Card Monster(int index, int type, ushort id = 24) =>
        new(index, id, "Monster", "", ((uint)type << 20) | (4u << 25) | (100u << 9));
    private static Card Spell(ushort id) => new(id,id,"Wipe","",22u << 20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for (int i=0;i<100 && q.IsBusy;i++) q.Tick();
        if(q.IsBusy) throw new InvalidOperationException("Wipe queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release {get;set;} = true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result) =>
            command.Command is not DestroyCommand || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach (var (id,type) in new (ushort,int)[] {
            (335,0),(336,0),(652,15),(659,7),(661,10),(662,6),(663,8),(1036,18),(1037,3)})
        foreach (int side in new[] {0,1})
        foreach (bool set in new[] {false,true})
        {
            var matching=Monster(24,type==0?15:type);
            var other=Monster(25,type==15?7:15);
            var d=DemoDuel.FromPartialObservation(side,[Spell(id)],Slots(matching,matching,other),
                Slots(matching,matching,other),8000);
            d.MonsterFaceDown[0,1]=d.MonsterFaceDown[1,1]=true;
            d.DefensePosition[1,0]=true;
            var expected=MonsterWipeRules.Targets(id,side,d.Monsters,d.MonsterFaceDown);
            var gate=new Gate();
            var q=d.CreatePrimitiveActionQueue(gate);
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Wipe Set admission");Drain(q);}
            var events=new List<DemoMonsterDestruction>();
            d.MonsterDestroyed+=events.Add;
            int before=q.Trace.Count;
            check(d.TryQueueMonsterWipe(set?DuelZone.SpellTrap:DuelZone.Hand,0,out _) &&
                events.Count==0 && d.Graveyard(side).Count==0,"Wipe admission is read-only");
            q.Tick(); q.Tick(); // activation then source spend
            check(d.Graveyard(side).Single().Id==id && events.Count==0,"Wipe spends source before destruction");
            gate.Release=false; q.Tick(); q.Tick();
            check(events.Count==1 && d.LastMonsterWipe is null && q.IsBusy,
                "Destruction gate holds each applied movement once");
            gate.Release=true; Drain(q);
            var result=d.LastMonsterWipe;
            check(result is not null && result.EffectResourceId==id && result.Caster==side &&
                result.TotalDestroyed==expected.Count &&
                result.DestroyedPlayer0==expected.Count(t=>t.Player==0) &&
                result.DestroyedPlayer1==expected.Count(t=>t.Player==1) &&
                result.DestroyedFaceDown==expected.Count(t=>t.FaceDown) &&
                events.Count==expected.Count &&
                expected.All(t=>d.Monsters[t.Player,t.Zone] is null) &&
                d.Hand.Count==0 && d.Spells[side,0] is null &&
                q.Trace.Skip(before).Count(x=>x.Stage==ActionTraceStage.Executed &&
                    x.Entry.Command is DestroyCommand)==expected.Count,
                $"Wipe {id}: exact scope, count, identity and one destroy per target");
            for(int p=0;p<2;p++)
            for(int z=0;z<3;z++)
                check((d.Monsters[p,z] is null)==expected.Any(t=>t.Player==p&&t.Zone==z),
                    $"Wipe {id}: untouched hidden/type/own slots remain");
        }
        var sangan=Monster(47,3,47);
        var witch=Monster(573,18,573);
        var normal=Monster(24,15);
        var trigger=DemoDuel.FromPartialObservation(0,[Spell(335)],Slots(sangan),Slots(witch),
            8000,side0Deck:[normal],side1Deck:[normal]);
        var tq=trigger.CreatePrimitiveActionQueue();
        check(trigger.TryQueueMonsterWipe(DuelZone.Hand,0,out _),"Dark Hole trigger batch admitted");
        Drain(tq);
        check(trigger.LastMonsterWipe is {TotalDestroyed:2} &&
            trigger.PendingCardSelection is {Owner:0,EffectResourceId:47} &&
            trigger.ResolveCardSelection(0,out _) &&
            trigger.PendingCardSelection is {Owner:1,EffectResourceId:573},
            "Dark Hole opens Sangan then Witch only after all destruction");

        var stale=DemoDuel.FromPartialObservation(0,[Spell(335)],Slots(normal),Slots(normal),8000);
        var sq=stale.CreatePrimitiveActionQueue();
        stale.TryQueueSpellTrapSet(0,out _);Drain(sq);
        check(stale.TryQueueMonsterWipe(DuelZone.SpellTrap,0,out _),"Stale source admitted");
        sq.Tick();
        stale.Spells[0,0]=Spell(335);
        Drain(sq);
        check(stale.Monsters[0,0] is not null && stale.Monsters[1,0] is not null &&
            stale.LastMonsterWipe is null && stale.Graveyard(0).Count==0,
            "Failed source spend cancels entire reserved wipe");

        var empty=DemoDuel.FromPartialObservation(0,[Spell(652)],Slots(normal),Slots(),8000);
        empty.MonsterFaceDown[0,0]=true;
        var eq=empty.CreatePrimitiveActionQueue();
        check(!empty.TryQueueMonsterWipe(DuelZone.Hand,0,out _) && !eq.IsBusy &&
            empty.Hand.Count==1,"Type wipe cannot target a hidden-only match");

        var linked=DemoDuel.FromPartialObservation(0,[Spell(300),Spell(335)],Slots(normal),Slots(normal),
            8000,equipmentContextResolved:true);
        var lq=linked.CreatePrimitiveActionQueue();
        check(linked.TryQueueEquipment(DuelZone.Hand,0,1,0,out _),"Wipe linked equipment setup");
        Drain(lq);
        check(linked.TryQueueMonsterWipe(DuelZone.Hand,0,out _),"Wipe with linked equipment admitted");
        Drain(lq);
        check(linked.EquipmentCards.Count==0 && linked.Spells[0,0] is null &&
            linked.Graveyard(0).Any(c=>c.Id==300) && linked.LastMonsterWipe is {TotalDestroyed:2},
            "Wipe removes linked equipment via separate command");
    }
}
