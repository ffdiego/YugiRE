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
    private static CardTarget Capture(DemoDuel d,int side,DuelZone zone,int slot)
    {
        if(!d.TryCaptureCardTarget(side,zone,slot,out var target)) throw new InvalidOperationException("Untracked Final Destiny fixture.");
        return target;
    }

    private static void CheckFinalDestiny(Action<bool,string> check)
    {
        foreach(int side in new[]{0,1})
        foreach(bool set in new[]{false,true})
        {
            int enemy=1-side;
            // Six hand cards besides the source; two more drawn for own
            // Set/FIELD setup. Duplicate resource IDs are separate occurrences.
            Card[] deck=[Spell(1067),Spell(300),Spell(300),Spell(300),Spell(300),
                Spell(300),Spell(301),Spell(1057,21),Spell(331)];
            var d=DemoDuel.FromPartialObservation(enemy,[Spell(300),Spell(1056,21),Spell(332)],
                Units(),Units(),8000,equipmentContextResolved:true,
                side0Deck:side==0?deck:null,side1Deck:side==1?deck:null);
            check(d.Equip(0,side,0,out _)&&d.SetFromHand(0,out _)&&d.SetFromHand(0,out _)&&d.EndTurn(out _),
                "Final Destiny enemy equip/trap/FIELD setup");
            var gate=new Gate();var q=d.CreatePrimitiveActionQueue(gate);
            q.EnqueueBatch(Enumerable.Range(0,deck.Length).Select(_=>(DuelCommand)new DrawCommand(side)));Drain(q);
            check(d.TryQueueSpellTrapSet(7,out _),"Final Destiny own trap");Drain(q);
            check(d.TryQueueSpellTrapSet(7,out _),"Final Destiny own FIELD");Drain(q);
            if(set){check(d.TryQueueSpellTrapSet(0,out _),"Final Destiny Set");Drain(q);}
            d.MonsterFaceDown[enemy,0]=true;
            int sourceSlot=set?1:0;
            var source=Capture(d,side,set?DuelZone.SpellTrap:DuelZone.Hand,sourceSlot);
            var choices=d.QueuedFinalDestinyCosts(source);
            var selected=choices.Where(c=>c.Card.Id==300).Select(c=>c.Target).ToArray();
            check(choices.Count==6&&selected.Length==5&&selected.Distinct().Count()==5&&
                !d.TryQueueFinalDestiny(source,selected.Take(4).ToArray(),out _)&&
                !d.TryQueueFinalDestiny(source,Enumerable.Repeat(selected[0],5).ToArray(),out _)&&
                !d.TryQueueFinalDestiny(source,[source,.. selected.Take(4)],out _)&&
                !(set?d.ActivateSetSpell(sourceSlot,out _):d.Play(sourceSlot,out _))&&
                !q.IsBusy,"Final Destiny exact distinct costs, excludes source; no legacy bypass");
            int before=q.Trace.Count;
            int discards=0;d.HandTransferred+=_=>discards++;
            check(d.TryQueueFinalDestiny(source,selected,out _)&&d.Graveyard(side).Count==0,
                "Final Destiny admission is read-only");
            q.Tick();
            for(int i=1;i<=5;i++){q.Tick();check(discards==i&&d.Graveyard(side).Count==i&&
                d.Monsters[0,0] is not null&&d.Monsters[1,0] is not null,"Each discard precedes field removal");}
            q.Tick();
            check(d.Graveyard(side).Count==6&&d.Graveyard(side).Last().Id==1067&&discards==5,
                "Source spend is not another discard");
            q.Tick();gate.Release=false;q.Tick();q.Tick();
            check(q.IsBusy&&d.Monsters[0,0] is null&&d.Monsters[1,0] is not null,"First destruction waits at gate");
            gate.Release=true;Drain(q);
            var executed=q.Trace.Skip(before).Where(x=>x.Stage==ActionTraceStage.Executed).ToArray();
            var destroys=executed.Where(x=>x.Entry.Command is DestroyCommand).ToArray();
            check(destroys.Length==7&&destroys.All(x=>x.Result?.Outcome==CommandOutcome.Applied)&&
                destroys.Select(x=>((DestroyCommand)x.Entry.Command).Target).Distinct().Count()==7&&
                d.Monsters.Cast<Card?>().All(c=>c is null)&&d.Spells.Cast<Card?>().All(c=>c is null)&&
                d.FieldSpells.All(c=>c is null)&&d.EquipmentCards.Count==0&&
                d.Graveyard(enemy).Count(c=>c.Id==300)==1&&d.HandFor(side).Single().Id==301,
                "All seven field occurrences destroyed once, linked equip not duplicated, unselected hand stays");
        }
        foreach(bool afterFirst in new[]{false,true})
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1067),Spell(300),Spell(300),Spell(300),Spell(300),Spell(300)],
                Units(),Units(),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueSpellTrapSet(0,out _),"Stale Final Destiny Set");Drain(q);
            var source=Capture(d,0,DuelZone.SpellTrap,0);
            var costs=d.QueuedFinalDestinyCosts(source).Select(c=>c.Target).ToArray();
            check(d.TryQueueFinalDestiny(source,costs,out _),"Stale Final Destiny admission");q.Tick();
            if(afterFirst)q.Tick();
            d.Spells[0,0]=Spell(1067);
            Drain(q);
            check(d.Graveyard(0).Count==(afterFirst?1:0)&&d.Hand.Count==(afterFirst?4:5)&&
                d.Monsters[0,0] is not null&&d.Monsters[1,0] is not null,
                "Invalid source stops remaining costs, never wipes or refunds an applied discard");
        }
        {
            var d=DemoDuel.FromPartialObservation(0,[Spell(1067),Spell(300),Spell(300),Spell(300),Spell(300)],
                Units(),Units(),8000);
            var q=d.CreatePrimitiveActionQueue();
            check(d.QueuedFinalDestinyCosts(Capture(d,0,DuelZone.Hand,0)).Count==0&&
                !d.TryQueueActivation(0,false,out _)&&!q.IsBusy,"Five other cards required");
        }
        {
            var sangan=new Card(47,47,"Sangan","",(3u<<20)|(3u<<25)|(100u<<9));
            var d=DemoDuel.FromPartialObservation(0,[Spell(1067),Spell(300),Spell(300),Spell(300),Spell(300),Spell(300)],
                [sangan,null,null,null,null],Units(),8000,side0Deck:[Warrior()]);
            var q=d.CreatePrimitiveActionQueue();
            var source=Capture(d,0,DuelZone.Hand,0);
            check(d.TryQueueFinalDestiny(source,d.QueuedFinalDestinyCosts(source).Select(c=>c.Target).ToArray(),out _),
                "Final Destiny Sangan trigger admitted");
            Drain(q);
            check(d.Monsters.Cast<Card?>().All(c=>c is null)&&d.PendingCardSelection is {Owner:0,EffectResourceId:47},
                "All field removals finish before Sangan search opens");
        }
    }

    public static void Run(Action<bool,string> check)
    {
        CheckFinalDestiny(check);
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
            bool equipCaptured=d.TryCaptureCardTarget(enemy,DuelZone.SpellTrap,0,out var equip);
            bool trapCaptured=d.TryCaptureCardTarget(side,DuelZone.SpellTrap,0,out var trap);
            check(equipCaptured&&trapCaptured,"Capture return identities");
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

        var returnBoard=DemoDuel.FromPartialObservation(0,
            [Spell(DestinyBoardRules.BoardResourceId,21),Spell(1541)],Units(),Units(),8000,
            side1Deck:[Spell(1080)]);
        returnBoard.SetFromHand(0,out _);
        returnBoard.NextTurn();returnBoard.NextTurn();
        check(returnBoard.ActivateSetSpell(0,out _),"Trunade Board activation");
        returnBoard.NextTurn();returnBoard.NextTurn();returnBoard.NextTurn();
        var tbq=returnBoard.CreatePrimitiveActionQueue();tbq.Enqueue(new DrawCommand(1));Drain(tbq);
        check(returnBoard.TryQueueActivation(0,false,out _),"Trunade on Board admitted");
        Drain(tbq);
        check(returnBoard.Graveyard(0).Count==0&&
            returnBoard.HandFor(0).Select(c=>c.Id).Order().SequenceEqual(new ushort[]{1528,1541})&&
            returnBoard.Spells[0,0] is null&&returnBoard.Spells[0,1] is null,
            "Mass return moves both Board pieces without selective destruction cascade");

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
