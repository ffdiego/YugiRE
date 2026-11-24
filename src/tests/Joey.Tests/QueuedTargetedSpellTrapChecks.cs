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
        public bool CanComplete(QueuedDuelCommand c,CommandResult result)=>c.Command is not DestroyCommand||Release;
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

        var stale=DemoDuel.FromPartialObservation(0,[Spell(1079),Spell(332)],Units(),Units(),8000);
        stale.SetFromHand(1,out _);
        var stq=stale.CreatePrimitiveActionQueue();
        var src=Capture(stale,0,DuelZone.Hand,0);var old=Capture(stale,0,DuelZone.FieldSpell,0);
        stale.FieldSpells[0]=Spell(332);
        check(!stale.TryQueueTargetedSpellTrap(src,old,out _)&&stale.Hand.Count==1&&!stq.IsBusy,
            "Stale selection rejected before spend");
    }
}
