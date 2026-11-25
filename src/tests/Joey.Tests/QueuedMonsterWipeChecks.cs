using Joey.Core;

internal static class QueuedMonsterWipeChecks
{
    private static Card Monster(int index, int type, ushort id = 24) =>
        new(index, id, "Monster", "", ((uint)type << 20) | (4u << 25) | (100u << 9));
    private static Card Spell(ushort id) => new(id,id,"Wipe","",22u << 20);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static CardTarget Capture(DemoDuel d,int side,DuelZone zone,int index)
    {
        if(!d.TryCaptureCardTarget(side,zone,index,out var target)) throw new InvalidOperationException("Untracked test occurrence.");
        return target;
    }
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
        // Eternal Rest is based on live links, not catalogue type or the
        // number of equipment cards. Exercise both controllers and sources.
        foreach (int side in new[] {0,1})
        foreach (bool set in new[] {false,true})
        {
            var warrior=Monster(24,15);
            var d=DemoDuel.FromPartialObservation(side,
                [Spell(300),Spell(300),Spell(300),Spell(655)],
                Slots(warrior,warrior),Slots(warrior,warrior),8000,
                equipmentContextResolved:true);
            var gate=new Gate();
            var q=d.CreatePrimitiveActionQueue(gate);
            foreach(int targetSide in new[]{side,side,1-side})
            {
                check(d.TryQueueEquipment(DuelZone.Hand,0,targetSide,0,out _),
                    "Eternal Rest equipment setup");
                Drain(q);
            }
            check(d.EquipmentCards.Count==3,"Two links to one monster, one to the other");
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Eternal Rest Set");Drain(q);}
            int sourceSlot=set?Enumerable.Range(0,5).Single(z=>d.Spells[side,z]?.Id==655):0;
            check(!(set?d.ActivateSetSpell(sourceSlot,out _):d.Play(0,out _)),
                "Eternal Rest must not bypass the queue");
            int start=q.Trace.Count;
            check(d.TryQueueActivation(sourceSlot,set,out _),"Eternal Rest generic admission");
            q.Tick(); q.Tick();
            check(d.Graveyard(side).Any(c=>c.Id==655)&&d.EquipmentCards.Count==3,
                "Source is spent before the first destruction");
            gate.Release=false; q.Tick(); q.Tick();
            check(q.IsBusy&&d.LastMonsterWipe is null,
                "Eternal Rest holds each destruction at its presentation gate");
            gate.Release=true; Drain(q);
            check(d.Monsters[0,0] is null&&d.Monsters[1,0] is null&&
                d.Monsters[0,1] is not null&&d.Monsters[1,1] is not null&&
                d.EquipmentCards.Count==0&&d.Graveyard(side).Count(c=>c.Id==300)==3&&
                d.LastMonsterWipe is {EffectResourceId:655,TotalDestroyed:2},
                "Eternal Rest destroys only equipped monsters on both sides and cleans all links");
            check(q.Trace.Skip(start).Count(x=>x.Stage==ActionTraceStage.Executed&&
                x.Entry.Command is DestroyCommand)==5,
                "Each monster and each linked equipment is destroyed exactly once");
        }
        {
            var warrior=Monster(24,15);
            var d=DemoDuel.FromPartialObservation(0,[Spell(300),Spell(655)],
                Slots(warrior),Slots(warrior),8000,equipmentContextResolved:true);
            var q=d.CreatePrimitiveActionQueue();
            check(!d.TryQueueActivation(1,false,out _)&&!q.IsBusy,
                "Eternal Rest rejects a field with no equipped monster");
            check(d.TryQueueEquipment(DuelZone.Hand,0,0,0,out _),"Stale link setup");
            Drain(q);
            d.Spells[0,0]=Spell(300); // a distinct fixture occurrence, not the linked source
            check(!d.TryQueueActivation(0,false,out _)&&!q.IsBusy&&d.Hand.Count==1,
                "A replaced equipment occurrence cannot make its old monster eligible");
        }
        check(MonsterWipeRules.SupportsQueued(655)&&MonsterWipeRules.Scope(655) is null,
            "Eternal Rest only uses the queue-backed link-aware selector");

