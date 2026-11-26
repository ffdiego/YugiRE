namespace Joey.Core;

public sealed partial class DemoDuel
{
    /// <summary>Admit an attack against a field slot or -1 for a direct attack.
    /// Both pilots preserve their individual eligibility and revalidation.</summary>
    public bool TryQueueAttack(int attackerSlot, int defenderSlot, out string message,
        ActionOrigin origin = ActionOrigin.Player) => defenderSlot == -1
            ? TryQueueDirectAttack(attackerSlot, out message, origin)
            : TryQueueTargetedAttack(attackerSlot, defenderSlot, out message, origin);

    /// <summary>Opt-in direct battle pilot. An eligible response can coexist
    /// with other supported normal monsters on the attacker's field.</summary>
    public bool TryQueueDirectAttack(int slot, out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Direct-attack pilot requires an unused attacker in Battle Phase without unsupported monsters or Spell/Trap effects.";
        if (primitiveActionQueue is not { IsBusy: false } queue || ActionsBlocked ||
            PendingEndDiscards != 0 || !CanAttackDirectly(slot) ||
            !TryCaptureCardTarget(ActivePlayer,DuelZone.Monster,slot,out var target) ||
            !TryPlanQueuedDirectAttack(target,out _,out _,out _)) return false;
        queue.Enqueue(new DeclareDirectAttackCommand(ActivePlayer,target),origin);
        message = "Direct attack admitted.";
        return true;
    }

    private bool TryPlanQueuedDirectAttack(CardTarget source,out int slot,out Card card,out int damage, bool declared = false)
    {
        damage=0;
        if (!TryResolveCardTarget(source,out slot,out card)) return false;
        int side=source.Side;
        if (source.Zone!=DuelZone.Monster || side!=ActivePlayer || Phase!=DemoPhase.Battle ||
            GameOver || attacked[side,slot] != declared || MonsterFaceDown[side,slot] || DefensePosition[side,slot] ||
            !card.IsMonster || !DirectAttackAllowed(side,card) ||
            BattleDamageEffectRules.AfterBattleDamage(card.Id,side,1-side,1,false) is not null ||
            card.Id is 219 or 522)
            return false;
        // Mirror Force already schedules linked removal through the wipe executor.
        if (!equipment.All(IsTrackedDirectBattleEquipment)) return false;
        bool admitAlliedNormals = HasQueuedAttackResponseTrap(side);
        for(int p=0;p<2;p++)
        {
            if(!IsSupportedQueuedBattleFieldSpell(p)) return false;
            for(int z=0;z<5;z++)
                if((!IsSupportedQueuedBattleBackRow(p,z) &&
                        !equipment.Any(e => e.Owner == p && e.SourceZone == z)) ||
                    (p!=side || z!=slot) && Monsters[p,z] is { } other &&
                        (p != side || !admitAlliedNormals || !IsPlainBattlePilot(other)))
                    return false;
        }
        damage=Math.Clamp(AttackValue(side,slot),0,ushort.MaxValue);
        return true;
    }

    private bool IsTrackedDirectBattleEquipment(Equipment link) =>
        link.Card.ClassCode == 22 && EquipmentRules.SupportsQueued(link.Card.Id) &&
        ReferenceEquals(Spells[link.Owner, link.SourceZone], link.Card) &&
        !SpellFaceDown[link.Owner, link.SourceZone] &&
        TryCaptureCardTarget(link.Owner, DuelZone.SpellTrap, link.SourceZone, out _) &&
        ReferenceEquals(Monsters[link.TargetPlayer, link.TargetZone], link.TargetCard) &&
        TryCaptureCardTarget(link.TargetPlayer, DuelZone.Monster, link.TargetZone, out _);

    private CommandResult ExecuteQueuedDirectAttack(DeclareDirectAttackCommand attack,IActionCommandSink followups)
    {
        if (!TryResolveCardTarget(attack.Attacker,out _,out _))
            return new(CommandOutcome.Skipped,"Declared attacker occurrence gone.");
        if (followups is not IActionBatchSink { PendingCount: 0 } ||
            !TryPlanQueuedDirectAttack(attack.Attacker,out int slot,out var card,out int damage))
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
        if(damage>0) followups.Enqueue(new DamageCommand(1-attack.Side,damage));
        return new(CommandOutcome.Applied)
        {
            DirectAttack=new(attack.Attacker,card.Id,slot,damage)
        };
    }
}
