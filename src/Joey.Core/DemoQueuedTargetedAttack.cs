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
    private readonly HashSet<CardTarget> queuedBattleEquipmentRemovals = [];
    private sealed record BattleRevealReservation(BattleReservation Attacker, BattleReservation Defender, int Turn)
    {
        public bool FlipCalculated { get; set; }
        public bool FlipOpened { get; set; }
        public Dictionary<CardTarget,DuelZone> ExpectedEffectMoves { get; } = [];
    }
    private BattleRevealReservation? queuedBattleReveal;
    private bool queuedBattleRecruitmentOpening;
    private PendingCardSelection? queuedBattleRecruitmentRequest;

    // Preserve the original pilot IDs alongside the audited Normal catalog.
    private static bool IsPlainBattlePilot(Card card) => card.IsMonster &&
        (card.Id is 24 or 1351 || NormalSummonRules.Category(card) == 0 && NormalMonsterCatalog.Contains(card.Id));

    private static bool IsElementalBattleRecruiter(Card card) => card.IsMonster &&
        CardSelectionRules.BattleRecruitAttribute(card.Id) is not null;

    private static bool IsOrdinaryBattlePilot(Card card) =>
        IsPlainBattlePilot(card) || IsElementalBattleRecruiter(card) ||
        card.IsMonster && BattleDamageEffectRules.SupportsDraw(card.Id);

    private static bool IsBattleFlipPilot(Card card) => card.IsMonster &&
        card.Id is FlipEffectRules.Skelengel or FlipEffectRules.PenguinSoldier or FlipEffectRules.ManEaterBug
            or FlipEffectRules.HaneHane;

    public bool TryQueueTargetedAttack(int attackerSlot, int defenderSlot, out string message,
        ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Targeted battle pilot requires supported combatants, with only supported normal monsters elsewhere on the field.";
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
        out BattleReservation a,out BattleReservation b,bool declared = false)
    {
        a=null!; b=null!;
        int p=attacker.Side, e=defender.Side;
        if (p!=ActivePlayer || e!=1-p || attacker.Zone!=DuelZone.Monster || defender.Zone!=DuelZone.Monster ||
            GameOver || Phase!=DemoPhase.Battle || queuedBattleReveal is not null || queuedBattleRemoved.Any(x=>x is not null) ||
            !TryResolveCardTarget(attacker,out int s,out var ac) ||
            !TryResolveCardTarget(defender,out int t,out var bc) ||
            attacked[p,s] != declared || MonsterFaceDown[p,s] || DefensePosition[p,s] || (MonsterFaceDown[e,t] && !DefensePosition[e,t]) ||
            !IsOrdinaryBattlePilot(ac) || !(IsOrdinaryBattlePilot(bc) ||
                IsBattleFlipPilot(bc) && MonsterFaceDown[e,t] && DefensePosition[e,t]))
            return false;
        if (!IsSupportedBattleField(p,s,t)) return false;
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
        if (HasQueuedAttackResponseTrap(command.Side))
        {
            queue.Enqueue(new OpenAttackResponseCommand(command.Side, command.Attacker, command.Defender,
                primitiveActionQueue?.Active?.Origin ?? ActionOrigin.Effect));
            return new(CommandOutcome.Applied, "Targeted attack declared; response follows.");
        }
        EnqueueTargetedBattleResolution(a, b, queue);
        return new(CommandOutcome.Applied) { TargetedAttack=new(a.Target,b.Target,a.Slot,b.Slot) };
    }

    private void EnqueueTargetedBattleResolution(BattleReservation a, BattleReservation b, IActionBatchSink queue)
    {
        if (MonsterFaceDown[b.Target.Side,b.Slot])
        {
            // Reveal is a separate operation after the declaration gate.
            // Never turn this into a Flip Summon or invoke its triggers.
            queuedBattleReveal=new(a,b,Turn);
            queue.Enqueue(new RevealBattleDefenderCommand(b.Target.Side,a.Target,b.Target));
        }
        else QueuePlainBattleOutcome(a,b,queue);
    }

    private bool IsSupportedBattleField(int attackerSide,int source,int target)
    {
        if (!equipment.All(IsTrackedBattleEquipment)) return false;
        for (int side=0;side<2;side++)
        {
            if (!IsSupportedQueuedBattleFieldSpell(side)) return false;
            for (int z=0;z<5;z++)
            {
                if (!IsSupportedQueuedBattleBackRow(side,z)) return false;
                if (z != (side == attackerSide ? source : target) && Monsters[side,z] is { } other &&
                    !IsPlainBattlePilot(other)) return false;
            }
        }
        return true;
    }

    private bool ValidateBattleReveal(BattleRevealReservation reserved,bool faceDown,bool allowEnded=false)
    {
        var a=reserved.Attacker; var b=reserved.Defender;
        int p=a.Target.Side,e=b.Target.Side;
        return Turn==reserved.Turn && ActivePlayer==p && Phase==DemoPhase.Battle && (allowEnded || !GameOver) &&
            TryResolveCardTarget(a.Target,out int source,out var ac) && source==a.Slot && ReferenceEquals(ac,a.Card) &&
            TryResolveCardTarget(b.Target,out int target,out var bc) && target==b.Slot && ReferenceEquals(bc,b.Card) &&
            attacked[p,source] && !MonsterFaceDown[p,source] && !DefensePosition[p,source] &&
            MonsterFaceDown[e,target]==faceDown && DefensePosition[e,target] &&
            IsOrdinaryBattlePilot(ac) && (IsOrdinaryBattlePilot(bc) || IsBattleFlipPilot(bc)) &&
            IsSupportedBattleField(p,source,target);
    }

    private CommandResult RevealQueuedBattleDefender(RevealBattleDefenderCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { } reserved || reserved.Attacker.Target!=command.Attacker ||
            reserved.Defender.Target!=command.Defender || followups is not IActionBatchSink { PendingCount:0 } queue)
            return new(CommandOutcome.Rejected,"No matching reserved battle reveal.");
        if (!ValidateBattleReveal(reserved,faceDown:true))
            throw new InvalidOperationException("Reserved battle reveal changed.");
        int slot=reserved.Defender.Slot;
        MonsterFaceDown[command.Side,slot]=false;
        MonsterWasFlipped[command.Side,slot]=true;
        queue.Enqueue(new ResolveRevealedBattleCommand(command.Attacker.Side,command.Attacker,command.Defender));
        return new(CommandOutcome.Applied);
    }

    private CommandResult ResolveQueuedRevealedBattle(ResolveRevealedBattleCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { } reserved || reserved.Attacker.Target!=command.Attacker ||
            reserved.Defender.Target!=command.Defender || followups is not IActionBatchSink { PendingCount:0 } queue)
            return new(CommandOutcome.Rejected,"No matching reserved revealed battle.");
        if (!ValidateBattleReveal(reserved,faceDown:false))
            throw new InvalidOperationException("Reserved revealed combatant changed.");
        if (IsBattleFlipPilot(reserved.Defender.Card))
        {
            // In defense, positive delta marks the defender; negative delta
            // damages the attacker. Keep the marked card present for FLIP.
            int delta=AttackValue(reserved.Attacker.Target.Side,reserved.Attacker.Slot) -
                DefenseValue(reserved.Defender.Target.Side,reserved.Defender.Slot);
            reserved.FlipCalculated=true;
            queuedBattleRemoved[1]=delta>0?reserved.Defender:null;
            if(delta<0) queue.Enqueue(new DamageCommand(command.Side,Math.Min(-delta,ushort.MaxValue)));
            queue.Enqueue(reserved.Defender.Card.Id==FlipEffectRules.Skelengel
                ? new OpenBattleFlipDrawCommand(reserved.Defender.Target.Side,reserved.Defender.Target)
                : new OpenBattleFlipTargetsCommand(reserved.Defender.Target.Side,reserved.Defender.Target));
        }
        else
        {
            queuedBattleReveal=null;
            QueuePlainBattleOutcome(reserved.Attacker,reserved.Defender,queue);
        }
        return new(CommandOutcome.Applied);
    }

    private static DemoFlipEffect? BattleTargetEffect(Card card) => card.Id switch
    {
        FlipEffectRules.PenguinSoldier => DemoFlipEffect.ReturnUpToTwo,
        FlipEffectRules.HaneHane => DemoFlipEffect.ReturnOne,
        FlipEffectRules.ManEaterBug => DemoFlipEffect.DestroyMonster,
        _ => null
    };

    private bool HasQueuedBattleTargetChoice =>
        queuedBattleReveal is { FlipCalculated:true, FlipOpened:true } reserved &&
        BattleTargetEffect(reserved.Defender.Card) is { } effect &&
        PendingFlipTarget is { } pending && pending.Effect==effect &&
        pending.Owner==reserved.Defender.Target.Side && pending.SourceZone==reserved.Defender.Slot &&
        ReferenceEquals(pending.SourceCard,reserved.Defender.Card);

    private CommandResult OpenQueuedBattleFlipTargets(OpenBattleFlipTargetsCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { FlipCalculated:true, FlipOpened:false } reserved ||
            reserved.Defender.Target!=command.Defender || BattleTargetEffect(reserved.Defender.Card) is not { } effect ||
            followups is not IActionBatchSink { PendingCount:0 } ||
            PendingFlipTarget is not null)
            return new(CommandOutcome.Rejected,"No matching calculated battle target FLIP.");
        if (!ValidateBattleReveal(reserved,faceDown:false,allowEnded:true))
            throw new InvalidOperationException("Reserved battle FLIP changed before targets.");
        reserved.FlipOpened=true;
        if (GameOver || FlipEffectsNegated)
            followups.Enqueue(new FinishBattleFlipCommand(command.Side,command.Defender));
        else
            PendingFlipTarget=new(command.Side,reserved.Defender.Slot,reserved.Defender.Card,effect);
        return new(CommandOutcome.Applied);
    }

    private bool ValidateFinishedBattleEffectMoves(BattleRevealReservation reserved)
    {
        if (Turn!=reserved.Turn || ActivePlayer!=reserved.Attacker.Target.Side || Phase!=DemoPhase.Battle ||
            !IsSupportedBattleField(reserved.Attacker.Target.Side,reserved.Attacker.Slot,reserved.Defender.Slot))
            return false;
        foreach (var (target, destination) in reserved.ExpectedEffectMoves)
            if (!TryResolveCardTarget(target with { Zone = destination }, out _, out _)) return false;

        foreach (var combatant in new[]{reserved.Attacker,reserved.Defender})
        {
            int side=combatant.Target.Side,slot=combatant.Slot;
            if (reserved.ExpectedEffectMoves.ContainsKey(combatant.Target))
            {
                if (Monsters[side,slot] is not null) return false;
            }
            else if (!TryResolveCardTarget(combatant.Target,out int index,out var card) ||
                index!=slot || !ReferenceEquals(card,combatant.Card) || MonsterFaceDown[side,slot] ||
                DefensePosition[side,slot]!=combatant.Defense ||
                (combatant==reserved.Attacker && !attacked[side,slot]))
                return false;
        }
        return true;
    }

    private bool IsQueuedBattleMarked(int side,int slot) =>
        queuedBattleReveal is { FlipCalculated:true } reserved &&
        queuedBattleRemoved[1] is { } marked && marked.Target==reserved.Defender.Target &&
        marked.Target.Side==side && marked.Slot==slot &&
        ReferenceEquals(Monsters[side,slot],marked.Card);

    private CommandResult FinishQueuedBattleFlip(FinishBattleFlipCommand command,IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { FlipCalculated:true, FlipOpened:true } reserved ||
            reserved.Defender.Target!=command.Defender || followups is not IActionBatchSink { PendingCount:0 } queue)
            return new(CommandOutcome.Rejected,"No matching opened battle FLIP.");
        if (!(BattleTargetEffect(reserved.Defender.Card) is not null
            ? ValidateFinishedBattleEffectMoves(reserved)
            : ValidateBattleReveal(reserved,faceDown:false,allowEnded:true)))
            throw new InvalidOperationException("Reserved FLIP combatants changed before finish.");
        // A combatant destroyed by its own FLIP already reached the GY
        // by effect. Do not emit a second removal or a battle trigger.
        if (reserved.ExpectedEffectMoves.TryGetValue(reserved.Defender.Target,out var destination) &&
            destination==DuelZone.Graveyard)
            queuedBattleRemoved[1]=null;
        queuedBattleReveal=null;
        if (queuedBattleRemoved[1] is { } marked)
        {
            var batch = new List<DuelCommand> { new SnapshotBattleRemovalCommand(command.Side, marked.Target, 1) };
            PlanBattleRemoval(marked, 1, batch);
            queue.EnqueueBatch(batch);
        }
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
        amount = Math.Min(amount, ushort.MaxValue);
        var draw = BattleDrawAfterDamage(delta < 0 ? b.Card : a.Card, 1 - damaged, amount);
        var removals = new List<DuelCommand>();
        if (removeA) removals.Add(new SnapshotBattleRemovalCommand(p, a.Target, 0));
        if (removeB) removals.Add(new SnapshotBattleRemovalCommand(e, b.Target, 1));
        if (draw is null && amount > 0) removals.Add(new DamageCommand(damaged, amount));
        if (removeA) PlanBattleRemoval(a, 0, removals);
        if (removeB) PlanBattleRemoval(b, 1, removals);
        if (draw is null)
            queue.EnqueueBatch(removals);
        else
        {
            queue.EnqueueBatch([new DamageCommand(damaged, amount), draw]);
            if (removals.Count != 0) queue.ScheduleContinuation(_ => new(removals));
        }
        queuedBattleRemoved[0]=removeA?a:null;
        queuedBattleRemoved[1]=removeB?b:null;
    }

    private void PlanBattleRemoval(BattleReservation removed, int role, List<DuelCommand> batch)
    {
        if (!TryPlanRemovalLinks(removed.Target.Side, removed.Slot, out var links))
            throw new InvalidOperationException("Reserved battle removal links changed.");
        batch.Add(new FinishBattleRemovalCommand(removed.Target.Side, removed.Target, role));
        foreach (var link in links)
        {
            if (link is not DestroyCommand { Target.Zone: DuelZone.SpellTrap } equipmentDestroy)
                throw new InvalidOperationException("Unexpected link in the ordinary battle pilot.");
            queuedBattleEquipmentRemovals.Add(equipmentDestroy.Target);
            batch.Add(equipmentDestroy);
        }
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

    private bool HasQueuedBattleRecruitmentChoice =>
        queuedBattleRecruitmentRequest is not null &&
        ReferenceEquals(PendingCardSelection,queuedBattleRecruitmentRequest);

    private CommandResult OpenQueuedBattleRecruitment()
    {
        if (!queuedBattleRecruitmentOpening || queuedBattleRemoved.Any(x=>x is not null) ||
            queuedBattleRecruitmentRequest is not null || PendingCardSelection is not null)
            return new(CommandOutcome.Rejected,"No completed battle recruitment batch.");
        queuedBattleRecruitmentOpening=false;
        BeginNextQueuedDeckSearch();
        RefreshQueuedBattleRecruitmentRequest();
        return new(CommandOutcome.Applied);
    }

    private void RefreshQueuedBattleRecruitmentRequest()
    {
        // The restricted battle admits only P164 post-battle producers.
        queuedBattleRecruitmentRequest=PendingCardSelection is { } next &&
            IsElementalRecruitment(next) ? next : null;
    }

    private CommandResult FinishQueuedBattleRemoval(FinishBattleRemovalCommand command,IActionCommandSink followups)
    {
        var r=queuedBattleRemoved[command.Role];
        if(r is null || r.Target!=command.Target || !r.Detached)
            return new(CommandOutcome.Rejected,"No detached combatant to send.");
        // Preserve the Card reference. Only a real battle arrival can
        // produce P164; effect DestroyCommand never uses this path.
        graves[command.Side].Add(r.Card);
        queuedBattleRemoved[command.Role]=null;
        if (!GameOver && IsElementalBattleRecruiter(r.Card))
        {
            queuedDeckSearches.Enqueue((command.Side,r.Card,true,false,false));
            queuedBattleRecruitmentOpening=true;
        }
        // All reserved combatants must reach GY and finish their gates
        // before the first owner receives a recruitment request.
        if (queuedBattleRecruitmentOpening && queuedBattleRemoved.All(x=>x is null))
            followups.ScheduleContinuation(_ => new([new OpenBattleRecruitmentCommand(command.Side)]));
        return new(CommandOutcome.Applied) { BattleRemoval=new(r.Target,r.Card.Id,r.Slot,command.Role) };
    }
}
