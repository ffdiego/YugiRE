using Joey.Core;

internal static class QueuedTargetedSpellTrapChecks
{
    private static Card Spell(ushort id,int type=22) => new(id,id,"Spell/Trap","",(uint)type<<20);
    private static Card?[] Units() => [new Card(77,77,"Warrior","",
        (15u<<20)|(4u<<25)|(100u<<9)|80u),null,null,null,null];
    private static CardTarget Capture(DemoDuel d,int side,DuelZone zone,int index)
    {
        if(!d.TryCaptureCardTarget(side,zone,index,out var t))
            throw new InvalidOperationException("Untracked test occurrence.");
        return t;
    }
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Targeted queue did not drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance=>true;
        public bool Release {get;set;}=true;
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>c.Command is not (DestroyCommand or ReturnToHandCommand)||Release;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        {
            int enemy=1-side;
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(300),Spell(332),Spell(1056,21)],
                Units(),Units(),8000,equipmentContextResolved:true,
                side0Deck:side==0?[Spell(1079)]:null,side1Deck:side==1?[Spell(1079)]:null);
            check(d.Equip(0,side,0,out _)&&d.SetFromHand(0,out _)&&d.SetFromHand(0,out _),
                "MST setup: real equip, FIELD and hidden trap");
            d.NextTurn();
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            q.Enqueue(new DrawCommand(side));Drain(q);
            var source=Capture(d,side,DuelZone.Hand,0);
            var candidates=d.QueuedSpellTrapTargetCandidates(source);
            var target=Capture(d,enemy,DuelZone.SpellTrap,0);
            check(candidates.Count==3 && candidates.Any(c=>c.Target.Zone==DuelZone.FieldSpell) &&
                candidates.Any(c=>c.FaceDown) && d.Hand.Count==1 && !q.IsBusy,
                "MST query includes equip, set and FIELD without state mutation");
            check(d.TryQueueTargetedSpellTrap(source,target,out _)&&d.Hand.Count==1&&
                d.AttackValue(side,0)==1300,"MST admission preserves source and target until tick");
            q.Tick();q.Tick();
            gate.Release=false;q.Tick();q.Tick();
            check(d.EquipmentCards.Count==0&&d.AttackValue(side,0)==1000&&q.IsBusy,
                "MST removes only selected equipment, once during gate");
            gate.Release=true;Drain(q);
            check(d.FieldSpells[enemy]?.Id==332&&d.Spells[enemy,1]?.Id==1056&&
                d.Graveyard(enemy).Single().Id==300&&d.Graveyard(side).Single().Id==1079,
                "MST preserves unrelated cards");
        }

        var set=DemoDuel.FromPartialObservation(0,[Spell(1079),Spell(332)],Units(),Units(),8000);
        set.SetFromHand(1,out _);set.SetFromHand(0,out _);
        var sourceSet=Capture(set,0,DuelZone.SpellTrap,0);
        var field=Capture(set,0,DuelZone.FieldSpell,0);
        var sq=set.CreatePrimitiveActionQueue();
        check(set.QueuedSpellTrapTargetCandidates(sourceSet).Count==0&&
            !set.TryQueueTargetedSpellTrap(sourceSet,field,out _),"MST cannot activate on Set turn");
        set.NextTurn();set.NextTurn();
        check(set.QueuedSpellTrapTargetCandidates(sourceSet).Single().Target==field&&
            !set.TryQueueTargetedSpellTrap(sourceSet,sourceSet,out _)&&
            set.TryQueueTargetedSpellTrap(sourceSet,field,out _),"Later MST can choose own FIELD but not itself");
        Drain(sq);
        check(set.Graveyard(0).Select(c=>c.Id).SequenceEqual(new ushort[]{1079,332})&&
            set.FieldSpells[0] is null&&!set.FieldSpellFaceDown[0],"Set MST destroys own FIELD");

        foreach(bool chooseMessage in new[]{false,true})
        foreach(bool replace in new[]{false,true})
        {
            ushort spellId=chooseMessage?(ushort)1079:(ushort)1004;
            var board=DemoDuel.FromPartialObservation(0,
                [Spell(1528,21),Spell(1541),Spell(1056,21)],Units(),Units(),8000,
                side1Deck:[Spell(spellId)]);
            board.SetFromHand(0,out _);board.SetFromHand(1,out _);
            board.NextTurn();board.NextTurn();
            check(board.ActivateSetSpell(0,out _),"Destiny Board activates on later turn");
            board.NextTurn();board.NextTurn();board.NextTurn();
            var q=board.CreatePrimitiveActionQueue();q.Enqueue(new DrawCommand(1));Drain(q);
            if(!chooseMessage){check(board.TryQueueSpellTrapSet(0,out _),"Remove Trap Set");Drain(q);}
            var source=Capture(board,1,chooseMessage?DuelZone.Hand:DuelZone.SpellTrap,0);
            var primary=Capture(board,0,DuelZone.SpellTrap,chooseMessage?2:0);
            var options=board.QueuedSpellTrapTargetCandidates(source);
            check(chooseMessage?options.Count==3:options.Count==1&&options.Single().Target==primary,
                "Remove Trap excludes hidden traps and face-up messages");
            check(board.TryQueueTargetedSpellTrap(source,primary,out _),"Targeted Destiny Board batch admitted");
            q.Tick();q.Tick();
            if(replace)board.Spells[0,chooseMessage?2:0]=Spell(chooseMessage?(ushort)1541:(ushort)1528,
                chooseMessage?22:21);
            Drain(q);
            check(board.Spells[0,1]?.Id==1056&&board.SpellFaceDown[0,1],
                "Unrelated hidden trap survives targeted effect");
            if(replace)
                check(board.Spells[0,0]?.Id==1528&&board.Spells[0,2]?.Id==1541&&
                    board.Graveyard(0).Count==0,"Stale primary does not destroy replacement or cascade");
            else
                check(board.Spells[0,0] is null&&board.Spells[0,2] is null&&
                    board.Graveyard(0).Select(c=>c.Id).SequenceEqual(chooseMessage?
                        new ushort[]{1541,1528}:new ushort[]{1528,1541}),
                    "Destroy primary then cascade each remaining face-up piece");
        }

        foreach(int side in new[]{0,1})
        foreach(bool fromSet in new[]{false,true})
        foreach(int targetKind in new[]{0,1,2})
        {
            int enemy=1-side;
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(300),Spell(332),Spell(1056,21)],
                Units(),Units(),8000,equipmentContextResolved:true,
                side0Deck:side==0?[Spell(1539),Spell(331)]:null,
                side1Deck:side==1?[Spell(1539),Spell(331)]:null);
            check(d.Equip(0,side,0,out _)&&d.SetFromHand(0,out _)&&d.SetFromHand(0,out _),
                "Spiritualism setup equip, FIELD and hidden trap");
            d.NextTurn();
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            q.EnqueueBatch([new DrawCommand(side),new DrawCommand(side)]);Drain(q);
            check(d.TryQueueSpellTrapSet(1,out _),"Own FIELD Set");Drain(q);
            if(fromSet){check(d.TryQueueSpellTrapSet(0,out _),"Spiritualism Set");Drain(q);}
            check(!(fromSet?d.ActivateSetSpell(0,out _):d.Play(0,out _)),"No legacy destroy bypass");
            var source=Capture(d,side,fromSet?DuelZone.SpellTrap:DuelZone.Hand,0);
            var own=Capture(d,side,DuelZone.FieldSpell,0);
            var target=Capture(d,enemy,targetKind==2?DuelZone.FieldSpell:DuelZone.SpellTrap,targetKind==1?1:0);
            var options=d.QueuedSpellTrapTargetCandidates(source);
            check(options.Count==3&&options.All(c=>c.Target.Side==enemy)&&
                !d.TryQueueTargetedSpellTrap(source,own,out _)&&!q.IsBusy,
                "Spiritualism cannot select own S/T; enemy hidden and face-up eligible");
            int start=q.Trace.Count;
            check(d.TryQueueTargetedSpellTrap(source,target,out _)&&d.Graveyard(side).Count==0,
                "Admission does not spend source");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==1539&&d.HandFor(enemy).Count==0,
                "Spend first");
            gate.Release=false;q.Tick();q.Tick();
            check(d.HandFor(enemy).Count==1&&q.IsBusy,"Return executes once while presentation waits");
            gate.Release=true;Drain(q);
            var received=Capture(d,enemy,DuelZone.Hand,0);
            check(received.Occurrence==target.Occurrence&&d.Graveyard(enemy).Count==0&&
                d.FieldSpells[side]?.Id==331&&(d.FieldSpells[enemy] is null)==(targetKind==2)&&
                (d.Spells[enemy,1] is null)==(targetKind==1)&&
                d.EquipmentCards.Count==(targetKind==0?0:1)&&d.AttackValue(side,0)==(targetKind==0?1000:1300)&&
                q.Trace.Skip(start).Count(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is ReturnToHandCommand)==1&&
                !q.Trace.Skip(start).Any(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is DestroyCommand),
                "Selected occurrence returns only, preserving unrelated cards and links");
        }

        foreach(bool chooseMessage in new[]{false,true})
        foreach(bool replace in new[]{false,true})
        {
            var board=DemoDuel.FromPartialObservation(0,
                [Spell(1528,21),Spell(1541)],Units(),Units(),8000,side1Deck:[Spell(1539)]);
            board.SetFromHand(0,out _);board.NextTurn();board.NextTurn();
            check(board.ActivateSetSpell(0,out _),"Spiritualism Board activates");
            board.NextTurn();board.NextTurn();board.NextTurn();
            var q=board.CreatePrimitiveActionQueue();q.Enqueue(new DrawCommand(1));Drain(q);
            var source=Capture(board,1,DuelZone.Hand,0);
            int slot=chooseMessage?1:0;
            var primary=Capture(board,0,DuelZone.SpellTrap,slot);
            check(board.TryQueueTargetedSpellTrap(source,primary,out _),"Spiritualism Board target admitted");
            q.Tick();q.Tick();
            if(replace)board.Spells[0,slot]=Spell(chooseMessage?(ushort)1541:(ushort)1528,chooseMessage?22:21);
            Drain(q);
            if(replace)
                check(board.HandFor(0).Count==0&&board.Graveyard(0).Count==0&&
                    board.Spells[0,0] is not null&&board.Spells[0,1] is not null,
                    "Stale target skips return and dependent cascade");
            else
                check(board.HandFor(0).Single().Id==(chooseMessage?1541:1528)&&
                    board.Graveyard(0).Single().Id==(chooseMessage?1528:1541)&&
                    board.Spells[0,0] is null&&board.Spells[0,1] is null,
                    "Selected Board piece returns; remaining piece destroyed by modeled Board rule");
        }

        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1539),Spell(332)],Units(),Units(),8000);
            d.SetFromHand(1,out _);var q=d.CreatePrimitiveActionQueue();
            var source=Capture(d,0,DuelZone.Hand,0);
            check(d.QueuedSpellTrapTargetCandidates(source).Count==0&&
                !d.TryQueueTargetedSpellTrap(source,Capture(d,0,DuelZone.FieldSpell,0),out _)&&
                !q.IsBusy,"Own-only board is not a valid Spiritualism activation");
        }

        var stale=DemoDuel.FromPartialObservation(0,[Spell(1079),Spell(332)],Units(),Units(),8000);
        stale.SetFromHand(1,out _);
        var stq=stale.CreatePrimitiveActionQueue();
        var src=Capture(stale,0,DuelZone.Hand,0);var old=Capture(stale,0,DuelZone.FieldSpell,0);
        stale.FieldSpells[0]=Spell(332);
        check(!stale.TryQueueTargetedSpellTrap(src,old,out _)&&stale.Hand.Count==1&&!stq.IsBusy,
            "Stale selection rejected before spend");
    }
}