        // Tribute to The Doomed: distinct exact cost, spellSource and target;
        // both controllers, Hand/Set and both target sides.
        foreach(int side in new[]{0,1})
        foreach(bool set in new[]{false,true})
        foreach(bool own in new[]{false,true})
        {
            int targetSide=own?side:1-side;
            var warrior=Monster(24,15);
            var d=DemoDuel.FromPartialObservation(side,
                [Spell(300),Spell(1023),Spell(1023),Monster(25,15,25)],
                Slots(warrior,warrior),Slots(warrior,warrior),8000,
                equipmentContextResolved:true);
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueEquipment(DuelZone.Hand,0,targetSide,0,out _),"Tribute linked equipment");Drain(q);
            d.MonsterFaceDown[targetSide,1]=true;
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Tribute Set");Drain(q);}
            var spellSource=Capture(d,side,set?DuelZone.SpellTrap:DuelZone.Hand,set?1:0);
            var costs=d.QueuedDiscardSpellCosts(spellSource);
            var cost=costs.Single(c=>c.Card.Id==1023).Target;
            var target=Capture(d,targetSide,DuelZone.Monster,0);
            check(costs.Count==2&&cost!=spellSource&&
                d.QueuedTargetedMonsterSpellCandidates(spellSource).Count==4&&
                d.QueuedTargetedMonsterSpellCandidates(spellSource).Count(c=>c.FaceDown)==1&&
                !d.TryQueueTargetedMonsterSpell(spellSource,spellSource,target,out _)&&
                !d.TryQueueTargetedMonsterSpell(spellSource,cost with {Side=1-side},target,out _)&&
                !(set?d.ActivateSetSpell(1,out _):d.Play(0,out _))&&
                !q.IsBusy,"Both target sides including hidden, discard spellSource excluded, no legacy bypass");
            int start=q.Trace.Count;
            int discards=0;d.HandTransferred+=_=>discards++;
            check(d.TryQueueTargetedMonsterSpell(spellSource,cost,target,out _)&&
                d.Graveyard(side).Count==0&&d.EquipmentCards.Count==1,"Read-only admission");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==1023&&discards==1&&
                d.Monsters[targetSide,0] is not null,"Discard first, spellSource still tracked");
            q.Tick();
            check(d.Graveyard(side).Count==2&&discards==1&&d.Monsters[targetSide,0] is not null,
                "Source spend is not another discard");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(q.IsBusy&&d.Monsters[targetSide,0] is null&&d.EquipmentCards.Count==1,
                "Monster removal gate precedes linked equipment");
            gate.Release=true;Drain(q);
            var paidSpellMoves=q.Trace.Skip(start).Where(x=>x.Stage==ActionTraceStage.Executed)
                .Select(x=>x.Entry.Command).Where(c=>c is DiscardCommand or SendToGraveyardCommand or DestroyCommand).ToArray();
            check(paidSpellMoves.Length==4&&paidSpellMoves[0]==new DiscardCommand(side,cost)&&
                paidSpellMoves[1]==new SendToGraveyardCommand(side,spellSource)&&paidSpellMoves[2]==new DestroyCommand(targetSide,target)&&
                d.Graveyard(side).Select(c=>c.Id).SequenceEqual(own?new ushort[]{1023,1023,24,300}:new ushort[]{1023,1023,300})&&
                d.EquipmentCards.Count==0&&d.Monsters[1-targetSide,0] is not null&&
                d.Monsters[0,1] is not null&&d.Monsters[1,1] is not null&&d.HandFor(side).Single().Id==25,
                "Exact payment and one destruction with its linked cleanup");
        }
        foreach(bool staleBeforeCost in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1023),Spell(300)],
                Slots(),Slots(Monster(24,15)),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueSpellTrapSet(0,out _),"Tribute stale Set");Drain(q);
            var spellSource=Capture(d,0,DuelZone.SpellTrap,0);
            var cost=Capture(d,0,DuelZone.Hand,0);
            var target=Capture(d,1,DuelZone.Monster,0);
            check(d.TryQueueTargetedMonsterSpell(spellSource,cost,target,out _),"Tribute stale reservation");
            q.Tick();
            if(staleBeforeCost)d.Spells[0,0]=Spell(1023);
            else {q.Tick();q.Tick();d.Monsters[1,0]=Monster(24,15);}
            Drain(q);
            check(d.Monsters[1,0] is not null&&d.Graveyard(1).Count==0&&
                d.Graveyard(0).Count==(staleBeforeCost?0:2)&&
                d.HandFor(0).Count==(staleBeforeCost?1:0),
                "Invalid spellSource before payment costs nothing; stale target after payment never retargets");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1023)],Slots(),Slots(Monster(24,15)),8000);
            var q=d.CreatePrimitiveActionQueue();
            var spellSource=Capture(d,0,DuelZone.Hand,0);
            check(d.QueuedDiscardSpellCosts(spellSource).Count==0&&d.QueuedTargetedMonsterSpellCandidates(spellSource).Count==0&&
                !d.TryQueueTargetedMonsterSpell(spellSource,spellSource,Capture(d,1,DuelZone.Monster,0),out _)&&!q.IsBusy,
                "No discard cost available");
        }

        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1023),Spell(300)],
                Slots(),Slots(Monster(47,3,47)),8000,side1Deck:[Monster(24,15)]);
            d.MonsterFaceDown[1,0]=true;
            var q=d.CreatePrimitiveActionQueue();
            var spellSource=Capture(d,0,DuelZone.Hand,0);
            var cost=Capture(d,0,DuelZone.Hand,1);
            var target=Capture(d,1,DuelZone.Monster,0);
            check(d.TryQueueTargetedMonsterSpell(spellSource,cost,target,out _),"Hidden Sangan target");
            Drain(q);
            check(d.Monsters[1,0] is null&&d.Graveyard(1).Single().Id==47&&
                d.PendingCardSelection is {Owner:1,EffectResourceId:47},
                "Hidden target destroyed without FLIP and Sangan search opens after removal");
            var request=d.PendingCardSelection!;
            check(d.TryQueueDeckSearch(request,d.QueuedDeckSearchCandidates(request).Single().Target,out _),
                "Reuse queued Sangan search after paid removal");
            Drain(q);
            check(d.PendingCardSelection is null&&d.HandFor(1).Single().Id==24,"Search completes normally");
        }

        // Thousand Knives: each real DAT Dark Magician identity, both sides,
        // Hand/Set, a hidden opposing target, no extra discard required.
        foreach (ushort magicianId in new ushort[]{34,1210,2034})
        foreach (int side in new[]{0,1})
        foreach (bool set in new[]{false,true})
        {
            int enemy=1-side;
            var magician=Monster(magicianId,18,magicianId);
            var victim=Monster(24,15);
            var d=DemoDuel.FromPartialObservation(side,[Spell(1211)],
                side==0?Slots(magician):Slots(victim,victim),
                side==1?Slots(magician):Slots(victim,victim),8000);
            d.MonsterFaceDown[enemy,1]=true;
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Knives Set");Drain(q);}
            var knivesSource=Capture(d,side,set?DuelZone.SpellTrap:DuelZone.Hand,0);
            var knivesTarget=Capture(d,enemy,DuelZone.Monster,1);
            var ownTarget=Capture(d,side,DuelZone.Monster,0);
            check(d.QueuedDiscardSpellCosts(knivesSource).Count==0&&
                d.QueuedTargetedMonsterSpellCandidates(knivesSource).Count==2&&
                d.QueuedTargetedMonsterSpellCandidates(knivesSource).Any(c=>c.Target==knivesTarget&&c.FaceDown)&&
                !d.TryQueueTargetedMonsterSpell(knivesSource,ownTarget,out _)&&
                !(set?d.ActivateSetSpell(0,out _):d.Play(0,out _)),
                "Knives requires no discard; own target and legacy bypass rejected");
            int start=q.Trace.Count;
            check(d.TryQueueTargetedMonsterSpell(knivesSource,knivesTarget,out _)&&
                d.Graveyard(side).Count==0,"Knives admission");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==1211&&d.Monsters[enemy,1] is not null,"Knives spends source");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(q.IsBusy&&d.Monsters[enemy,1] is null&&d.Graveyard(enemy).Single().Id==24,
                "Knives hidden target destroyed once while gate waits");
            gate.Release=true;Drain(q);
            check(d.Monsters[side,0]?.Id==magicianId&&d.Monsters[enemy,0] is not null&&
                d.HandFor(side).Count==0&&
                q.Trace.Skip(start).Count(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is DestroyCommand)==1&&
                !q.Trace.Skip(start).Any(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is DiscardCommand),
                "Knives preserves magician and other monster; no discard command");
        }
        foreach (int condition in new[]{0,1,2})
        {
            // Absent, face-down, opponent-only.
            var magician=Monster(34,18,34);
            var victim=Monster(24,15);
            var d=DemoDuel.FromPartialObservation(0,[Spell(1211)],
                condition==1?Slots(magician):Slots(victim),
                condition==2?Slots(magician):Slots(victim),8000);
            if(condition==1)d.MonsterFaceDown[0,0]=true;
            var q=d.CreatePrimitiveActionQueue();
            var knivesSource=Capture(d,0,DuelZone.Hand,0);
            var knivesTarget=Capture(d,1,DuelZone.Monster,0);
            check(d.QueuedTargetedMonsterSpellCandidates(knivesSource).Count==0&&
                !d.TryQueueTargetedMonsterSpell(knivesSource,knivesTarget,out _)&&!q.IsBusy,
                "Knives requires face-up own Dark Magician");
        }
        foreach(bool beforeSpend in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1211)],
                Slots(Monster(34,18,34)),Slots(Monster(24,15)),8000);
            var q=d.CreatePrimitiveActionQueue();
            var knivesSource=Capture(d,0,DuelZone.Hand,0);
            var knivesTarget=Capture(d,1,DuelZone.Monster,0);
            check(d.TryQueueTargetedMonsterSpell(knivesSource,knivesTarget,out _),"Knives stale admission");
            q.Tick();
            if(beforeSpend)d.MonsterFaceDown[0,0]=true;
            else {q.Tick();d.Monsters[1,0]=Monster(24,15);}
            Drain(q);
            check(d.Monsters[1,0] is not null&&d.Graveyard(1).Count==0&&
                d.Graveyard(0).Count==(beforeSpend?0:1)&&d.HandFor(0).Count==(beforeSpend?1:0),
                "Knives condition rechecked before spend; replaced target not destroyed");
        }

        // Fissure uses live ATK, not printed ATK, and ignores hidden
        // cards even when their printed ATK is lower.
        foreach(int fissureSide in new[]{0,1})
        foreach(bool fissureSet in new[]{false,true})
        {
            int enemy=1-fissureSide;
            var weak=new Card(70,70,"weak","",(15u<<20)|(4u<<25)|(90u<<9));
            var middle=Monster(24,15);
            var hidden=new Card(71,71,"hidden","",(15u<<20)|(4u<<25)|(10u<<9));
            var d=DemoDuel.FromPartialObservation(fissureSide,[Spell(300),Spell(1001)],
                fissureSide==0?Slots(Monster(24,15)):Slots(weak,middle,hidden),
                fissureSide==1?Slots(Monster(24,15)):Slots(weak,middle,hidden),
                8000,equipmentContextResolved:true);
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            check(d.TryQueueEquipment(DuelZone.Hand,0,enemy,0,out _),"Fissure equip setup");Drain(q);
            d.MonsterFaceDown[enemy,2]=true;
            d.DefensePosition[enemy,1]=true;
            if(fissureSet){check(d.TryQueueSpellTrapSet(0,out _),"Fissure Set");Drain(q);}
            int fissureIndex=fissureSet?1:0;
            var fissureSource=Capture(d,fissureSide,fissureSet?DuelZone.SpellTrap:DuelZone.Hand,fissureIndex);
            var fissureTarget=Capture(d,enemy,DuelZone.Monster,1);
            check(d.AttackValue(enemy,0)==1200&&d.AttackValue(enemy,1)==1000&&
                d.QueuedTargetedMonsterSpellCandidates(fissureSource).Single().Target==fissureTarget&&
                !d.TryQueueTargetedMonsterSpell(fissureSource,Capture(d,enemy,DuelZone.Monster,0),out _)&&
                !d.TryQueueTargetedMonsterSpell(fissureSource,Capture(d,enemy,DuelZone.Monster,2),out _)&&
                !d.TryQueueTargetedMonsterSpell(fissureSource,Capture(d,fissureSide,DuelZone.Monster,0),out _)&&
                !(fissureSet?d.ActivateSetSpell(fissureIndex,out _):d.Play(fissureIndex,out _)),
                "Fissure uses effective ATK even in defense and excludes hidden/own/higher");
            check(d.TryQueueActivation(fissureIndex,fissureSet,out _),"Fissure unique minimum automatic");
            q.Tick();q.Tick();
            check(d.Graveyard(fissureSide).Single().Id==1001&&d.Monsters[enemy,1] is not null,
                "Fissure source spend before destruction");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(q.IsBusy&&d.Monsters[enemy,1] is null,"Fissure gate holds applied destruction");
            gate.Release=true;Drain(q);
            check(d.Monsters[enemy,0]?.Id==70&&d.Monsters[enemy,2]?.Id==71&&
                d.MonsterFaceDown[enemy,2]&&d.EquipmentCards.Count==1&&
                d.Graveyard(enemy).Single().Id==24&&d.Monsters[fissureSide,0] is not null,
                "Fissure preserves other monsters and their equipment");
        }
        foreach(int chosen in new[]{0,1})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1001)],Slots(),
                Slots(Monster(24,15),Monster(25,15,25)),8000);
            var q=d.CreatePrimitiveActionQueue();
            var fissureSource=Capture(d,0,DuelZone.Hand,0);
            var fissureTarget=Capture(d,1,DuelZone.Monster,chosen);
            check(d.QueuedTargetedMonsterSpellCandidates(fissureSource).Count==2&&
                !d.TryQueueActivation(0,false,out _)&&!q.IsBusy&&d.Hand.Count==1,
                "Fissure tie does not silently choose a slot or spend");
            check(d.TryQueueTargetedMonsterSpell(fissureSource,fissureTarget,out _),"Fissure explicit tie choice");
            Drain(q);
            check(d.Monsters[1,chosen] is null&&d.Monsters[1,1-chosen] is not null&&
                d.Graveyard(0).Single().Id==1001,"Fissure destroys only chosen tied occurrence");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1001)],Slots(Monster(24,15)),
                Slots(Monster(24,15)),8000);
            d.MonsterFaceDown[1,0]=true;
            var q=d.CreatePrimitiveActionQueue();
            check(!d.TryQueueActivation(0,false,out _)&&!q.IsBusy&&d.Hand.Count==1,
                "Fissure no face-up opponent means no activation");
        }
        foreach(bool beforeSpend in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1001)],Slots(),
                Slots(Monster(24,15)),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueActivation(0,false,out _),"Fissure stale setup");q.Tick();
            if(!beforeSpend)q.Tick();
            d.MonsterFaceDown[1,0]=true;
            Drain(q);
            check(d.Monsters[1,0] is not null&&d.Graveyard(1).Count==0&&
                d.Graveyard(0).Count==(beforeSpend?0:1)&&d.Hand.Count==(beforeSpend?1:0),
                "Fissure hidden before spend rejects; after spend no replacement chosen");
        }

        // Offerings: quick-play in own Main only; any face-up monster.
        foreach(int offeringsSide in new[]{0,1})
        foreach(bool offeringsSet in new[]{false,true})
        foreach(bool offeringsOwn in new[]{false,true})
        {
            int enemy=1-offeringsSide;
            int victimSide=offeringsOwn?offeringsSide:enemy;
            var d=DemoDuel.FromPartialObservation(offeringsSide,[Spell(1451)],
                Slots(Monster(24,15),Monster(25,15,25)),Slots(Monster(24,15),Monster(25,15,25)),
                8000,side0Deck:[Monster(80,15,80),Monster(81,15,81)],
                side1Deck:[Monster(80,15,80),Monster(81,15,81)]);
            d.MonsterFaceDown[enemy,1]=true;
            var q=d.CreatePrimitiveActionQueue();
            if(offeringsSet)
            {
                check(d.TryQueueSpellTrapSet(0,out _),"Offerings Set");Drain(q);
                var fresh=Capture(d,offeringsSide,DuelZone.SpellTrap,0);
                check(d.QueuedTargetedMonsterSpellCandidates(fresh).Count==0&&
                    !d.TryQueueTargetedMonsterSpell(fresh,Capture(d,victimSide,DuelZone.Monster,0),out _),
                    "Offerings cannot activate during its Set turn");
                d.NextTurn();d.NextTurn();
            }
            var offeringsSource=Capture(d,offeringsSide,offeringsSet?DuelZone.SpellTrap:DuelZone.Hand,0);
            var offeringsTarget=Capture(d,victimSide,DuelZone.Monster,0);
            int due=d.Turn+2;
            check(d.QueuedTargetedMonsterSpellCandidates(offeringsSource).Count==3&&
                !d.TryQueueTargetedMonsterSpell(offeringsSource,Capture(d,enemy,DuelZone.Monster,1),out _)&&
                d.TryQueueTargetedMonsterSpell(offeringsSource,offeringsTarget,out _)&&
                d.PendingSkippedDrawTurn(offeringsSide) is null,"Offerings valid face-up targets both sides");
            Drain(q);
            check(d.Monsters[victimSide,0] is null&&d.PendingSkippedDrawTurn(offeringsSide)==due&&
                d.PendingSkippedDrawTurn(enemy) is null,"Offerings resolves destruction and owner skip");
            // A generic effect draw does not consume a phase replacement.
            q.Enqueue(new DrawCommand(offeringsSide));Drain(q);
            check(d.HandFor(offeringsSide).Count==1&&d.PendingSkippedDrawTurn(offeringsSide)==due,
                "Effect draw is unaffected before due turn");
            d.NextTurn();
            check(d.TryQueueTurnDraw(out _),"Opponent ordinary draw admitted");Drain(q);
            check(d.HandFor(enemy).Count==1&&d.PendingSkippedDrawTurn(offeringsSide)==due,
                "Opponent draw unaffected");
            d.NextTurn();
            int deckBefore=d.DeckCount(offeringsSide);
            int traceStart=q.Trace.Count;
            check(d.TryQueueTurnDraw(out _),"Due turn draw opportunity admitted");Drain(q);
            check(!d.GameOver&&!d.CanDraw&&d.HandFor(offeringsSide).Count==1&&
                d.DeckCount(offeringsSide)==deckBefore&&d.PendingSkippedDrawTurn(offeringsSide) is null&&
                q.Trace.Skip(traceStart).Any(x=>x.Stage==ActionTraceStage.Executed&&
                    x.Entry.Command is TurnDrawCommand&&x.Result?.Detail=="Draw Phase skipped."),
                "Due Draw Phase consumed exactly once without card movement");
            d.NextTurn();d.NextTurn();
            check(d.Draw(out _)&&d.HandFor(offeringsSide).Count==2,"Following turn draws normally");
        }
        foreach(bool offeringsHide in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1451),Spell(1451)],Slots(),
                Slots(Monster(24,15),Monster(25,15,25)),8000);
            var q=d.CreatePrimitiveActionQueue();
            var first=Capture(d,0,DuelZone.Hand,0);
            check(d.TryQueueTargetedMonsterSpell(first,Capture(d,1,DuelZone.Monster,0),out _),
                "First Offerings");Drain(q);
            var second=Capture(d,0,DuelZone.Hand,0);
            check(d.TryQueueTargetedMonsterSpell(second,Capture(d,1,DuelZone.Monster,1),out _),
                "Second Offerings");q.Tick();q.Tick();
            if(offeringsHide)d.MonsterFaceDown[1,1]=true;
            Drain(q);
            check((d.Monsters[1,1] is not null)==offeringsHide&&d.PendingSkippedDrawTurn(0)==3,
                "Multiple resolved copies coalesce; hidden target still imposes skip");
            d.NextTurn();d.NextTurn();
            check(d.TryQueueTurnDraw(out _),"Empty deck skipped Draw Phase");Drain(q);
            check(!d.GameOver&&d.PendingSkippedDrawTurn(0) is null&&!d.CanDraw,
                "Skipped phase never causes deck-out");
            d.NextTurn();d.NextTurn();
            check(d.Draw(out _)&&d.Winner==1,"Later empty-deck draw loses normally");
        }
        foreach(bool staleOfferings in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1451)],Slots(),Slots(Monster(24,15)),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueTargetedMonsterSpell(Capture(d,0,DuelZone.Hand,0),
                Capture(d,1,DuelZone.Monster,0),out _),"Offerings stale test");q.Tick();
            if(staleOfferings)d.MonsterFaceDown[1,0]=true;
            Drain(q);
            check(d.PendingSkippedDrawTurn(0)==(staleOfferings?(int?)null:3),
                "Before-spend invalidity creates no skip");
            d.NextTurn();d.NextTurn();d.NextTurn();d.NextTurn();
            check(d.PendingSkippedDrawTurn(0) is null,"Skip expires on due turn exit without manual draw");
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
        check(linked.TryQueueEquipment(DuelZone.Hand,0,0,0,out _),"Wipe linked equipment setup");
        Drain(lq);
        bool sourceCaptured = linked.TryCaptureCardTarget(0, DuelZone.Hand, 0, out var source);
        bool ownCaptured = linked.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var ownMonster);
        bool opposingCaptured = linked.TryCaptureCardTarget(1, DuelZone.Monster, 0, out var opposingMonster);
        bool equipmentCaptured = linked.TryCaptureCardTarget(0, DuelZone.SpellTrap, 0, out var equipment);
        check(sourceCaptured && ownCaptured && opposingCaptured && equipmentCaptured,
            "Wipe captures real occurrences");
        int linkedTraceStart = lq.Trace.Count;
        check(linked.TryQueueMonsterWipe(DuelZone.Hand,0,out _),"Wipe with linked equipment admitted");
        Drain(lq);
        var moves = lq.Trace.Skip(linkedTraceStart)
            .Where(entry => entry.Stage == ActionTraceStage.Executed)
            .Select(entry => entry.Entry.Command)
            .Where(command => command is SendToGraveyardCommand or DestroyCommand);
        check(moves.SequenceEqual([
                new SendToGraveyardCommand(0, source), new DestroyCommand(0, ownMonster),
                new DestroyCommand(0, equipment), new DestroyCommand(1, opposingMonster)]),
            "A08 wipe spends its source, destroys ordered targets and follows immediately with linked cleanup");
        check(linked.EquipmentCards.Count==0 && linked.Spells[0,0] is null &&
            linked.Graveyard(0).Any(c=>c.Id==300) && linked.LastMonsterWipe is {TotalDestroyed:2},
            "Wipe removes linked equipment via separate command");
    }
}
