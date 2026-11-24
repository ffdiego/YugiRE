namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Roles are not player numbers: 0 attacker, 1 defender. These are
    // reserved transient card records, NOT an additional action queue.
    private sealed record BattleReservation(CardTarget Target, int Slot, Card Card, bool Defense)
    {
        public bool Detached { get; set; }
    }
    private readonly BattleReservation?[] queuedBattleRemoved = new BattleReservation?[2];
    private (CardTarget Attacker, CardTarget Defender)? queuedBattleReveal;

    // Explicit first pilot IDs without callback rows in the cumulative RE DB.
    // Not a general normal-monster classifier: the DAT has no proven bit for it.
    private static bool IsPlainBattlePilot(Card card) => card.IsMonster && card.Id is 24 or 1351;

    public bool TryQueueTargetedAttack(int attackerSlot, int defenderSlot, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Targeted battle pilot requires a supported face-up attacker and supported defender and no other field cards.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !CanAttack(attackerSlot) ||
            !TryCaptureCardTarget(ActivePlayer,DuelZone.Monster,attackerSlot,out var attacker) ||
            !TryCaptureCardTarget(1-ActivePlayer,DuelZone.Monster,defenderSlot,out var defender) ||
            !TryPlanTargetedAttack(attacker,defender,out _,out _)) return false;
        queue.Enqueue(new DeclareTargetedAttackCommand(ActivePlayer,attacker,defender),origin);
        message="Targeted attack admitted.";
        return true;
    }

    private bool TryPlanTargetedAttack(CardTarget attacker,CardTarget defender,
        out BattleReservation a,out BattleReservation b)
    {
        a=null!; b=null!;
        int p=attacker.Side, e=defender.Side;
        if (p!=ActivePlayer || e!=1-p || attacker.Zone!=DuelZone.Monster || defender.Zone!=DuelZone.Monster ||
            GameOver || Phase!=DemoPhase.Battle || queuedBattleReveal is not null || queuedBattleRemoved.Any(x=>x is not null) ||
            !TryResolveCardTarget(attacker,out int s,out var ac) ||
            !TryResolveCardTarget(defender,out int t,out var bc) ||
            attacked[p,s] || MonsterFaceDown[p,s] || DefensePosition[p,s] || MonsterFaceDown[e,t] && !DefensePosition[e,t] ||
            !IsPlainBattlePilot(ac) || !IsPlainBattlePilot(bc) || equipment.Count!=0)
            return false;
        for(int side=0;side<2;side++)
        {
            if(FieldSpells[side] is not null) return false;
            for(int z=0;z<5;z++)
                if(Spells[side,z] is not null || z!=(side==p?s:t) && Monsters[side,z] is not null)
                    return false;
        }
        a=new(attacker,s,ac,false);
        b=new(defender,t,bc,DefensePosition[e,t]);
        return true;
    }

    private CommandResult ExecuteQueuedTargetedAttack(DeclareTargetedAttackCommand command,IActionCommandSink followups)
    {
        if(!TryResolveCardTarget(command.Attacker,out _,out _) || !TryResolveCardTarget(command.Defender,out _,out _))
            return new(CommandOutcome.Skipped,"Declared combatant gone.");
        if(followups is not IActionBatchSink { PendingCount:0 } queue ||
            !TryPlanTargetedAttack(command.Attacker,command.Defender,out var a,out var b))
            return new(CommandOutcome.Rejected,"Targeted battle no longer satisfies pilot restrictions.");

        attacked[a.Target.Side,a.Slot]=true;
        if (MonsterFaceDown[b.Target.Side,b.Slot])
        {
            // Reveal is a separate operation after the declaration gate.
            // Never turn this into a Flip Summon or invoke its triggers.
            queuedBattleReveal=(a.Target,b.Target);
            queue.Enqueue(new RevealBattleDefenderCommand(b.Target.Side,a.Target,b.Target));
        }
        else QueuePlainBattleOutcome(a,b,queue);
        return new(CommandOutcome.Applied) { TargetedAttack=new(a.Target,b.Target,a.Slot,b.Slot) };
    }

    private CommandResult RevealQueuedBattleDefender(RevealBattleDefenderCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { } reserved || reserved.Attacker!=command.Attacker ||
            reserved.Defender!=command.Defender || followups is not IActionBatchSink { PendingCount:0 } queue)
            return new(CommandOutcome.Rejected,"No matching reserved battle reveal.");
        if (!TryResolveCardTarget(command.Defender,out int slot,out var card) ||
            !TryResolveCardTarget(command.Attacker,out int source,out _) ||
            !attacked[command.Attacker.Side,source] || !MonsterFaceDown[command.Side,slot] ||
            !DefensePosition[command.Side,slot] || !IsPlainBattlePilot(card))
            throw new InvalidOperationException("Reserved battle reveal changed.");
        MonsterFaceDown[command.Side,slot]=false;
        MonsterWasFlipped[command.Side,slot]=true;
        queue.Enqueue(new ResolveRevealedBattleCommand(command.Attacker.Side,command.Attacker,command.Defender));
        return new(CommandOutcome.Applied);
    }

    private CommandResult ResolveQueuedRevealedBattle(ResolveRevealedBattleCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { } reserved || reserved.Attacker!=command.Attacker ||
            reserved.Defender!=command.Defender || followups is not IActionBatchSink { PendingCount:0 } queue)
            return new(CommandOutcome.Rejected,"No matching reserved revealed battle.");
        if (!TryResolveCardTarget(command.Attacker,out int source,out var attacker) ||
            !TryResolveCardTarget(command.Defender,out int target,out var defender) ||
            !attacked[command.Side,source] || MonsterFaceDown[command.Defender.Side,target] ||
            !DefensePosition[command.Defender.Side,target] ||
            !IsPlainBattlePilot(attacker) || !IsPlainBattlePilot(defender))
            throw new InvalidOperationException("Reserved revealed combatant changed.");
        queuedBattleReveal=null;
        QueuePlainBattleOutcome(new(command.Attacker,source,attacker,false),
            new(command.Defender,target,defender,true),queue);
        return new(CommandOutcome.Applied);
    }

    private void QueuePlainBattleOutcome(BattleReservation a,BattleReservation b,IActionBatchSink queue)
    {
        int p=a.Target.Side,e=b.Target.Side;
        int atk=AttackValue(p,a.Slot);
        int other=b.Defense?DefenseValue(e,b.Slot):AttackValue(e,b.Slot);
        int delta=atk-other;
        bool removeA=!b.Defense && (delta<0 || delta==0 && atk>0);
        bool removeB=delta>0 || !b.Defense && delta==0 && atk>0;
        int amount=delta<0 ? -delta : !b.Defense ? delta : 0;
        int damaged=delta<0?p:e;
        var batch=new List<DuelCommand>();
        if(removeA) batch.Add(new SnapshotBattleRemovalCommand(p,a.Target,0));
        if(removeB) batch.Add(new SnapshotBattleRemovalCommand(e,b.Target,1));
        if(amount>0) batch.Add(new DamageCommand(damaged,Math.Min(amount,ushort.MaxValue)));
        if(removeA) batch.Add(new FinishBattleRemovalCommand(p,a.Target,0));
        if(removeB) batch.Add(new FinishBattleRemovalCommand(e,b.Target,1));
        queue.EnqueueBatch(batch);
        queuedBattleRemoved[0]=removeA?a:null;
        queuedBattleRemoved[1]=removeB?b:null;

    }

    private CommandResult SnapshotQueuedBattleRemoval(SnapshotBattleRemovalCommand command)
    {
        var r=queuedBattleRemoved[command.Role];
        if(r is null || r.Target!=command.Target || r.Detached)
            return new(CommandOutcome.Rejected,"No matching reserved battle removal.");
        if(!TryResolveCardTarget(r.Target,out int slot,out var card) ||
            slot!=r.Slot || !ReferenceEquals(card,r.Card))
            throw new InvalidOperationException("Reserved combatant changed before snapshot.");
        ClearMonsterSlot(command.Side,slot,removeEquipment:false);
        r.Detached=true;
        // Existing visual event marks field disappearance, NOT GY arrival.
        MonsterDestroyed?.Invoke(new(command.Side,slot,card,false,r.Defense));
        return new(CommandOutcome.Applied) { BattleRemoval=new(r.Target,card.Id,slot,command.Role) };
    }

    private CommandResult FinishQueuedBattleRemoval(FinishBattleRemovalCommand command)
    {
        var r=queuedBattleRemoved[command.Role];
        if(r is null || r.Target!=command.Target || !r.Detached)
            return new(CommandOutcome.Rejected,"No detached combatant to send.");
        // This narrow pilot has no field-to-GY callbacks, tokens, linked
        // effects or controller/owner changes. Preserve the Card reference.
        graves[command.Side].Add(r.Card);
        queuedBattleRemoved[command.Role]=null;
        return new(CommandOutcome.Applied) { BattleRemoval=new(r.Target,r.Card.Id,r.Slot,command.Role) };
    }
}
