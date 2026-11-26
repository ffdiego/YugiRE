namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Roles are not player numbers: 0 attacker, 1 defender. These are
    // reserved transient card records, NOT an additional action queue.
    private sealed record BattleReservation(CardTarget Target, int Slot, Card Card, bool Defense)
    {
        public bool Detached { get; set; }
        public bool WasFlipped { get; set; }
    }
    private readonly BattleReservation?[] queuedBattleRemoved = new BattleReservation?[2];
    private readonly HashSet<CardTarget> queuedBattleEquipmentRemovals = [];
    private readonly Queue<DuelCommand> queuedBattleLifeEffects = new();
    private sealed record BattleRevealReservation(BattleReservation Attacker, BattleReservation Defender, int Turn)
    {
        public bool FlipCalculated { get; set; }
        public bool FlipOpened { get; set; }
        public Dictionary<CardTarget,DuelZone> ExpectedEffectMoves { get; } = [];
    }
    private BattleRevealReservation? queuedBattleReveal;
    private bool queuedBattleRemovalSelectionOpening;
    private PendingCardSelection? queuedBattleRemovalSelectionRequest;

    // Preserve the original pilot IDs alongside the audited Normal catalog.
    private static bool IsSupportedBattleBystander(Card card) => card.IsMonster &&
        (card.Id is 24 or 1351 or FlipEffectRules.PrincessOfTsurugi ||
            NormalSummonRules.Category(card) == 0 && NormalMonsterCatalog.Contains(card.Id) ||
            CountedAttributeRules.ContainsKey(card.Id) || BattleDamageEffectRules.SupportsQueued(card.Id));

    private static bool HasBattleRemovalDeckSelection(Card card) => card.IsMonster &&
        (CardSelectionRules.BattleRecruitAttribute(card.Id) is not null ||
            CardSelectionRules.BattleSameNameRecruiter(card.Id) ||
            CardSelectionRules.OnFieldToGraveyardFilter(card.Id) is not null);

    private static bool IsOrdinaryBattlePilot(Card card) =>
        IsSupportedBattleBystander(card) || HasBattleRemovalDeckSelection(card) ||
        card.IsMonster && card.Id == CardSelectionRules.SpearCretin;

    private static bool IsBattleFlipPilot(Card card) => card.IsMonster &&
        card.Id is FlipEffectRules.Skelengel or FlipEffectRules.PenguinSoldier or FlipEffectRules.ManEaterBug
            or FlipEffectRules.HaneHane or FlipEffectRules.PrincessOfTsurugi;

    public bool TryQueueTargetedAttack(int attackerSlot, int defenderSlot, out string message,
        ActionOrigin origin = ActionOrigin.Player) =>
        TryQueueTargetedAttack(attackerSlot, defenderSlot, out message, out _, origin);

    private bool TryQueueTargetedAttack(int attackerSlot, int defenderSlot, out string message,
        out bool unsupported, ActionOrigin origin)
    {
        unsupported = false;
        message = "Choose a legal targeted attack with an idle queue.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !CanAttack(attackerSlot) ||
            !TryCaptureCardTarget(ActivePlayer,DuelZone.Monster,attackerSlot,out var attacker) ||
            !TryCaptureCardTarget(1-ActivePlayer,DuelZone.Monster,defenderSlot,out var defender)) return false;
        if (!TryPlanTargetedAttack(attacker,defender,out _,out _,out unsupported))
        {
            if (unsupported) message = "Targeted attack is outside the supported queued battle configurations.";
            return false;
        }
        queue.Enqueue(new DeclareTargetedAttackCommand(ActivePlayer,attacker,defender),origin);
        message="Targeted attack admitted.";
        return true;
    }

    private bool TryPlanTargetedAttack(CardTarget attacker,CardTarget defender,
        out BattleReservation a,out BattleReservation b,out bool unsupported,bool declared = false)
    {
        a=null!; b=null!;
        unsupported=false;
        int p=attacker.Side, e=defender.Side;
        if (p!=ActivePlayer || e!=1-p || attacker.Zone!=DuelZone.Monster || defender.Zone!=DuelZone.Monster ||
            GameOver || Phase!=DemoPhase.Battle || queuedBattleReveal is not null || queuedBattleRemoved.Any(x=>x is not null) ||
            !TryResolveCardTarget(attacker,out int s,out var ac) ||
            !TryResolveCardTarget(defender,out int t,out var bc) ||
            attacked[p,s] != declared || MonsterFaceDown[p,s] || DefensePosition[p,s])
            return false;
        unsupported = (MonsterFaceDown[e,t] && !DefensePosition[e,t]) ||
            !IsOrdinaryBattlePilot(ac) || !(IsOrdinaryBattlePilot(bc) ||
                IsBattleFlipPilot(bc) && MonsterFaceDown[e,t] && DefensePosition[e,t]) ||
            !IsSupportedBattleField(p,s,t);
        if (unsupported) return false;
        a=new(attacker,s,ac,false);
        b=new(defender,t,bc,DefensePosition[e,t]);
        return true;
    }

    private CommandResult ExecuteQueuedTargetedAttack(DeclareTargetedAttackCommand command,IActionCommandSink followups)
    {
        if(!TryResolveCardTarget(command.Attacker,out _,out _) || !TryResolveCardTarget(command.Defender,out _,out _))
            return new(CommandOutcome.Skipped,"Declared combatant gone.");
        if(followups is not IActionBatchSink { PendingCount:0 } queue ||
            !TryPlanTargetedAttack(command.Attacker,command.Defender,out var a,out var b,out _))
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
                    !IsSupportedBattleBystander(other)) return false;
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
            var defender = reserved.Defender.Target;
            queue.Enqueue(reserved.Defender.Card.Id switch
            {
                FlipEffectRules.Skelengel => new OpenBattleFlipDrawCommand(defender.Side, defender),
                FlipEffectRules.PrincessOfTsurugi => new ResolveBattleFlipDamageCommand(defender.Side, defender),
                _ => new OpenBattleFlipTargetsCommand(defender.Side, defender)
            });
        }
        else
        {
            queuedBattleReveal=null;
            QueuePlainBattleOutcome(reserved.Attacker,reserved.Defender,queue);
        }
        return new(CommandOutcome.Applied);
    }

    private void EnqueueOpposingSpellTrapDamage(int side, IActionCommandSink followups)
    {
        int count = SpellTrapWipeRules.CountOnSide(1 - side, Spells, FieldSpells);
        if (count > 0) followups.Enqueue(new DamageCommand(1 - side, count * 500));
    }

    private CommandResult ResolveQueuedBattleFlipDamage(ResolveBattleFlipDamageCommand command, IActionCommandSink followups)
    {
        if (queuedBattleReveal is not { FlipCalculated: true, FlipOpened: false } reserved ||
            reserved.Defender.Target != command.Defender || reserved.Defender.Card.Id != FlipEffectRules.PrincessOfTsurugi ||
            followups is not IActionBatchSink { PendingCount: 0 })
            return new(CommandOutcome.Rejected, "No matching calculated battle damage FLIP.");
        if (!ValidateBattleReveal(reserved, faceDown: false, allowEnded: true))
            throw new InvalidOperationException("Reserved battle FLIP changed before damage.");
        reserved.FlipOpened = true;
        if (!GameOver && !FlipEffectsNegated) EnqueueOpposingSpellTrapDamage(command.Side, followups);
        followups.Enqueue(new FinishBattleFlipCommand(command.Side, command.Defender));
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
        var effect = BattleEffectAfterDamage(delta < 0 ? b.Card : a.Card, 1 - damaged, amount);
        var removals = new List<DuelCommand>();
        if (removeA) removals.Add(new SnapshotBattleRemovalCommand(p, a.Target, 0));
        if (removeB) removals.Add(new SnapshotBattleRemovalCommand(e, b.Target, 1));
        if (effect is null && amount > 0) removals.Add(new DamageCommand(damaged, amount));
        if (removeA) PlanBattleRemoval(a, 0, removals);
        if (removeB) PlanBattleRemoval(b, 1, removals);
        if (effect is null)
            queue.EnqueueBatch(removals);
        else
        {
            queue.EnqueueBatch([new DamageCommand(damaged, amount), effect]);
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
        if (CardSelectionRules.BattleSameNameLifeChange(removed.Card.Id, removed.Target.Side) is { } lifeChange)
        {
            DuelCommand effect = lifeChange.Delta > 0
                ? new HealCommand(lifeChange.Side, lifeChange.Delta)
                : new DamageCommand(lifeChange.Side, -lifeChange.Delta);
            queuedBattleLifeEffects.Enqueue(effect);
            batch.Add(effect);
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
        r.WasFlipped = MonsterWasFlipped[command.Side, slot];
        ClearMonsterSlot(command.Side,slot,removeEquipment:false);
        r.Detached=true;
        // Existing visual event marks field disappearance, NOT GY arrival.
        MonsterDestroyed?.Invoke(new(command.Side,slot,card,false,r.Defense));
        return new(CommandOutcome.Applied) { BattleRemoval=new(r.Target,card.Id,slot,command.Role) };
    }

    private bool HasQueuedBattleRemovalSelectionChoice =>
        queuedBattleRemovalSelectionRequest is not null &&
        ReferenceEquals(PendingCardSelection,queuedBattleRemovalSelectionRequest);

    private CommandResult OpenQueuedBattleRemovalSelection()
    {
        if (!queuedBattleRemovalSelectionOpening || queuedBattleRemoved.Any(x=>x is not null) ||
            queuedBattleRemovalSelectionRequest is not null || PendingCardSelection is not null)
            return new(CommandOutcome.Rejected,"No pending selection after battle removal.");
        queuedBattleRemovalSelectionOpening=false;
        BeginNextQueuedDeckSearch();
        RefreshQueuedBattleRemovalSelectionRequest();
        return new(CommandOutcome.Applied);
    }

    private void ContinueQueuedGraveyardOrDeckSelection()
    {
        BeginNextQueuedDeckSearch();
        if (queuedBattleRemovalSelectionRequest is not null)
            RefreshQueuedBattleRemovalSelectionRequest();
    }

    private void RefreshQueuedBattleRemovalSelectionRequest()
    {
        queuedBattleRemovalSelectionRequest = PendingCardSelection is { } next &&
            (IsElementalRecruitment(next) || IsSpearCretinRevival(next) ||
                CardSelectionRules.BattleSameNameRecruiter(next.EffectResourceId) && IsSameNameRecruitment(next) ||
                UsesQueuedHandSelection(next) && CardSelectionRules.OnFieldToGraveyardFilter(next.EffectResourceId) is not null)
            ? next : null;
    }

    private CommandResult FinishQueuedBattleRemoval(FinishBattleRemovalCommand command,IActionCommandSink followups)
    {
        var r=queuedBattleRemoved[command.Role];
        if(r is null || r.Target!=command.Target || !r.Detached)
            return new(CommandOutcome.Rejected,"No detached combatant to send.");
        // Preserve the Card reference for effects triggered by its arrival.
        graves[command.Side].Add(r.Card);
        queuedBattleRemoved[command.Role]=null;
        if (!GameOver)
        {
            if (HasBattleRemovalDeckSelection(r.Card))
            {
                queuedDeckSearches.Enqueue((command.Side,r.Card,true,false,false));
                queuedBattleRemovalSelectionOpening = true;
            }
            if (QueueSpearCretinRevival(command.Side, r.Card, r.WasFlipped, destroyedByBattle: true))
                queuedBattleRemovalSelectionOpening = true;
        }
        // All reserved combatants must reach GY and finish their gates
        // before the first owner receives a selection request.
        if (queuedBattleRemovalSelectionOpening && queuedBattleRemoved.All(x=>x is null))
            followups.ScheduleContinuation(_ => new([new OpenBattleRecruitmentCommand(command.Side)]));
        return new(CommandOutcome.Applied) { BattleRemoval=new(r.Target,r.Card.Id,r.Slot,command.Role) };
    }
}
