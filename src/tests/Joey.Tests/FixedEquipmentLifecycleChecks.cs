using Joey.Core;

internal static class FixedEquipmentLifecycleChecks
{
    private static Card Monster(int index,int type,int attr,int atk=1000,int def=1000,int level=4) =>
        new(index,77,$"Monster {index}","",((uint)type<<20)|((uint)attr<<29)|((uint)level<<25)|
            ((uint)(atk/10)<<9)|(uint)(def/10));
    private static Card Spell(ushort id,int cls=22) => new(id,id,"Equip","",(uint)cls<<20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Fixed equipment queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult r)=>c.Command is not EquipCommand||Release;
    }

    public static void Run(Action<bool,string> check)
    {
        foreach(var (id,type,attr,atk,def) in new (ushort,int,int,int,int)[]
        {
            (300,15,0,300,300),(301,0,2,400,-200),(307,11,0,300,300),
            (302,3,0,300,300),(304,10,0,300,300),(306,0,1,400,-200),
            (308,0,3,400,-200),(309,13,0,300,300),(311,17,0,300,300),
            (314,1,0,300,300),(315,19,0,300,300),(318,12,0,300,300),
            (321,2,0,300,300),(322,18,0,300,300),(324,7,0,300,300),
            (325,9,0,300,300),(326,16,0,300,300),(327,14,0,300,300),
            (653,0,4,700,0),(1012,0,4,400,-200),(1013,0,6,400,-200)
        })
        foreach(int owner in new[]{0,1})
        foreach(int side in new[]{0,1})
        foreach(bool set in new[]{false,true})
        foreach(int removal in new[]{0,1,2,3})
        {
            int validType=type==0?15:type, validAttr=attr==0?2:attr;
            var valid=Monster(42,validType,validAttr);
            var wrong=Monster(43,type==0?validType:type==1?2:1,attr==0?validAttr:attr==1?2:1);
            var hidden=Monster(44,validType,validAttr);
            // Use a deliberately ineligible attacker so the projection contains
            // exactly one face-up target; its high ATK guarantees destruction.
            var attacker=Monster(45,type==0?validType:type==1?2:1,
                attr==0?validAttr:attr==1?2:1,4000);
            var targets=Slots(valid,wrong,hidden);
            var foes=Slots(attacker);
            var d=DemoDuel.FromPartialObservation(owner,[Spell(id)],
                side==0?targets:foes,side==1?targets:foes,8000,equipmentContextResolved:true);
            d.MonsterFaceDown[side,2]=d.DefensePosition[side,2]=true;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Fixed equip set");Drain(q);}
            var zone=set?DuelZone.SpellTrap:DuelZone.Hand;
            check(d.TryCaptureCardTarget(owner,zone,0,out var source),"Capture exact equip source");
            var choices=d.QueuedEquipmentCandidates(source);
            check(choices.Count==1&&choices[0].Target.Side==side&&choices[0].Slot==0,
                $"Equip {id}: target filter by type/attribute and face-up state");
            var pick=choices.Single().Target;
            d.TryCaptureCardTarget(side,DuelZone.Monster,1,out var invalid);
            check(!d.TryQueueEquipment(source,invalid,out _)&&d.EquipmentCards.Count==0,
                $"Equip {id}: wrong target has no side effects");
            gate.Release=false;
            check(d.TryQueueEquipment(source,pick,out _)&&d.EquipmentCards.Count==0,
                $"Equip {id}: exact admission, no mutation");
            q.Tick();q.Tick();q.Tick();
            check(d.EquipmentCards.Count==1&&q.IsBusy&&d.Hand.Count==0&&
                d.AttackValue(side,0)==1000+atk&&d.DefenseValue(side,0)==1000+def,
                $"Equip {id}: correct independent modifiers and gate");
            gate.Release=true;Drain(q);
            check(d.TryCaptureCardTarget(owner,DuelZone.SpellTrap,0,out var equipped)&&
                equipped.Occurrence==source.Occurrence&&!d.SpellFaceDown[owner,0]&&
                !d.TryQueueEquipment(equipped,pick,out _),"Physical equip preserved; no duplicate activation");
            if(removal==0){q.Enqueue(new DestroyCommand(owner,equipped));Drain(q);}
            else if(removal==1){q.Enqueue(new DestroyCommand(side,pick));Drain(q);}
            else if(removal==2){q.Enqueue(new ReturnToHandCommand(side,pick));Drain(q);}
            else
            {
                // Both turn and combat remain on the existing sandbox paths.
                if(d.ActivePlayer==side)d.NextTurn();
                if(d.Turn==1){d.NextTurn();d.NextTurn();}
                check(d.BeginBattle(out _)&&d.Attack(0,0,out _),"Real battle removal of equipped monster");
            }
            check(d.EquipmentCards.Count==0&&d.Spells[owner,0] is null&&
                d.Graveyard(owner).Count(c=>c.Id==id)==1&&
                d.TryCaptureCardTarget(owner,DuelZone.Graveyard,
                    d.Graveyard(owner).ToList().FindIndex(c=>c.Id==id),out var removed)&&
                removed.Occurrence==source.Occurrence,
                $"Equip {id}: source removed exactly once to its owner's GY");
            if(removal==0)
                check(d.AttackValue(side,0)==1000&&d.DefenseValue(side,0)==1000,"Source destroyed: base restored");
            else
                check(d.Monsters[side,0] is null&&
                    (removal!=2||d.HandFor(side).Any(c=>ReferenceEquals(c,choices[0].Card))),
                    "Target departure: correct zone and no lingering equip");
        }

        var stale=DemoDuel.FromPartialObservation(0,[Spell(300)],Slots(Monster(42,15,2)),Slots(),8000,equipmentContextResolved:true);
        var sq=stale.CreatePrimitiveActionQueue();
        stale.TryCaptureCardTarget(0,DuelZone.Hand,0,out var e);
        var selected=stale.QueuedEquipmentCandidates(e).Single().Target;
        stale.Monsters[0,0]=Monster(42,15,2);
        check(!stale.TryQueueEquipment(e,selected,out _)&&!sq.IsBusy&&stale.Hand.Count==1,
            "Replacement before admission cannot inherit selected target");
        var spoof=DemoDuel.FromPartialObservation(0,[Spell(300,21)],Slots(Monster(42,15,2)),Slots(),8000,equipmentContextResolved:true);
        var tq=spoof.CreatePrimitiveActionQueue();
        spoof.TryCaptureCardTarget(0,DuelZone.Hand,0,out var fake);
        spoof.TryCaptureCardTarget(0,DuelZone.Monster,0,out var tgt);
        check(spoof.QueuedEquipmentCandidates(fake).Count==0&&!spoof.TryQueueEquipment(fake,tgt,out _)&&
            !spoof.Equip(0,0,0,out _)&&!tq.IsBusy,"Wrong card class cannot create equip link");
    }
}
