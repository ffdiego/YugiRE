namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Admit an attack against a field slot or -1 for a direct attack.
    /// Both pilots preserve their individual eligibility and revalidation.</summary>
    public bool TryQueueAttack(int attackerSlot, int defenderSlot, out string message,
        ActionOrigin origin = ActionOrigin.Player) =>
        TryQueueAttack(attackerSlot, defenderSlot, out message, out _, origin);

    /// <summary>Only a legal declaration outside the queue's supported battle
    /// configurations may be routed to the compatibility resolver.</summary>
    public bool TryQueueAttack(int attackerSlot, int defenderSlot, out string message,
        out bool unsupported, ActionOrigin origin = ActionOrigin.Player) => defenderSlot == -1
            ? TryQueueDirectAttack(attackerSlot, out message, out unsupported, origin)
            : TryQueueTargetedAttack(attackerSlot, defenderSlot, out message, out unsupported, origin);

    /// <summary>Opt-in direct battle pilot. An eligible response can coexist
    /// with other supported normal monsters on the attacker's field.</summary>
    public bool TryQueueDirectAttack(int slot, out string message, ActionOrigin origin = ActionOrigin.Player) =>
        TryQueueDirectAttack(slot, out message, out _, origin);

    private bool TryQueueDirectAttack(int slot, out string message, out bool unsupported, ActionOrigin origin)
    {
        unsupported = false;
        message = "Choose a legal direct attack with an idle queue.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !CanAttackDirectly(slot) ||
            !TryCaptureCardTarget(ActivePlayer,DuelZone.Monster,slot,out var target)) return false;
        if (!TryPlanQueuedDirectAttack(target,out _,out _,out _,out unsupported))
        {
            if (unsupported) message = "Direct attack is outside the supported queued battle configurations.";
            return false;
        }
        queue.Enqueue(new DeclareDirectAttackCommand(ActivePlayer,target),origin);
        message = "Direct attack admitted.";
        return true;
    }

    private bool TryPlanQueuedDirectAttack(CardTarget source,out int slot,out Card card,out int damage,
        out bool unsupported, bool declared = false)
    {
        damage=0;
        unsupported=false;
        if (!TryResolveCardTarget(source,out slot,out card)) return false;
        int side=source.Side;
        if (source.Zone!=DuelZone.Monster || side!=ActivePlayer || Phase!=DemoPhase.Battle ||
            GameOver || attacked[side,slot] != declared || MonsterFaceDown[side,slot] || DefensePosition[side,slot] ||
            !card.IsMonster || !DirectAttackAllowed(side,card))
            return false;
        unsupported = !SupportsQueuedDirectAttack(side, slot, card);
        if (unsupported) return false;
        damage=Math.Clamp(AttackValue(side,slot),0,ushort.MaxValue);
        return true;
    }

    private bool SupportsQueuedDirectAttack(int side, int slot, Card card)
    {
        if (BattleDamageEffectRules.AfterBattleDamage(card.Id,side,1-side,1,false) is not null &&
            !BattleDamageEffectRules.SupportsDraw(card.Id)) return false;
        // Mirror Force already schedules linked removal through the wipe executor.
        if (!equipment.All(IsTrackedBattleEquipment)) return false;
        for(int p=0;p<2;p++)
        {
            if(!IsSupportedQueuedBattleFieldSpell(p)) return false;
            for(int z=0;z<5;z++)
                if(!IsSupportedQueuedBattleBackRow(p,z) ||
                    (p!=side || z!=slot) && Monsters[p,z] is { } other &&
                        (p != side || !IsPlainBattlePilot(other)))
                    return false;
        }
        return true;
    }

    private CommandResult ExecuteQueuedDirectAttack(DeclareDirectAttackCommand attack,IActionCommandSink followups)
    {
        if (!TryResolveCardTarget(attack.Attacker,out _,out _))
            return new(CommandOutcome.Skipped,"Declared attacker occurrence gone.");
        if (followups is not IActionBatchSink { PendingCount: 0 } queue ||
            !TryPlanQueuedDirectAttack(attack.Attacker,out int slot,out var card,out int damage,out _))
            return new(CommandOutcome.Rejected,"Direct attack no longer satisfies pilot restrictions.");
        attacked[attack.Side,slot]=true;
        if (HasQueuedAttackResponseTrap(attack.Side))
        {
            followups.Enqueue(new OpenAttackResponseCommand(attack.Side, attack.Attacker, null,
                primitiveActionQueue?.Active?.Origin ?? ActionOrigin.Effect));
            // This response window has no archived native opcode projection.
            return new(CommandOutcome.Applied, "Direct attack declared; response follows.");
        }
        // Declaration owns the gate. Only the subsequent command changes LP.
        EnqueueDirectBattleDamage(card, attack.Side, damage, queue);
        return new(CommandOutcome.Applied)
        {
            DirectAttack=new(attack.Attacker,card.Id,slot,damage)
        };
    }
}
