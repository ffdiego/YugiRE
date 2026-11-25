using Joey.Core;

internal static class QueuedFlipFieldChecks
{
    private static Card Monster(ushort id,int level=4) =>
        new(id,id,"Monster","",(15u<<20)|((uint)level<<25)|(100u<<9)|80u);
    private static Card Spell(ushort id,int type=22) => new(id,id,"S/T","",(uint)type<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("FLIP field queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>
            c.Command is not (DestroyCommand or ReturnToHandCommand) || Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort id in new ushort[]{585,1521})
        {
            bool bounce=id==1521;int enemy=1-side;
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(332),Spell(1056,21),Spell(300)],
                side==0?Slots(Monster(id),Monster(24)):Slots(Monster(25)),
                side==1?Slots(Monster(id),Monster(24)):Slots(Monster(25)),8000,equipmentContextResolved:true);
            check(d.SetFromHand(0,out _)&&d.SetFromHand(0,out _)&&d.Equip(0,side,1,out _),
                "FLIP pair tracked FIELD, Set trap and equip");
            d.NextTurn();
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueChangePosition(0,out _),"Pair FLIP admitted");
            Drain(q);
            var request=d.PendingFlipSpellTrapTargets??throw new InvalidOperationException("Missing FLIP pair.");
            var options=d.QueuedFlipSpellTrapCandidates(request);
            check(options.Count==(bounce?3:2)&&options.Any(c=>c.Target.Zone==DuelZone.FieldSpell)&&
                options.All(c=>c.FaceDown||bounce),"Pair filters and FIELD");
            var field=options.Single(c=>c.Target.Zone==DuelZone.FieldSpell);
            var other=options.Single(c=>c.Card.Id==(bounce?300:1056));
            check(!d.TryQueueFlipSpellTrapPair(request,[field.Target],out _)&&
                !d.TryQueueFlipSpellTrapPair(request,[field.Target,field.Target],out _)&&
                !d.TryQueueFlipSpellTrapPair(request with {},[field.Target,other.Target],out _),
                "Mandatory distinct pair and exact request");
            check(d.UsesQueuedFlipSpellTrapChoice(request)&&
                !d.TrySubmitQueuedFlipSpellTrapChoice(request with {},[field.Target,other.Target],out _),
                "Pair producer requires exact request");
            check(d.TrySubmitQueuedFlipSpellTrapChoice(request,[field.Target,other.Target],out _)&&
                d.PendingFlipSpellTrapTargets==request&&d.FieldSpells[enemy] is not null,"Admission nonmutating");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(d.FieldSpells[enemy] is null&&d.Spells[enemy,bounce?1:0] is not null&&q.IsBusy,
                "First movement waits for its gate");
            gate.Release=true;Drain(q);
            check(d.PendingFlipSpellTrapTargets is null&&
                (bounce?d.HandFor(enemy).Select(c=>c.Id).SequenceEqual(new ushort[]{332,300}):
                    d.Graveyard(enemy).Select(c=>c.Id).SequenceEqual(new ushort[]{332,1056}))&&
                d.EquipmentCards.Count==(bounce?0:1)&&d.AttackValue(side,1)==(bounce?1000:1300),
                "Pair correct destinations, equip cleanup and untouched cards");
        }

        foreach(int side in new[]{0,1})
        {
            int enemy=1-side;
            var own=Slots(Monster(1338),Monster(24));
            var foes=Slots(Monster(47),Monster(25),Monster(26,5),Monster(27));
            var d=DemoDuel.FromPartialObservation(side,[],side==0?own:foes,side==1?own:foes,8000,
                side0Deck:enemy==0?[Monster(28)]:null,side1Deck:enemy==1?[Monster(28)]:null);
            d.MonsterFaceDown[side,0]=d.DefensePosition[side,0]=true;
            d.MonsterFaceDown[enemy,1]=d.DefensePosition[enemy,1]=true;
            d.DefensePosition[enemy,3]=true;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueChangePosition(0,out _),"Ladybug admitted");
            q.Tick();q.Tick();q.Tick();gate.Release=false;q.Tick();
            check(d.Graveyard(enemy).Single().Id==47&&d.PendingCardSelection is null&&
                d.Monsters[enemy,3] is not null&&q.IsBusy,"Ladybug first destruction gates later search");
            gate.Release=true;Drain(q);
            check(d.LastMonsterWipe is {EffectResourceId:1338,TotalDestroyed:2,DestroyedFaceDown:0}&&
                d.Monsters[enemy,0] is null&&d.Monsters[enemy,3] is null&&
                d.Monsters[enemy,1]?.Id==25&&d.Monsters[enemy,2]?.Id==26&&
                d.Monsters[side,1]?.Id==24&&d.PendingCardSelection is {EffectResourceId:47},
                "Ladybug destroys only opposing face-up level four, then opens Sangan");
        }
    }
}
