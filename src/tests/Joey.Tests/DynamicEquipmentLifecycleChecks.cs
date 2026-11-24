using Joey.Core;

internal static class DynamicEquipmentLifecycleChecks
{
    private static Card Monster(int index) => new(index,77,"Monster","",
        (15u<<20)|(2u<<29)|(4u<<25)|(100u<<9)|100u);
    private static Card Spell(ushort id) => new(id,id,"Spell","",22u<<20);
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<300&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Dynamic equipment queue did not drain.");
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int owner in new[]{0,1})
        foreach(int targetSide in new[]{0,1})
        foreach(bool fromSet in new[]{false,true})
        foreach(bool reverse in new[]{false,true})
        {
            ushort[] ids=[656,1449,1450,300];
            if(reverse)Array.Reverse(ids);
            Card[] hand=[.. ids.Select(Spell),Spell(FieldSpellRules.SogenId),Spell(1079)];
            Card?[][] fields=[[Monster(42),null,null,null,null],[Monster(43),null,null,null,null]];
            fields[owner][1]=Monster(44);
            var d=DemoDuel.FromPartialObservation(owner,hand,fields[0],fields[1],8000,equipmentContextResolved:true);
            d.MonsterFaceDown[owner,1]=d.DefensePosition[owner,1]=true;
            var q=d.CreatePrimitiveActionQueue();
            d.TryCaptureCardTarget(targetSide,DuelZone.Monster,0,out var target);
            var links=new Dictionary<ushort,CardTarget>();
            for(int i=0;i<ids.Length;i++)
            {
                if(fromSet){check(d.TryQueueSpellTrapSet(0,out _),"Dynamic equipment Set");Drain(q);}
                check(d.TryCaptureCardTarget(owner,fromSet?DuelZone.SpellTrap:DuelZone.Hand,fromSet?i:0,out var source)&&
                    d.QueuedEquipmentCandidates(source).Any(c=>c.Target==target)&&
                    d.TryQueueEquipment(source,target,out _),"Exact occurrence equipment admission");
                Drain(q);
                check(d.TryCaptureCardTarget(owner,DuelZone.SpellTrap,i,out var linked)&&linked.Occurrence==source.Occurrence,
                    "Equipment identity preserved");
                links.Add(ids[i],linked);
            }
            void Stats(int atk,int def,string reason) =>
                check(d.AttackValue(targetSide,0)==atk&&d.DefenseValue(targetSide,0)==def,reason);
            Stats(4100,4100,"Equal LP: three dynamic and fixed effects combine regardless of equip order");
            check(d.TryQueueSpellTrapSet(0,out _),"Set Field Spell");Drain(q);
            Stats(4600,4600,"Mage Power counts own Set FIELD");
            q.Enqueue(new DamageCommand(owner,1000));Drain(q);
            Stats(5600,4600,"Megamorph changes base only; other modifiers survive");
            check(d.TryQueueChangePosition(1,out _),"Reveal another controlled monster");Drain(q);
            Stats(6400,5400,"United updates after actual Flip Summon");
            d.TryCaptureCardTarget(owner,DuelZone.Monster,1,out var extraMonster);
            q.Enqueue(new DestroyCommand(owner,extraMonster));Drain(q);
            Stats(5600,4600,"United updates after another monster leaves");
            d.TryCaptureCardTarget(owner,DuelZone.Hand,0,out var mst);
            d.TryCaptureCardTarget(owner,DuelZone.FieldSpell,0,out var field);
            check(d.TryQueueTargetedSpellTrap(mst,field,out _),"Remove own Set FIELD by MST");Drain(q);
            Stats(5100,4100,"Mage Power drops after FIELD removal and MST resolution");
            q.Enqueue(new HealCommand(owner,2000));Drain(q);
            Stats(3600,4100,"Higher LP halves original ATK, not final attack");
            q.Enqueue(new DamageCommand(owner,1000));Drain(q);
            Stats(4100,4100,"LP equality restores base");
            if(reverse)
            {
                q.Enqueue(new ReturnToHandCommand(targetSide,target));Drain(q);
                check(d.Monsters[targetSide,0] is null&&d.EquipmentCards.Count==0&&
                    links.All(kv=>d.Graveyard(owner).Count(c=>c.Id==kv.Key)==1)&&
                    d.HandFor(targetSide).Any(c=>c.Id==77),"Target departure cleans four links");
            }
            else
            {
                q.Enqueue(new DestroyCommand(owner,links[300]));Drain(q);
                Stats(3300,3300,"Fixed equip gone and Mage count reduced");
                q.Enqueue(new DestroyCommand(owner,links[656]));Drain(q);
                Stats(2800,2800,"Megamorph removed and Mage count reduced");
                q.Enqueue(new DestroyCommand(owner,links[1449]));Drain(q);
                Stats(1500,1500,"Only Mage Power remains, counts itself");
                q.Enqueue(new DestroyCommand(owner,links[1450]));Drain(q);
                Stats(1000,1000,"All links gone, original attributes restored");
                check(d.EquipmentCards.Count==0&&links.All(kv=>d.Graveyard(owner).Count(c=>c.Id==kv.Key)==1),
                    "Each source arrives once in owner graveyard");
            }
            check(d.HandFor(1-owner).Count==(reverse&&targetSide!=owner?1:0)&&
                d.Life[owner]==8000,"Other hand and LP consistent");
        }
        for(int monsters=0;monsters<=5;monsters++)
        for(int spells=0;spells<=6;spells++)
        {
            var united=DynamicEquipmentRules.Evaluate(1449,1000,8000,8000,monsters,spells);
            var mage=DynamicEquipmentRules.Evaluate(1450,1000,8000,8000,monsters,spells);
            check(united.BaseAttack is null&&united.Delta.Attack==800*monsters&&united.Delta.Defense==800*monsters&&
                mage.BaseAttack is null&&mage.Delta.Attack==500*spells&&mage.Delta.Defense==500*spells,
                "Complete native counter domain");
        }
        var copies=DemoDuel.FromPartialObservation(0,[Spell(656),Spell(656)],
            [Monster(42),null,null,null,null],new Card?[5],8000,equipmentContextResolved:true);
        var cq=copies.CreatePrimitiveActionQueue();
        for(int i=0;i<2;i++){check(copies.TryQueueEquipment(DuelZone.Hand,0,0,0,out _),"Two Megamorph copies");Drain(cq);}
        cq.Enqueue(new DamageCommand(0,1000));Drain(cq);
        check(copies.AttackValue(0,0)==2000&&copies.DefenseValue(0,0)==1000,
            "Two same-owner Megamorph replacements do not multiply the working base twice");
    }
}
