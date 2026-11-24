using Joey.Core;

internal static class QueuedSpellTrapWipeChecks
{
    private static Card Spell(ushort id, int type=22) => new(id,id,"Spell/Trap fixture","",(uint)type<<20);
    private static Card Warrior() => new(77,77,"Warrior","",
        (15u<<20)|(4u<<25)|(100u<<9)|80u);
    private static Card?[] Units() => [Warrior(),null,null,null,null];
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<100 && q.IsBusy;i++) q.Tick();
        if(q.IsBusy) throw new InvalidOperationException("S/T wipe failed to drain.");
    }
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release {get;set;} = true;
        public bool CanComplete(QueuedDuelCommand command,CommandResult result) =>
            command.Command is not (DestroyCommand or ReturnToHandCommand) || Release;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(ushort id in new ushort[]{671,1061})
        foreach(bool set in new[]{false,true})
        {
            int enemy=1-side;
            var equip=Spell(300);var trap=Spell(1056,21);var field=Spell(332);
            var spell=Spell(id);var ownTrap=Spell(1057,21);var ownField=Spell(331);
            Card[] deck=[spell,ownTrap,ownField];
            var d=DemoDuel.FromPartialObservation(enemy,[equip,trap,field],Units(),Units(),8000,
                equipmentContextResolved:true,side0Deck:side==0?deck:null,side1Deck:side==1?deck:null);
            // Set up only registered occurrences through the actual APIs.
            check(d.Equip(0,side,0,out _) && d.SetFromHand(0,out _) &&
                d.SetFromHand(0,out _) && d.EndTurn(out _),
                "S/T wipe setup: opponent equipment, set trap and FIELD");
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            q.EnqueueBatch([new DrawCommand(side),new DrawCommand(side),new DrawCommand(side)]);
            Drain(q);
            check(d.TryQueueSpellTrapSet(1,out _),"Own trap Set");Drain(q);
            check(d.TryQueueSpellTrapSet(1,out _),"Own FIELD Set");Drain(q);
            int sourceIndex=0;
            if(set)
            {
                check(d.TryQueueSpellTrapSet(0,out _),"Wipe Set");Drain(q);
                sourceIndex=1;
            }
            int count=q.Trace.Count;
            check(d.TryQueueSpellTrapWipe(set?DuelZone.SpellTrap:DuelZone.Hand,sourceIndex,out _) &&
                d.Graveyard(side).Count==0 && d.EquipmentCards.Count==1 &&
                d.AttackValue(side,0)==1300,"S/T wipe admission is read-only");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==id && d.EquipmentCards.Count==1,
                "S/T wipe spends source before first destruction");
            gate.Release=false;q.Tick();q.Tick();
            check(d.EquipmentCards.Count==0 && d.AttackValue(side,0)==1000 &&
                d.Graveyard(enemy).Single().Id==300 && d.Spells[enemy,1] is not null &&
                q.IsBusy,"Equipment removal applies once while visual gate waits");
            gate.Release=true;Drain(q);
            var destroyed=q.Trace.Skip(count).Where(x=>x.Stage==ActionTraceStage.Executed &&
                x.Entry.Command is DestroyCommand).Select(x=>((DestroyCommand)x.Entry.Command).Target).ToArray();
            check(d.Graveyard(enemy).Select(c=>c.Id).SequenceEqual(new ushort[]{300,1056,332}) &&
                d.FieldSpells[enemy] is null && !d.FieldSpellFaceDown[enemy] &&
                d.Spells[enemy,1] is null && !d.SpellFaceDown[enemy,1] &&
                d.Monsters[0,0] is not null && d.Monsters[1,0] is not null &&
                destroyed.Take(3).Select(t=>(t.Side,t.Zone)).SequenceEqual(
                    new[]{(enemy,DuelZone.SpellTrap),(enemy,DuelZone.SpellTrap),(enemy,DuelZone.FieldSpell)}),
                "Opponent S/T slots then FIELD, no monster removal");
            bool storm=id==1061;
            check(destroyed.Length==(storm?5:3) &&
                (d.Spells[side,0] is null)==storm && (d.FieldSpells[side] is null)==storm &&
                d.Graveyard(side).Select(c=>c.Id).SequenceEqual(storm?
                    new ushort[]{id,1057,331}:new ushort[]{id}) &&
                d.LastMonsterWipe is null,
                $"S/T wipe {id}: own scope, source exclusion and correct graveyard order");
        }
        foreach(int side in new[]{0,1})
        foreach(bool set in new[]{false,true})
        {
            int enemy=1-side;
            Card[] deck=[Spell(1080),Spell(1057,21),Spell(331)];
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(300),Spell(1056,21),Spell(332)],
                Units(),Units(),8000,equipmentContextResolved:true,
                side0Deck:side==0?deck:null,side1Deck:side==1?deck:null);
            check(d.Equip(0,side,0,out _)&&d.SetFromHand(0,out _)&&
                d.SetFromHand(0,out _)&&d.EndTurn(out _),"Trunade setup");
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            q.EnqueueBatch([new DrawCommand(side),new DrawCommand(side),new DrawCommand(side)]);Drain(q);
            check(d.TryQueueSpellTrapSet(1,out _),"Trunade own trap");Drain(q);
            check(d.TryQueueSpellTrapSet(1,out _),"Trunade own FIELD");Drain(q);
            int index=0;
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Trunade Set");Drain(q);index=1;}
            check(!(set?d.ActivateSetSpell(index,out _):d.Play(index,out _)),
                "No legacy destruction route");
            check(d.TryCaptureCardTarget(enemy,DuelZone.SpellTrap,0,out var equip)&&
                d.TryCaptureCardTarget(side,DuelZone.SpellTrap,0,out var trap),"Capture return identities");
            int start=q.Trace.Count;
            check(d.TryQueueActivation(index,set,out _)&&d.EquipmentCards.Count==1,
                "Trunade generic admission is nonmutating");
            q.Tick();q.Tick();
            check(d.Graveyard(side).Single().Id==1080&&d.EquipmentCards.Count==1,
                "Spend source before return");
            gate.Release=false;q.Tick();q.Tick();
            check(d.EquipmentCards.Count==0&&d.AttackValue(side,0)==1000&&q.IsBusy&&
                d.Spells[enemy,1] is not null,"Return gate and equipment unlink");
            gate.Release=true;Drain(q);
            check(d.HandFor(enemy).Select(c=>c.Id).SequenceEqual(new ushort[]{300,1056,332})&&
                d.HandFor(side).Select(c=>c.Id).SequenceEqual(new ushort[]{1057,331})&&
                d.Graveyard(enemy).Count==0&&d.Graveyard(side).Single().Id==1080&&
                d.FieldSpells.All(c=>c is null)&&d.Spells.Cast<Card?>().All(c=>c is null)&&
                d.Monsters[0,0] is not null&&d.Monsters[1,0] is not null&&d.LastMonsterWipe is null,
                "All other S/T returned to their respective hands; monsters preserved");
            check(d.TryCaptureCardTarget(enemy,DuelZone.Hand,0,out var returnedEquip)&&returnedEquip.Occurrence==equip.Occurrence&&
                d.TryCaptureCardTarget(side,DuelZone.Hand,0,out var returnedTrap)&&returnedTrap.Occurrence==trap.Occurrence&&
                q.Trace.Skip(start).Count(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is ReturnToHandCommand)==5&&
                !q.Trace.Skip(start).Any(x=>x.Stage==ActionTraceStage.Executed&&x.Entry.Command is DestroyCommand),
                "Exactly one return per occurrence; never a destruction");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1080)],Units(),Units(),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueSpellTrapSet(0,out _),"Trunade lone Set");Drain(q);
            check(!d.TryQueueActivation(0,true,out _)&&!q.IsBusy,"Trunade excludes its own source");
        }
        foreach(bool staleSource in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(1,[Spell(1056,21)],Units(),Units(),8000,
                side0Deck:[Spell(1080)]);
            d.SetFromHand(0,out _);d.NextTurn();
            var q=d.CreatePrimitiveActionQueue();q.Enqueue(new DrawCommand(0));Drain(q);
            check(d.TryQueueSpellTrapSet(0,out _),"Trunade stale Set");Drain(q);
            check(d.TryQueueActivation(0,true,out _),"Trunade stale admitted");q.Tick();
            if(staleSource)d.Spells[0,0]=Spell(1080);
            else {q.Tick();d.Spells[1,0]=Spell(1056,21);}
            Drain(q);
            check(d.Spells[1,0]?.Id==1056&&d.HandFor(1).Count==0&&
                d.Graveyard(0).Count==(staleSource?0:1),"Trunade never returns replacement or spends stale source");
        }

        // Produce F and I using the actual legacy activation/End Phase,
        // before creating the queue. Both pieces are registered occurrences.
        var board=DemoDuel.FromPartialObservation(0,
            [Spell(DestinyBoardRules.BoardResourceId,21),Spell(1541)],Units(),Units(),8000,
            side1Deck:[Spell(671)]);
        bool boardSet=board.SetFromHand(0,out _);
        board.NextTurn();board.NextTurn();
        bool boardActivated=board.ActivateSetSpell(0,out _);
        board.NextTurn();board.NextTurn();
        check(boardSet && boardActivated && board.Spells[0,0]?.Id==1528 &&
            board.Spells[0,1]?.Id==1541 && !board.SpellFaceDown[0,1],
            "S/T wipe setup: active Destiny Board and a real placed message");
        board.NextTurn();
        var bq=board.CreatePrimitiveActionQueue();bq.Enqueue(new DrawCommand(1));Drain(bq);
        check(board.TryQueueSpellTrapWipe(DuelZone.Hand,0,out _),"Duster on Destiny Board admitted");
        int boardBefore=bq.Trace.Count;Drain(bq);
        check(board.Graveyard(0).Select(c=>c.Id).SequenceEqual(new ushort[]{1528,1541}) &&
            board.Spells[0,0] is null && board.Spells[0,1] is null &&
            bq.Trace.Skip(boardBefore).Count(x=>x.Stage==ActionTraceStage.Executed &&
                x.Entry.Command is DestroyCommand)==2,
            "Destiny Board and message each receive one destruction, no double cleanup");

        var lone=DemoDuel.FromPartialObservation(0,[Spell(1061)],Units(),Units(),8000);
        var lq=lone.CreatePrimitiveActionQueue();
        lone.TryQueueSpellTrapSet(0,out _);Drain(lq);
        check(!lone.TryQueueSpellTrapWipe(DuelZone.SpellTrap,0,out _) &&
            lone.Spells[0,0]?.Id==1061 && !lq.IsBusy,"Heavy Storm cannot count itself");

        var stale=DemoDuel.FromPartialObservation(1,[Spell(1056,21)],Units(),Units(),8000,
            side0Deck:[Spell(671)]);
        stale.SetFromHand(0,out _);stale.NextTurn();
        var sq=stale.CreatePrimitiveActionQueue();sq.Enqueue(new DrawCommand(0));Drain(sq);
        stale.TryQueueSpellTrapSet(0,out _);Drain(sq);
        check(stale.TryQueueSpellTrapWipe(DuelZone.SpellTrap,0,out _),"Stale source admitted");
        sq.Tick();stale.Spells[0,0]=Spell(671);Drain(sq);
        check(stale.Spells[1,0]?.Id==1056 && stale.Graveyard(0).Count==0 &&
            stale.Graveyard(1).Count==0,"Failed source spend cancels S/T wipe");

        var replaced=DemoDuel.FromPartialObservation(1,[Spell(1056,21)],Units(),Units(),8000,
            side0Deck:[Spell(671)]);
        replaced.SetFromHand(0,out _);replaced.NextTurn();
        var rq=replaced.CreatePrimitiveActionQueue();rq.Enqueue(new DrawCommand(0));Drain(rq);
        check(replaced.TryQueueSpellTrapWipe(DuelZone.Hand,0,out _),"Stale target admitted");
        rq.Tick();rq.Tick();
        replaced.Spells[1,0]=Spell(1056,21);Drain(rq);
        check(replaced.Spells[1,0]?.Id==1056 && replaced.Graveyard(1).Count==0 &&
            replaced.Graveyard(0).Single().Id==671,"Same-ID replacement is not destroyed");
    }
}
